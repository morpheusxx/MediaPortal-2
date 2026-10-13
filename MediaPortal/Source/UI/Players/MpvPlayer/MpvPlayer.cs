#region Copyright (C) 2007-2021 Team MediaPortal

/*
    Copyright (C) 2007-2021 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.UI.Players.Mpv.Interop;
using MediaPortal.UI.Presentation.Geometries;
using MediaPortal.UI.Presentation.Players;
using MediaPortal.UI.Presentation.Players.ResumeState;
using MediaPortal.UI.SkinEngine.Players;
using MediaPortal.UI.SkinEngine.Gpu;
using MediaPortal.UI.SkinEngine.Players;

namespace MediaPortal.UI.Players.Mpv
{
  /// <summary>
  /// Video player which is based on libmpv. libmpv decodes the video and plays the audio; the video frames are rendered into an OpenGL texture
  /// (see <see cref="VideoTarget"/>) which is drawn by the SkinEngine (see <see cref="IVideoFrameSource"/>).
  /// </summary>
  /// <remarks>
  /// <para>
  /// Threading: libmpv calls are thread safe, except the rendering which has to be done in the render thread, the thread which owns the
  /// OpenGL context. The frames are rendered at the beginning of each frame of the SkinEngine (see <see cref="VideoRenderHost.SceneBegin"/>).
  /// The events of libmpv are processed by an own thread.
  /// </para>
  /// <para>
  /// The player is created paused. The player slot controller starts the playback by calling <see cref="Resume"/>.
  /// </para>
  /// </remarks>
  public sealed unsafe class MpvPlayer : IVideoFrameSource, ISubtitlePlayer, IChapterPlayer, ITitlePlayer, IResumablePlayer,
      IMediaPlaybackControl, IAutoRepeatPlayer, IReusablePlayer, IPlayerEvents, IDisposable
  {
    #region Consts

    private const double PLAYBACK_RATE_PLAY_THRESHOLD = 0.05;

    #endregion

    #region Nested types

    private class Track
    {
      public int Id;
      public string Name;
      public bool Selected;
    }

    #endregion

    #region Protected fields

    private readonly object _surfaceLock = new object();
    private readonly object _syncObj = new object();
    private IntPtr _handle;
    private IntPtr _renderContext;
    private MpvGetProcAddressFn _getProcAddressFn;
    private MpvUpdateCallbackFn _updateFn;
    private int _updatePending = 1;
    private volatile bool _renderRequested = true;
    private volatile bool _videoParamsDirty = true;
    private bool _stateReadyFired;

    private VideoTarget _videoTarget;
    private Texture _texture;
    private Size _videoSize = Size.Empty;
    private SizeF _videoAspectRatio = SizeF.Empty;

    private Thread _eventThread;
    private volatile bool _stopEventThread;

    private IResourceLocator _resourceLocator;
    private IResourceAccessor _resourceAccessor;
    private LocalFsResourceAccessorHelper _localFsHelper;
    private MediaItem _mediaItem;
    private string _mediaItemTitle;
    private PlayerState _state = PlayerState.Stopped;
    private volatile bool _fileLoaded;
    private TimeSpan? _pendingSeek;
    private bool _isPaused = true;
    private bool _autoRepeat;
    private int _volume = 100;
    private bool _isMuted;
    private bool _disposed;

    private PlayerEventDlgt _started;
    private PlayerEventDlgt _stateReady;
    private PlayerEventDlgt _stopped;
    private PlayerEventDlgt _ended;
    private PlayerEventDlgt _playbackStateChanged;
    private PlayerEventDlgt _playbackError;

    #endregion

    #region Ctor & dispose

    public MpvPlayer()
    {
      LibMpv.InstallResolver();
      _handle = LibMpv.mpv_create();
      if (_handle == IntPtr.Zero)
        throw new InvalidOperationException("MpvPlayer: mpv_create failed");

      foreach (KeyValuePair<string, string> option in new Dictionary<string, string>
        {
          { "config", "no" },
          { "vo", "libmpv" }, // The frames are delivered through the render API
          { "hwdec", "auto-safe" },
          { "idle", "yes" }, // Keep the core alive between two files of a multi-part item
          { "pause", "yes" },
          { "keep-open", "no" },
          { "osc", "no" },
          { "osd-level", "0" },
          { "input-default-bindings", "no" },
          { "input-vo-keyboard", "no" },
          { "load-scripts", "no" },
          { "ytdl", "no" },
          { "audio-display", "no" },
          { "sub-auto", "fuzzy" },
          { "audio-client-name", "MediaPortal 2" },
          { "msg-level", "all=warn" },
        })
        SetOption(option.Key, option.Value);
      Check(LibMpv.mpv_initialize(_handle), "mpv_initialize");
      LibMpv.mpv_request_log_messages(_handle, "warn");

      // The render context has to be created in the render thread
      Exception renderException = null;
      VideoRenderHost.Invoke(() =>
        {
          try
          {
            InitRender();
          }
          catch (Exception ex)
          {
            renderException = ex;
          }
        });
      if (_renderContext == IntPtr.Zero)
      {
        LibMpv.mpv_terminate_destroy(_handle);
        _handle = IntPtr.Zero;
        throw new InvalidOperationException("MpvPlayer: Unable to create the render context", renderException);
      }

      VideoRenderHost.SceneBegin += OnSceneBegin;
      VideoRenderHost.ScenePresented += OnScenePresented;

      _eventThread = new Thread(EventLoop) { IsBackground = true, Name = "MpvPlayer events" };
      _eventThread.Start();
    }

    public void Dispose()
    {
      lock (_syncObj)
      {
        if (_disposed)
          return;
        _disposed = true;
      }
      VideoRenderHost.SceneBegin -= OnSceneBegin;
      VideoRenderHost.ScenePresented -= OnScenePresented;

      if (_state != PlayerState.Stopped)
        Stop();

      _stopEventThread = true;
      if (_handle != IntPtr.Zero)
        LibMpv.mpv_wakeup(_handle);
      if (_eventThread != null && Thread.CurrentThread != _eventThread)
        _eventThread.Join(2000);

      // The render context and the GPU resources have to be released in the render thread, before the core
      VideoRenderHost.Invoke(ReleaseRenderResources);

      if (_handle != IntPtr.Zero)
      {
        LibMpv.mpv_terminate_destroy(_handle);
        _handle = IntPtr.Zero;
      }
      DisposeResource();
    }

    #endregion

    #region libmpv helpers

    private static void Check(int result, string what)
    {
      if (result < 0)
        throw new InvalidOperationException(string.Format("MpvPlayer: {0} failed: {1}", what, LibMpv.ErrorString(result)));
    }

    private static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }

    private void SetOption(string name, string value)
    {
      int result = LibMpv.mpv_set_option_string(_handle, name, value);
      if (result < 0)
        Logger.Warn("MpvPlayer: Unable to set option {0}={1}: {2}", name, value, LibMpv.ErrorString(result));
    }

    private bool SetProperty(string name, string value)
    {
      if (_handle == IntPtr.Zero)
        return false;
      int result = LibMpv.mpv_set_property_string(_handle, name, value);
      if (result < 0)
        Logger.Debug("MpvPlayer: Unable to set property {0}={1}: {2}", name, value, LibMpv.ErrorString(result));
      return result >= 0;
    }

    private bool Command(params string[] args)
    {
      IntPtr handle = _handle;
      if (handle == IntPtr.Zero)
        return false;
      IntPtr[] pointers = new IntPtr[args.Length + 1];
      try
      {
        for (int i = 0; i < args.Length; i++)
          pointers[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
        int result = LibMpv.mpv_command(handle, pointers);
        if (result < 0)
          Logger.Debug("MpvPlayer: Command '{0}' failed: {1}", string.Join(" ", args), LibMpv.ErrorString(result));
        return result >= 0;
      }
      finally
      {
        foreach (IntPtr pointer in pointers)
          if (pointer != IntPtr.Zero)
            Marshal.FreeCoTaskMem(pointer);
      }
    }

    private double? GetDouble(string name)
    {
      IntPtr handle = _handle;
      return handle != IntPtr.Zero && LibMpv.mpv_get_property_double(handle, name, LibMpv.FORMAT_DOUBLE, out double value) >= 0 ? value : (double?) null;
    }

    private long? GetLong(string name)
    {
      IntPtr handle = _handle;
      return handle != IntPtr.Zero && LibMpv.mpv_get_property_long(handle, name, LibMpv.FORMAT_INT64, out long value) >= 0 ? value : (long?) null;
    }

    private bool GetFlag(string name)
    {
      IntPtr handle = _handle;
      return handle != IntPtr.Zero && LibMpv.mpv_get_property_int(handle, name, LibMpv.FORMAT_FLAG, out int value) >= 0 && value != 0;
    }

    private string GetString(string name)
    {
      IntPtr handle = _handle;
      if (handle == IntPtr.Zero)
        return null;
      IntPtr pointer = LibMpv.mpv_get_property_string(handle, name);
      if (pointer == IntPtr.Zero)
        return null;
      try
      {
        return Marshal.PtrToStringUTF8(pointer);
      }
      finally
      {
        LibMpv.mpv_free(pointer);
      }
    }

    #endregion

    #region Render context

    private void InitRender()
    {
      _getProcAddressFn = (ctx, name) => VideoRenderHost.GetGlProcAddress(name);
      _updateFn = ctx => Interlocked.Exchange(ref _updatePending, 1);

      MpvOpenGLInitParams init = new MpvOpenGLInitParams { GetProcAddress = Marshal.GetFunctionPointerForDelegate(_getProcAddressFn) };
      IntPtr apiType = Marshal.StringToCoTaskMemUTF8("opengl");
      try
      {
        MpvRenderParam* parameters = stackalloc MpvRenderParam[3];
        parameters[0] = new MpvRenderParam(LibMpv.RENDER_PARAM_API_TYPE, apiType);
        parameters[1] = new MpvRenderParam(LibMpv.RENDER_PARAM_OPENGL_INIT_PARAMS, (IntPtr) (&init));
        parameters[2] = default(MpvRenderParam);
        Check(LibMpv.mpv_render_context_create(out _renderContext, _handle, parameters), "mpv_render_context_create");
      }
      finally
      {
        Marshal.FreeCoTaskMem(apiType);
      }
      LibMpv.mpv_render_context_set_update_callback(_renderContext, Marshal.GetFunctionPointerForDelegate(_updateFn), IntPtr.Zero);
    }

    private void ReleaseRenderResources()
    {
      lock (_surfaceLock)
      {
        DisposeFrameResources();
        if (_renderContext != IntPtr.Zero)
        {
          LibMpv.mpv_render_context_free(_renderContext);
          _renderContext = IntPtr.Zero;
        }
      }
    }

    private void DisposeFrameResources()
    {
      if (_texture != null)
      {
        _texture.Dispose();
        _texture = null;
      }
      if (_videoTarget != null)
      {
        _videoTarget.Dispose();
        _videoTarget = null;
      }
    }

    private void OnSceneBegin(object sender, EventArgs e)
    {
      try
      {
        UpdateFrame();
      }
      catch (Exception ex)
      {
        Logger.Warn("MpvPlayer: Error rendering video frame", ex);
      }
    }

    private void OnScenePresented(object sender, EventArgs e)
    {
      IntPtr context = _renderContext;
      if (context != IntPtr.Zero)
        LibMpv.mpv_render_context_report_swap(context);
    }

    /// <summary>
    /// Renders the current video frame into the frame buffer, if there is a new one. Called in the render thread.
    /// </summary>
    private void UpdateFrame()
    {
      lock (_surfaceLock)
      {
        if (_renderContext == IntPtr.Zero)
          return;
        bool newFrame = false;
        if (Interlocked.Exchange(ref _updatePending, 0) == 1)
          newFrame = (LibMpv.mpv_render_context_update(_renderContext) & LibMpv.RENDER_UPDATE_FRAME) != 0;
        if (!newFrame && !_renderRequested)
          return;

        if (_videoParamsDirty || _videoSize.IsEmpty)
          RefreshVideoParams();
        if (_videoSize.Width <= 0 || _videoSize.Height <= 0)
          return;

        if (_videoTarget == null)
          _videoTarget = new VideoTarget();
        if (_videoTarget.Ensure(_videoSize.Width, _videoSize.Height) || _texture == null)
        {
          if (_texture != null)
            _texture.Dispose();
          _texture = Texture.FromOpenGlTexture(_videoTarget.TextureId, _videoTarget.Width, _videoTarget.Height);
        }
        _renderRequested = false;

        MpvOpenGLFbo target = new MpvOpenGLFbo { Fbo = (int) _videoTarget.Fbo, W = _videoTarget.Width, H = _videoTarget.Height, InternalFormat = 0 };
        int flipY = 0; // The first row of the texture is the top row
        int block = 0;
        MpvRenderParam* parameters = stackalloc MpvRenderParam[4];
        parameters[0] = new MpvRenderParam(LibMpv.RENDER_PARAM_OPENGL_FBO, (IntPtr) (&target));
        parameters[1] = new MpvRenderParam(LibMpv.RENDER_PARAM_FLIP_Y, (IntPtr) (&flipY));
        parameters[2] = new MpvRenderParam(LibMpv.RENDER_PARAM_BLOCK_FOR_TARGET_TIME, (IntPtr) (&block));
        parameters[3] = default(MpvRenderParam);
        int result = LibMpv.mpv_render_context_render(_renderContext, parameters);
        if (result < 0)
          Logger.Warn("MpvPlayer: Render error: {0}", LibMpv.ErrorString(result));
        // libmpv changed the OpenGL state, so Skia has to forget what it knows about it
        VideoRenderHost.ResetGlState();
      }
    }

    private void RefreshVideoParams()
    {
      _videoParamsDirty = false;
      long? width = GetLong("video-params/w");
      long? height = GetLong("video-params/h");
      if (!width.HasValue || !height.HasValue || width.Value <= 0 || height.Value <= 0)
        return;
      _videoSize = new Size((int) width.Value, (int) height.Value);
      long? displayWidth = GetLong("video-params/dw");
      long? displayHeight = GetLong("video-params/dh");
      _videoAspectRatio = displayWidth.HasValue && displayHeight.HasValue && displayWidth.Value > 0 && displayHeight.Value > 0 ?
          new SizeF(displayWidth.Value, displayHeight.Value) : new SizeF(_videoSize.Width, _videoSize.Height);
      if (!_stateReadyFired)
      {
        _stateReadyFired = true;
        FireStateReady();
      }
    }

    #endregion

    #region Event processing

    private void EventLoop()
    {
      while (!_stopEventThread)
      {
        IntPtr handle = _handle;
        if (handle == IntPtr.Zero)
          return;
        try
        {
          IntPtr ev = LibMpv.mpv_wait_event(handle, 0.2);
          int id = Marshal.ReadInt32(ev);
          switch (id)
          {
            case LibMpv.EVENT_NONE:
              break;
            case LibMpv.EVENT_SHUTDOWN:
              return;
            case LibMpv.EVENT_LOG_MESSAGE:
              // mpv_event_log_message: const char* prefix, level, text
              IntPtr log = Marshal.ReadIntPtr(ev, 16);
              if (log != IntPtr.Zero)
                Logger.Debug("MpvPlayer: mpv [{0}] {1}: {2}", Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(log, 0)),
                    Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(log, IntPtr.Size)), (Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(log, 2 * IntPtr.Size)) ?? string.Empty).TrimEnd());
              break;
            case LibMpv.EVENT_FILE_LOADED:
              OnFileLoaded();
              break;
            case 17: // MPV_EVENT_VIDEO_RECONFIG
              _videoParamsDirty = true;
              _renderRequested = true;
              break;
            case LibMpv.EVENT_END_FILE:
              // mpv_event: int event_id; int error; uint64 reply_userdata; void* data -> mpv_event_end_file: int reason; int error
              IntPtr data = Marshal.ReadIntPtr(ev, 16);
              if (data != IntPtr.Zero)
                OnEndFile(Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4));
              break;
          }
        }
        catch (Exception ex)
        {
          Logger.Warn("MpvPlayer: Error processing event", ex);
        }
      }
    }

    private void OnFileLoaded()
    {
      _fileLoaded = true;
      _videoParamsDirty = true;
      _renderRequested = true;
      TimeSpan? seek = _pendingSeek;
      _pendingSeek = null;
      if (seek.HasValue)
        Command("seek", seek.Value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture), "absolute");
      Logger.Debug("MpvPlayer: Loaded '{0}', video codec: {1}, hardware decoding: {2}", _mediaItemTitle, GetString("video-codec") ?? "-", GetString("hwdec-current") ?? "no");
      if (GetString("vid") == "no" && !_stateReadyFired)
      { // No video track, no video size will be reported
        _stateReadyFired = true;
        FireStateReady();
      }
    }

    private void OnEndFile(int reason, int error)
    {
      if (reason == LibMpv.END_FILE_REASON_ERROR)
      {
        Logger.Warn("MpvPlayer: Playback of '{0}' failed: {1}", _mediaItemTitle, LibMpv.ErrorString(error));
        _state = PlayerState.Stopped;
        PlayerEventDlgt handler = _playbackError;
        if (handler != null)
          handler(this);
        return;
      }
      if (reason != LibMpv.END_FILE_REASON_EOF)
        return; // Stopped by us or by the loading of another file
      _fileLoaded = false;
      bool hasNextPart = _mediaItem != null && _mediaItem.ActiveResourceLocatorIndex < _mediaItem.MaximumResourceLocatorIndex;
      if (hasNextPart)
      {
        Logger.Debug("MpvPlayer: Part {0} ended, requesting next part", _mediaItem.ActiveResourceLocatorIndex);
        FireNextItemRequest();
        return;
      }
      _state = PlayerState.Ended;
      Logger.Debug("MpvPlayer: Playback ended");
      FireEnded();
    }

    #endregion

    #region Loading

    /// <summary>
    /// Opens the media item and prepares the playback. The player is paused after this method.
    /// </summary>
    public void SetMediaItem(IResourceLocator locator, string mediaItemTitle, MediaItem mediaItem)
    {
      _mediaItem = mediaItem;
      _mediaItemTitle = mediaItemTitle;
      DisposeResource();
      _resourceLocator = locator;
      _resourceAccessor = locator.CreateAccessor();

      string target;
      string deviceOption = null;
      INetworkResourceAccessor networkAccessor = _resourceAccessor as INetworkResourceAccessor;
      if (networkAccessor != null)
        target = networkAccessor.URL;
      else
      {
        ILocalFsResourceAccessor localAccessor = _resourceAccessor as ILocalFsResourceAccessor;
        if (localAccessor == null)
        {
          // Make remote resources available as local file system resource
          _localFsHelper = new LocalFsResourceAccessorHelper(_resourceAccessor);
          localAccessor = _localFsHelper.LocalFsResourceAccessor;
        }
        if (localAccessor == null)
          throw new InvalidOperationException("MpvPlayer: The resource can neither be accessed by URL nor by a local path");
        target = localAccessor.LocalFileSystemPath;
        if (string.Equals(System.IO.Path.GetExtension(target), ".ifo", StringComparison.OrdinalIgnoreCase))
        { // DVD structure: play the whole disc from the directory
          deviceOption = System.IO.Path.GetDirectoryName(target);
          target = "dvd://";
        }
      }

      _state = PlayerState.Active;
      _isPaused = true;
      _fileLoaded = false;
      _stateReadyFired = false;
      _videoSize = Size.Empty;
      _videoAspectRatio = SizeF.Empty;
      _videoParamsDirty = true;
      _renderRequested = true;
      SetProperty("pause", "yes");
      SetProperty("volume", _volume.ToString(CultureInfo.InvariantCulture));
      SetProperty("mute", _isMuted ? "yes" : "no");
      SetProperty("loop-file", _autoRepeat && !HasMultipleParts ? "inf" : "no");
      if (deviceOption != null)
        SetProperty("dvd-device", deviceOption);

      Logger.Debug("MpvPlayer: Opening '{0}'", target);
      if (!Command("loadfile", target, "replace"))
      {
        _state = PlayerState.Stopped;
        throw new InvalidOperationException("MpvPlayer: Unable to open '" + target + "'");
      }
    }

    private bool HasMultipleParts
    {
      get { return _mediaItem != null && _mediaItem.MaximumResourceLocatorIndex > 0; }
    }

    private void DisposeResource()
    {
      if (_localFsHelper != null)
      {
        _localFsHelper.Dispose();
        _localFsHelper = null;
      }
      if (_resourceAccessor != null)
      {
        _resourceAccessor.Dispose();
        _resourceAccessor = null;
      }
      _resourceLocator = null;
    }

    #endregion

    #region IPlayer implementation

    public string Name
    {
      get { return "mpv"; }
    }

    public PlayerState State
    {
      get { return _state; }
    }

    public string MediaItemTitle
    {
      get { return _mediaItemTitle; }
    }

    public void Stop()
    {
      if (_state == PlayerState.Stopped)
        return;
      Logger.Debug("MpvPlayer: Stop");
      _state = PlayerState.Stopped;
      _isPaused = false;
      _fileLoaded = false;
      _pendingSeek = null;
      Command("stop");
      FireStopped();
    }

    public override string ToString()
    {
      return string.Format("MpvPlayer: {0}", _mediaItemTitle ?? "no resource");
    }

    #endregion

    #region IReusablePlayer implementation

    public event RequestNextItemDlgt NextItemRequest;

    public bool NextItem(MediaItem mediaItem, StartTime startTime)
    {
      string mimeType;
      string title;
      // Only re-use the player for multi-part files
      if (!mediaItem.GetPlayData(out mimeType, out title) || mediaItem.MaximumResourceLocatorIndex == 0 && mediaItem.MaximumEditionIndex == 0)
      {
        Logger.Debug("MpvPlayer: Can reuse current player only for multi-resource items");
        return false;
      }
      if (mediaItem.ActiveResourceLocatorIndex > mediaItem.MaximumResourceLocatorIndex)
        return false;
      Logger.Debug("MpvPlayer: Changed resource to index {0}", mediaItem.ActiveResourceLocatorIndex);
      SetMediaItem(mediaItem.GetResourceLocator(), title, mediaItem);
      SetProperty("pause", "no");
      _isPaused = false;
      return true;
    }

    #endregion

    #region IPlayerEvents implementation

    public void InitializePlayerEvents(PlayerEventDlgt started, PlayerEventDlgt stateReady, PlayerEventDlgt stopped,
        PlayerEventDlgt ended, PlayerEventDlgt playbackStateChanged, PlayerEventDlgt playbackError)
    {
      _started = started;
      _stateReady = stateReady;
      _stopped = stopped;
      _ended = ended;
      _playbackStateChanged = playbackStateChanged;
      _playbackError = playbackError;
    }

    public void ResetPlayerEvents()
    {
      _started = null;
      _stateReady = null;
      _stopped = null;
      _ended = null;
      _playbackStateChanged = null;
      _playbackError = null;
    }

    private void FireStarted()
    {
      PlayerEventDlgt handler = _started;
      if (handler != null)
        handler(this);
    }

    private void FireStateReady()
    {
      PlayerEventDlgt handler = _stateReady;
      if (handler != null)
        handler(this);
    }

    private void FireStopped()
    {
      PlayerEventDlgt handler = _stopped;
      if (handler != null)
        handler(this);
    }

    private void FireEnded()
    {
      PlayerEventDlgt handler = _ended;
      if (handler != null)
        handler(this);
    }

    private void FireNextItemRequest()
    {
      RequestNextItemDlgt handler = NextItemRequest;
      if (handler != null)
        handler(this);
    }

    private void FirePlaybackStateChanged()
    {
      PlayerEventDlgt handler = _playbackStateChanged;
      if (handler != null)
        handler(this);
    }

    #endregion

    #region IMediaPlaybackControl implementation

    public TimeSpan CurrentTime
    {
      get
      {
        if (_pendingSeek.HasValue)
          return _pendingSeek.Value;
        double? position = GetDouble("time-pos");
        return position.HasValue ? TimeSpan.FromSeconds(Math.Max(0, position.Value)) : TimeSpan.Zero;
      }
      set
      {
        Logger.Debug("MpvPlayer: Seek to {0} seconds", value.TotalSeconds);
        if (_state != PlayerState.Active)
          Pause();
        if (!_fileLoaded)
        { // The seek is done as soon as the file is loaded
          _pendingSeek = value;
          return;
        }
        Command("seek", value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture), "absolute");
      }
    }

    public TimeSpan Duration
    {
      get
      {
        double? duration = GetDouble("duration");
        return duration.HasValue ? TimeSpan.FromSeconds(duration.Value) : TimeSpan.Zero;
      }
    }

    public double PlaybackRate
    {
      get { return GetDouble("speed") ?? 1.0; }
    }

    public bool SetPlaybackRate(double value)
    {
      if (value <= 0 || Math.Abs(PlaybackRate - value) < 0.001)
        return false;
      bool result = SetProperty("speed", value.ToString("0.###", CultureInfo.InvariantCulture));
      if (result)
        FirePlaybackStateChanged();
      return result;
    }

    public bool IsPlayingAtNormalRate
    {
      get { return Math.Abs(PlaybackRate - 1) < PLAYBACK_RATE_PLAY_THRESHOLD; }
    }

    public bool IsSeeking
    {
      get { return _state == PlayerState.Active && !IsPlayingAtNormalRate; }
    }

    public bool CanSeekForwards
    {
      get { return _fileLoaded && GetFlag("seekable"); }
    }

    public bool CanSeekBackwards
    {
      get { return _fileLoaded && GetFlag("seekable"); }
    }

    public bool IsPaused
    {
      get { return _isPaused; }
    }

    public void Pause()
    {
      if (_isPaused)
        return;
      Logger.Debug("MpvPlayer: Pause");
      StopSeeking();
      SetProperty("pause", "yes");
      _isPaused = true;
      _state = PlayerState.Active;
      FirePlaybackStateChanged();
    }

    public void Resume()
    {
      if (!_isPaused && !IsSeeking)
        return;
      Logger.Debug("MpvPlayer: Resume");
      StopSeeking();
      SetProperty("pause", "no");
      _isPaused = false;
      _state = PlayerState.Active;
      FirePlaybackStateChanged();
    }

    public void Restart()
    {
      CurrentTime = TimeSpan.Zero;
      StopSeeking();
      SetProperty("pause", "no");
      _isPaused = false;
      _state = PlayerState.Active;
      FireStarted();
    }

    private void StopSeeking()
    {
      SetPlaybackRate(1);
    }

    #endregion

    #region IAutoRepeatPlayer implementation

    public bool AutoRepeat
    {
      get { return _autoRepeat; }
      set
      {
        _autoRepeat = value;
        // Multi-part items are repeated by the player manager
        SetProperty("loop-file", value && !HasMultipleParts ? "inf" : "no");
      }
    }

    #endregion

    #region IVolumeControl implementation

    public int Volume
    {
      get { return _volume; }
      set
      {
        _volume = value;
        SetProperty("volume", value.ToString(CultureInfo.InvariantCulture));
      }
    }

    public bool Mute
    {
      get { return _isMuted; }
      set
      {
        _isMuted = value;
        SetProperty("mute", value ? "yes" : "no");
      }
    }

    #endregion

    #region IVideoPlayer implementation

    public Size VideoSize
    {
      get { return _videoSize; }
    }

    public SizeF VideoAspectRatio
    {
      get { return _videoAspectRatio; }
    }

    public IGeometry GeometryOverride { get; set; }

    public string EffectOverride { get; set; }

    public CropSettings CropSettings { get; set; }

    public string[] AudioStreams
    {
      get { return GetTrackNames("audio"); }
    }

    public string CurrentAudioStream
    {
      get { return GetSelectedTrackName("audio"); }
    }

    public void SetAudioStream(string audioStream)
    {
      SelectTrack("audio", "aid", audioStream);
    }

    #endregion

    #region IVideoFrameSource implementation

    public Texture Texture
    {
      get
      {
        lock (_surfaceLock)
          return _texture;
      }
    }

    public SharpDX.Rectangle CropVideoRect
    {
      get
      {
        Size size = _videoSize;
        CropSettings crop = CropSettings;
        if (crop == null)
          return new SharpDX.Rectangle(0, 0, size.Width, size.Height);
        return new SharpDX.Rectangle(crop.Left, crop.Top, size.Width - crop.Left - crop.Right, size.Height - crop.Top - crop.Bottom);
      }
    }

    public object SurfaceLock
    {
      get { return _surfaceLock; }
    }

    public void ReleaseGUIResources()
    {
      lock (_surfaceLock)
        DisposeFrameResources();
    }

    public void ReallocGUIResources()
    {
      // The frame buffer is created on demand with the next frame
      _renderRequested = true;
    }

    #endregion

    #region Tracks (audio streams and subtitles)

    private List<Track> GetTracks(string type)
    {
      List<Track> result = new List<Track>();
      long count = GetLong("track-list/count") ?? 0;
      HashSet<string> names = new HashSet<string>();
      for (int i = 0; i < count; i++)
      {
        string prefix = "track-list/" + i + "/";
        if (GetString(prefix + "type") != type)
          continue;
        long? id = GetLong(prefix + "id");
        if (!id.HasValue)
          continue;
        string language = GetString(prefix + "lang");
        string title = GetString(prefix + "title");
        string codec = GetString(prefix + "codec");
        string name = !string.IsNullOrEmpty(language) ? GetLanguageName(language) : null;
        if (!string.IsNullOrEmpty(title) && title != name)
          name = name == null ? title : name + " - " + title;
        if (name == null)
          name = (type == "audio" ? "Audio " : "Subtitle ") + id.Value;
        if (!string.IsNullOrEmpty(codec))
          name += " (" + codec + ")";
        if (!names.Add(name))
          name += " [" + id.Value + "]";
        result.Add(new Track { Id = (int) id.Value, Name = name, Selected = GetFlag(prefix + "selected") });
      }
      return result;
    }

    private string[] GetTrackNames(string type)
    {
      return GetTracks(type).ConvertAll(t => t.Name).ToArray();
    }

    private string GetSelectedTrackName(string type)
    {
      Track selected = GetTracks(type).Find(t => t.Selected);
      return selected == null ? null : selected.Name;
    }

    private void SelectTrack(string type, string property, string name)
    {
      Track track = GetTracks(type).Find(t => t.Name == name);
      if (track == null)
        return;
      SetProperty(property, track.Id.ToString(CultureInfo.InvariantCulture));
      FirePlaybackStateChanged();
    }

    private static readonly Dictionary<string, string> _languageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string GetLanguageName(string code)
    {
      lock (_languageNames)
      {
        string name;
        if (_languageNames.TryGetValue(code, out name))
          return name;
        name = code;
        try
        {
          foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
            if (code.Length == 2 && string.Equals(culture.TwoLetterISOLanguageName, code, StringComparison.OrdinalIgnoreCase) ||
                code.Length == 3 && string.Equals(culture.ThreeLetterISOLanguageName, code, StringComparison.OrdinalIgnoreCase))
            {
              name = culture.EnglishName;
              break;
            }
        }
        catch (Exception)
        { }
        _languageNames[code] = name;
        return name;
      }
    }

    #endregion

    #region ISubtitlePlayer implementation

    public string[] Subtitles
    {
      get { return GetTrackNames("sub"); }
    }

    public string CurrentSubtitle
    {
      get { return GetSelectedTrackName("sub"); }
    }

    public void SetSubtitle(string subtitle)
    {
      SelectTrack("sub", "sid", subtitle);
    }

    public void DisableSubtitle()
    {
      SetProperty("sid", "no");
      FirePlaybackStateChanged();
    }

    #endregion

    #region IChapterPlayer implementation

    private string[] GetChapterNames()
    {
      long count = GetLong("chapter-list/count") ?? 0;
      string[] result = new string[count];
      for (int i = 0; i < count; i++)
      {
        string title = GetString("chapter-list/" + i + "/title");
        result[i] = string.IsNullOrEmpty(title) ? "Chapter " + (i + 1) : (i + 1) + ": " + title;
      }
      return result;
    }

    public string[] Chapters
    {
      get { return GetChapterNames(); }
    }

    public bool ChaptersAvailable
    {
      get { return (GetLong("chapter-list/count") ?? 0) > 1; }
    }

    public string CurrentChapter
    {
      get
      {
        long? chapter = GetLong("chapter");
        string[] names = GetChapterNames();
        return chapter.HasValue && chapter.Value >= 0 && chapter.Value < names.Length ? names[chapter.Value] : null;
      }
    }

    public void SetChapter(string chapter)
    {
      int index = Array.IndexOf(GetChapterNames(), chapter);
      if (index >= 0)
        SetProperty("chapter", index.ToString(CultureInfo.InvariantCulture));
    }

    public void NextChapter()
    {
      Command("add", "chapter", "1");
    }

    public void PrevChapter()
    {
      Command("add", "chapter", "-1");
    }

    #endregion

    #region ITitlePlayer implementation

    private string[] GetTitleNames()
    {
      long count = GetLong("disc-titles") ?? 0;
      string[] result = new string[count];
      for (int i = 0; i < count; i++)
        result[i] = "Title " + (i + 1);
      return result;
    }

    public string[] Titles
    {
      get { return GetTitleNames(); }
    }

    public string CurrentTitle
    {
      get
      {
        long? title = GetLong("disc-title");
        string[] names = GetTitleNames();
        return title.HasValue && title.Value >= 0 && title.Value < names.Length ? names[title.Value] : null;
      }
    }

    public void SetTitle(string title)
    {
      int index = Array.IndexOf(GetTitleNames(), title);
      if (index >= 0)
        SetProperty("disc-title", index.ToString(CultureInfo.InvariantCulture));
    }

    #endregion

    #region IResumablePlayer implementation

    public bool GetResumeState(out IResumeState state)
    {
      TimeSpan currentTime = CurrentTime;
      TimeSpan duration = Duration;
      // If we already played back more then 99%, we don't want to ask the user to resume the playback
      if (duration.TotalSeconds > 0 && currentTime.TotalSeconds / duration.TotalSeconds > 0.99)
        state = null;
      else
        state = new PositionResumeState
          {
            ResumePosition = currentTime,
            ActiveResourceLocatorIndex = _mediaItem == null ? 0 : _mediaItem.ActiveResourceLocatorIndex,
            ActiveEditionIndex = _mediaItem == null ? 0 : _mediaItem.ActiveEditionIndex
          };
      return true;
    }

    public bool SetResumeState(IResumeState state)
    {
      PositionResumeState pos = state as PositionResumeState;
      if (pos == null)
        return false;
      if (_mediaItem != null)
      {
        // Check for multi-resource media items, first set the matching part, then the position
        if (pos.ActiveResourceLocatorIndex != _mediaItem.ActiveResourceLocatorIndex && pos.ActiveResourceLocatorIndex <= _mediaItem.MaximumResourceLocatorIndex ||
            pos.ActiveEditionIndex != _mediaItem.ActiveEditionIndex && pos.ActiveEditionIndex <= _mediaItem.MaximumEditionIndex)
        {
          _mediaItem.ActiveResourceLocatorIndex = pos.ActiveResourceLocatorIndex;
          _mediaItem.ActiveEditionIndex = pos.ActiveEditionIndex;
          if (!NextItem(_mediaItem, StartTime.AtOnce))
            return false;
        }
      }
      CurrentTime = pos.ResumePosition;
      return true;
    }

    #endregion
  }
}

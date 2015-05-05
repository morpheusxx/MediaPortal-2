#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DirectShow;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.UI.Players.Video.Tools;
using MediaPortal.UI.Presentation.Players;
using MediaPortal.UI.SkinEngine.ContentManagement;
using MediaPortal.UI.SkinEngine.DirectX11;
using MediaPortal.UI.SkinEngine.Rendering;
using MediaPortal.UI.SkinEngine.SkinManagement;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.DXGI;
using AlphaMode = SharpDX.Direct2D1.AlphaMode;

namespace MediaPortal.UI.Players.Video.Subtitles
{
  #region Structs and helper classes

  /// <summary>
  /// Structure used in communication with subtitle v3 filter.
  /// </summary>
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct NativeSubtitle
  {
    // Start of bitmap fields
    public Int32 Type;
    public Int32 Width;
    public Int32 Height;
    public Int32 WidthBytes;
    public UInt16 Planes;
    public UInt16 BitsPixel;
    public IntPtr Bits;
    // End of bitmap fields

    // Start of screen size definition
    public Int32 ScreenWidth;
    public Int32 ScreenHeight;

    // Subtitle timestmap
    public UInt64 TimeStamp;

    // How long to display subtitle
    public UInt64 TimeOut; // in seconds
    public Int32 FirstScanLine;
    public Int32 HorizontalPosition;
  }

  /*
   * int character_table;
  LPCSTR language;
  int page;
  LPCSTR text;
  int firstLine;  // can be 0 to (totalLines - 1)
  int totalLines; // for teletext this is 25 lines

  unsigned    __int64 timestamp;
  unsigned    __int64 timeOut;

  */
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct TextSubtitle
  {
    public int Encoding;
    public string Language;

    public int Page;
    public string Text; // subtitle lines seperated by newline characters
    public LineContent[] LineContents;
    public UInt64 TimeStamp;
    public UInt64 TimeOut; // in seconds
  }

  public enum TeletextCharTable
  {
    English = 1,
    Swedish = 2,
    Third = 3,
    Fourth = 4,
    Fifth = 5,
    Sixth = 6
  }

  public class TeletextPageEntry
  {
    public TeletextPageEntry() { }

    public TeletextPageEntry(TeletextPageEntry e)
    {
      Page = e.Page;
      Encoding = e.Encoding;
      Language = String.Copy(e.Language);
    }

    public int Page = -1; // indicates not valid
    public TeletextCharTable Encoding;
    public string Language;
  }

  public class Subtitle : IDisposable
  {
    public static int IdCount = 0;

    public Subtitle()
    {
      Id = IdCount++;
    }

    public uint Width;
    public uint Height;
    public double PresentTime;  // NOTE: in seconds
    public double TimeOut;      // NOTE: in seconds
    public int FirstScanLine;
    public long Id = 0;
    public bool ShouldDraw;
    public Int32 ScreenWidth; // Required for aspect ratio correction
    public Int32 HorizontalPosition;
    public Bitmap1 SubTexture;

    public override string ToString()
    {
      return "Subtitle " + Id + " meta data: Timeout=" + TimeOut + " timestamp=" + PresentTime;
    }

    public void Dispose()
    {
      if (SubTexture != null)
        SubTexture.Dispose();
      SubTexture = null;
    }
  }

  #endregion

  #region DVBSub2(3) interfaces

  /// <summary>
  /// Interface to the subtitle filter, which allows us to get notified of subtitles and retrieve them.
  /// </summary>
  [Guid("4A4fAE7C-6095-11DC-8314-0800200C9A66"),
  InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  public interface IDVBSubtitleSource
  {
    void SetBitmapCallback(IntPtr callBack);
    void SetResetCallback(IntPtr callBack);
    void SetUpdateTimeoutCallback(IntPtr callBack);
    void StatusTest(int status);
  }

  [UnmanagedFunctionPointer(CallingConvention.StdCall)]
  public delegate int SubtitleCallback(IntPtr sub);
  public delegate int ResetCallback();
  public delegate int UpdateTimeoutCallback(ref Int64 timeOut);

  #endregion

  /// <summary>
  /// SubtitleRenderer uses the DVBSub2 direct show filter in the video graph to retrieve subtitles.
  /// The subtitles are handled by drawing bitmap to the video frame (<see cref="DrawOverlay"/>).
  /// </summary>
  public class SubtitleRenderer : IDisposable
  {
    #region Constants

    private const int MAX_SUBTITLES_IN_QUEUE = 20;
    public static Guid CLSID_DVBSUB2 = new Guid("{1CF3606B-6F89-4813-9D05-F9CA324CF2EA}");
    public static Guid CLSID_DVBSUB3 = new Guid("{3B4C4F66-739F-452c-AFC4-1C039BED3299}");

    #endregion

    #region Fields

    // DirectX DeviceEx to handle graphic operations
    // protected DeviceEx _device;
    protected IDVBSubtitleSource _subFilter = null;

    /// <summary>
    /// Reference to the DirectShow DVBSub filter, which 
    /// is the source of our subtitle bitmaps
    /// </summary>
    protected IBaseFilter _filter = null;

    // The current player associated with this instance
    protected IMediaPlaybackControl _player = null;

    // important, these delegates must NOT be garbage collected
    // or horrible things will happen when the native code tries to call those!
    protected SubtitleCallback _callBack;
    protected readonly ResetCallback _resetCallBack;
    protected readonly UpdateTimeoutCallback _updateTimeoutCallBack;

    // Timestamp offset in MILLISECONDS
    protected double _startPos = 0;

    protected readonly LinkedList<Subtitle> _subtitles;
    protected readonly object _syncObj = new object();

    protected double _currentTime; // File position on last render
    protected bool _useBitmap = true; // If false use teletext
    protected long _subCounter = 0;
    protected bool _clearOnNextRender = false;
    protected bool _renderSubtitles = true;
    protected int _activeSubPage; // If use teletext, what page
    protected int _drawCount = 0;

    protected readonly Action _onTextureInvalidated;
    protected Thread _subtitleSyncThread;

    // Morpheus, 2014-05-08: TODO: this is a special workaround for a strange DVBSub3 behavior: the very first subtitle is a black rectangle that covers nearly full screen.
    // Remove this when the DirectShow filter has been fixed!
    protected bool _firstCallback = true;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or Sets a flag if subtitles should be rendered.
    /// </summary>
    public bool RenderSubtitles
    {
      get
      {
        lock (_syncObj)
          return _renderSubtitles;
      }
      set
      {
        if (value)
          EnableSubtitleHandling();
        else
          DisableSubtitleHandling();
      }
    }

    #endregion

    #region Constructor and initialization

    /// <summary>
    /// Constructs a <see cref="SubtitleRenderer"/> instance.
    /// </summary>
    public SubtitleRenderer(Action onTextureInvalidated)
    {
      _onTextureInvalidated = onTextureInvalidated;
      _subtitles = new LinkedList<Subtitle>();
      //instance.textCallBack = new TextSubtitleCallback(instance.OnTextSubtitle);
      _resetCallBack = Reset;
      _updateTimeoutCallBack = UpdateTimeout;
    }

    public void SetPlayer(IMediaPlaybackControl p)
    {
      lock (_syncObj)
      {
        _subtitles.Clear();
        _clearOnNextRender = true;
        _player = p;
      }
    }

    public void SetSubtitleOption(SubtitleOption option)
    {
      if (option.type == SubtitleType.None)
      {
        _useBitmap = false;
        _activeSubPage = 0;
      }
      else if (option.type == SubtitleType.Teletext)
      {
        _useBitmap = false;
        _activeSubPage = option.entry.Page;
        ServiceRegistration.Get<ILogger>().Debug("SubtitleRender: Now rendering {0} teletext subtitle page {1}", option.language, _activeSubPage);
      }
      else if (option.type == SubtitleType.Bitmap)
      {
        _useBitmap = true;
        ServiceRegistration.Get<ILogger>().Debug("SubtitleRender: Now rendering bitmap subtitles in language {0}", option.language);
      }
      else
      {
        ServiceRegistration.Get<ILogger>().Error("Unknown subtitle option " + option);
      }
    }

    #endregion

    #region Callback and event handling

    /// <summary>
    /// Alerts the subtitle render that a seek has just been performed.
    /// Stops displaying the current subtitle and removes any cached subtitles.
    /// Furthermore updates the time that playback starts after the seek.
    /// </summary>
    /// <returns></returns>
    public int OnSeek(double startPos)
    {
      ServiceRegistration.Get<ILogger>().Debug("SubtitleRenderer: OnSeek - clear subtitles");
      // Remove all previously received subtitles
      lock (_syncObj)
      {
        _subtitles.Clear();
        // Fixed seeking, currently TsPlayer & TsReader is not reseting the base time when seeking
        //this.startPos = startPos;
        _clearOnNextRender = true;
        //posOnLastTextSub = -1;
      }
      ServiceRegistration.Get<ILogger>().Debug("New StartPos is " + startPos);
      return 0;
    }

    /// <summary>
    /// Callback from subtitle filter. Updates the latest subtitle timeout.
    /// </summary>
    /// <returns>The return value is always <c>0</c>.</returns>
    public int UpdateTimeout(ref Int64 timeOut)
    {
      ServiceRegistration.Get<ILogger>().Debug("SubtitleRenderer: UpdateTimeout");
      Subtitle latest;
      lock (_syncObj)
        latest = _subtitles.Count > 0 ? _subtitles.Last.Value : null;

      if (latest != null)
      {
        latest.TimeOut = (double)timeOut / 1000.0f;
        ServiceRegistration.Get<ILogger>().Debug("  new timeOut = {0}", latest.TimeOut);
      }
      return 0;
    }

    /// <summary>
    /// Callback from subtitle filter, alerting us that a new subtitle is available.
    /// It receives the new subtitle as the argument sub, which data is only valid for the duration of OnSubtitleV2.
    /// </summary>
    /// <returns>The return value is always <c>0</c>.</returns>
    public int OnSubtitle(IntPtr sub)
    {
      if (_firstCallback)
      {
        // See field comment for reason
        _firstCallback = false;
        return 0;
      }
      if (!_useBitmap)
        return 0; // TODO: Might be good to let this cache and then check in Render method because bitmap subs arrive a while before display

      ServiceRegistration.Get<ILogger>().Debug("OnSubtitle - stream position " + TimeSpan.FromSeconds(_currentTime));
      lock (_syncObj)
      {
        try
        {
          Subtitle subtitle = ToSubtitle(sub);
          //subtitle.SubBitmap.Save(string.Format("C:\\temp\\sub{0}.png", subtitle.Id), ImageFormat.Png); // debug
          ServiceRegistration.Get<ILogger>().Debug("SubtitleRenderer: to = " + subtitle.TimeOut + " ts=" + subtitle.PresentTime + " fsl=" + subtitle.FirstScanLine + " (startPos = " + _startPos + ")");

          while (_subtitles.Count >= MAX_SUBTITLES_IN_QUEUE)
          {
            ServiceRegistration.Get<ILogger>().Debug("SubtitleRenderer: Subtitle queue too big, discarding first element");
            _subtitles.RemoveFirst();
          }
          _subtitles.AddLast(subtitle);
          ServiceRegistration.Get<ILogger>().Debug("SubtitleRenderer: Subtitle added, now have {0} subtitles in cache", _subtitles.Count);
        }
        catch (Exception e)
        {
          ServiceRegistration.Get<ILogger>().Error(e);
        }
      }
      return 0;
    }

    // Currently unused, teletext subtitles are not yet (re-)implemented!
    public void OnTextSubtitle(ref TextSubtitle sub)
    {
      ServiceRegistration.Get<ILogger>().Debug("On TextSubtitle called");
      try
      {
        if (sub.Page == _activeSubPage)
        {
          ServiceRegistration.Get<ILogger>().Debug("Page: " + sub.Page);
          ServiceRegistration.Get<ILogger>().Debug("Character table: " + sub.Encoding);
          ServiceRegistration.Get<ILogger>().Debug("Timeout: " + sub.TimeOut);
          ServiceRegistration.Get<ILogger>().Debug("Timestamp" + sub.TimeStamp);
          ServiceRegistration.Get<ILogger>().Debug("Language: " + sub.Language);

          String content = sub.Text;
          if (content == null)
          {
            ServiceRegistration.Get<ILogger>().Error("OnTextSubtitle: sub.txt == null!");
            return;
          }
        }
      }
      catch (Exception e)
      {
        ServiceRegistration.Get<ILogger>().Error("Problem with TEXT_SUBTITLE");
        ServiceRegistration.Get<ILogger>().Error(e);
      }

      try
      {
        // if we dont need the subtitle
        if (!_renderSubtitles || _useBitmap || (_activeSubPage != sub.Page))
        {
          ServiceRegistration.Get<ILogger>().Debug("Text subtitle (page {0}) discarded: useBitmap is {1} and activeSubPage is {2}", sub.Page, _useBitmap, _activeSubPage);
          return;
        }
        ServiceRegistration.Get<ILogger>().Debug("Text subtitle (page {0}) ACCEPTED: useBitmap is {1} and activeSubPage is {2}", sub.Page, _useBitmap, _activeSubPage);

        Subtitle subtitle = new Subtitle
                              {
                                SubTexture = RenderText(sub.LineContents),
                                TimeOut = sub.TimeOut,
                                PresentTime = sub.TimeStamp / 90000.0f + _startPos,
                              };

        if (subtitle.SubTexture != null)
        {
          subtitle.Height = (uint)subtitle.SubTexture.PixelSize.Height;
          subtitle.Width = (uint)subtitle.SubTexture.PixelSize.Width;
          subtitle.FirstScanLine = (int)(SkinContext.SkinResources.SkinHeight - 50 - subtitle.Height);
          subtitle.HorizontalPosition = (int)((SkinContext.SkinResources.SkinWidth - subtitle.Width) / 2);
        }

        lock (_subtitles)
        {
          while (_subtitles.Count >= MAX_SUBTITLES_IN_QUEUE)
          {
            ServiceRegistration.Get<ILogger>().Debug("SubtitleRenderer: Subtitle queue too big, discarding first element");
            _subtitles.RemoveFirst();
          }
          _subtitles.AddLast(subtitle);

          ServiceRegistration.Get<ILogger>().Debug("SubtitleRenderer: Text subtitle added, now have {0} subtitles in cache {1} pos on last render was {2}", _subtitles.Count, subtitle, _currentTime);
        }
      }
      catch (Exception e)
      {
        ServiceRegistration.Get<ILogger>().Error("Problem processing text subtitle");
        ServiceRegistration.Get<ILogger>().Error(e);
      }
    }

    #endregion

    #region Filter handling

    /// <summary>
    /// Adds the subtitle filter to the graph. The caller need to call <see cref="Marshal.ReleaseComObject"/> on the
    /// returned instance when done.
    /// </summary>
    /// <param name="graphBuilder">The IGraphBuilder</param>
    /// <returns>DvbSub2(3) filter instance</returns>
    public IBaseFilter AddSubtitleFilter(IGraphBuilder graphBuilder)
    {
      try
      {
        _filter = FilterLoader.LoadFilterFromDll("DVBSub3.ax", CLSID_DVBSUB3, true);
        _subFilter = _filter as IDVBSubtitleSource;
        ServiceRegistration.Get<ILogger>().Debug("SubtitleRenderer: CreateFilter success: " + (_filter != null) + " & " + (_subFilter != null));
      }
      catch (Exception e)
      {
        ServiceRegistration.Get<ILogger>().Error(e);
      }
      if (_subFilter != null)
      {
        graphBuilder.AddFilter(_filter, "MediaPortal DVBSub3");
        _subFilter.StatusTest(111);
        _callBack = OnSubtitle;

        IntPtr pCallback = Marshal.GetFunctionPointerForDelegate(_callBack);
        _subFilter.SetBitmapCallback(pCallback);

        _subFilter.StatusTest(222);

        IntPtr pResetCallBack = Marshal.GetFunctionPointerForDelegate(_resetCallBack);
        _subFilter.SetResetCallback(pResetCallBack);

        IntPtr pUpdateTimeoutCallBack = Marshal.GetFunctionPointerForDelegate(_updateTimeoutCallBack);
        _subFilter.SetUpdateTimeoutCallback(pUpdateTimeoutCallBack);
      }
      return _filter;
    }

    protected virtual void EnableSubtitleHandling()
    {
      lock (_syncObj)
      {
        _renderSubtitles = true;
        if (_subtitleSyncThread == null)
        {
          _subtitleSyncThread = new Thread(SubtitleSync) { Name = "SubtitleSync", IsBackground = true, Priority = ThreadPriority.BelowNormal };
          _subtitleSyncThread.Start();
        }
        _useBitmap = true;
      }
    }

    /// <summary>
    /// <see cref="SubtitleSync"/> runs in a separated thread if <see cref="RenderSubtitles"/> is set to <c>true</c>. It watches the active player
    /// position and sets the subtitle that is to be shown.
    /// </summary>
    protected void SubtitleSync()
    {
      for (; ; )
      {
        bool enabled;
        lock (_syncObj)
          enabled = _renderSubtitles;

        if (!enabled)
          break;

        SetMatchingSubTitle();
        Thread.Sleep(20);
      }
    }


    protected virtual void DisableSubtitleHandling()
    {
      Thread subSyncThread;
      lock (_syncObj)
      {
        _renderSubtitles = false;
        subSyncThread = _subtitleSyncThread;
      }

      if (subSyncThread != null)
        subSyncThread.Join(1000);

      lock (_syncObj)
      {
        _activeSubPage = -1;
        _useBitmap = false;
        _clearOnNextRender = true;
        _subtitleSyncThread = null;
        SetMatchingSubTitle();
      }
    }

    protected virtual Subtitle ToSubtitle(IntPtr nativeSubPtr)
    {
      NativeSubtitle nativeSub = (NativeSubtitle)Marshal.PtrToStructure(nativeSubPtr, typeof(NativeSubtitle));
      PixelFormat format = new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied);
      BitmapProperties1 properties = new BitmapProperties1(format);
      Subtitle subtitle = new Subtitle
      {
        SubTexture = new Bitmap1(GraphicsDevice11.Instance.Context2D1, new Size2(nativeSub.Width, nativeSub.Height), properties),
        TimeOut = nativeSub.TimeOut,
        PresentTime = ((double)nativeSub.TimeStamp / 1000.0f) + _startPos,
        Height = (uint)nativeSub.Height,
        Width = (uint)nativeSub.Width,
        ScreenWidth = nativeSub.ScreenWidth,
        FirstScanLine = nativeSub.FirstScanLine,
        HorizontalPosition = nativeSub.HorizontalPosition,
        Id = _subCounter++
      };
      subtitle.SubTexture.CopyFromMemory(nativeSub.Bits, nativeSub.WidthBytes);
      //subtitle.SubTexture.Save("subtitle" + (si++) + ".png");
      return subtitle;
    }
    //private static int si = 0;

    #endregion

    #region Subtitle rendering

    public void DrawOverlay(IBitmapAsset2D targetSurface)
    {
      Subtitle currentSubtitle;
      Bitmap1 subTexture;
      lock (_syncObj)
      {
        currentSubtitle = _subtitles.ToList().FirstOrDefault(s => s.ShouldDraw);
        if (currentSubtitle == null)
          return;
        subTexture = currentSubtitle.SubTexture;
        if (targetSurface == null || !targetSurface.IsAllocated || subTexture == null || subTexture.IsDisposed)
        {
          if (_drawCount > 0)
            ServiceRegistration.Get<ILogger>().Debug("Draw count for last sub: {0}", _drawCount);
          _drawCount = 0;
          return;
        }
        _drawCount++;
      }

      try
      {
        // Check the target texture dimensions and adjust scaling and translation
        var desc = targetSurface.Bitmap.Size;
        Matrix transform = Matrix.Identity;
        transform *= Matrix.Translation(currentSubtitle.HorizontalPosition, currentSubtitle.FirstScanLine, 0);

        // TODO: Check scaling requirements for SD and HD sources
        // Subtitle could be smaller for 16:9 anamorphic video (subtitle width: 720, video texture: 1024)
        // then we need to scale the subtitle width also.
        if (currentSubtitle.ScreenWidth != desc.Width)
          transform *= Matrix.Scaling((float)desc.Width / currentSubtitle.ScreenWidth, 1, 1);

        using (new TemporaryRenderTarget2D(targetSurface.Bitmap))
        {
          GraphicsDevice11.Instance.Context2D1.Transform = (Matrix3x2)transform;
          GraphicsDevice11.Instance.Context2D1.DrawBitmap(subTexture, 1f, InterpolationMode.Linear);
          GraphicsDevice11.Instance.Context2D1.Flush();
        }
        if (_onTextureInvalidated != null)
          _onTextureInvalidated();
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Debug("Error in DrawOverlay", ex);
      }
    }

    public static Bitmap1 RenderText(LineContent[] lc)
    {
      if (lc.Length == 0)
        return null;

      // Concat all text into one block and let DirectWrite do the layouting
      string textBlock = lc.Select(l => l.line).Aggregate((a, b) => string.Format("{0}\r\n{1}", a, b));

      using (SolidColorBrush shadowBrush = new SolidColorBrush(GraphicsDevice11.Instance.Context2D1, Color.Black))
      using (SolidColorBrush textBrush = new SolidColorBrush(GraphicsDevice11.Instance.Context2D1, Color.White))
      {
        using (var format = new TextFormat(GraphicsDevice11.Instance.FactoryDW, "Calibri", FontWeight.Bold, FontStyle.Normal, lc[0].doubleHeight ? 48 : 36))
        using (var layout = new TextLayout(GraphicsDevice11.Instance.FactoryDW, textBlock, format, GraphicsDevice11.Instance.BackBuffer.Description.Width, GraphicsDevice11.Instance.BackBuffer.Description.Height))
        {
          Size2 size = new Size2((int)layout.Metrics.Width, (int)layout.Metrics.Height);
          BitmapProperties1 props = new BitmapProperties1(GraphicsDevice11.Instance.Context2D1.PixelFormat) { BitmapOptions = BitmapOptions.Target };
          Bitmap1 bmp = new Bitmap1(GraphicsDevice11.Instance.Context2D1, size, props);
          using (new TemporaryRenderTarget2D(bmp))
          {
            GraphicsDevice11.Instance.Context2D1.DrawTextLayout(new Vector2(1, 0), layout, shadowBrush);
            GraphicsDevice11.Instance.Context2D1.DrawTextLayout(new Vector2(0, 1), layout, shadowBrush);
            GraphicsDevice11.Instance.Context2D1.DrawTextLayout(new Vector2(-1, 0), layout, shadowBrush);
            GraphicsDevice11.Instance.Context2D1.DrawTextLayout(new Vector2(0, -1), layout, shadowBrush);
            GraphicsDevice11.Instance.Context2D1.DrawTextLayout(Vector2.Zero, layout, textBrush);
            return bmp;
          }
        }
      }
    }

    #endregion

    #region Subtitle queue handling

    private void SetMatchingSubTitle()
    {
      if (_player == null)
        return;

      double currentTime = _player.CurrentTime.TotalSeconds;
      lock (_syncObj)
      {
        if (_clearOnNextRender)
        {
          _clearOnNextRender = false;
          _subtitles.ToList().ForEach(s => s.Dispose());
          _subtitles.Clear();
        }

        if (_renderSubtitles == false)
          return;

        bool shouldOneDraw = false;
        // Enumerate from back of list, later subtitles will remove former
        foreach (Subtitle subtitle in _subtitles.Reverse())
        {
          subtitle.ShouldDraw = !shouldOneDraw && subtitle.PresentTime <= currentTime && currentTime <= subtitle.PresentTime + subtitle.TimeOut;
          if (subtitle.ShouldDraw)
          {
            shouldOneDraw = true;
          }
        }

        // Remove overdue subs
        _subtitles
          .Where(subtitle => subtitle.PresentTime + subtitle.TimeOut <= currentTime)
          .ToList()
          .ForEach(subtitle =>
          {
            subtitle.Dispose();
            _subtitles.Remove(subtitle);
          });
      }
    }

    /// <summary>
    /// Alerts the subtitle render that a reset has just been performed.
    /// Stops displaying the current subtitle and removes any cached subtitles.
    /// </summary>
    /// <returns></returns>
    public int Reset()
    {
      ServiceRegistration.Get<ILogger>().Debug("SubtitleRenderer: RESET");
      // Remove all previously received subtitles
      lock (_syncObj)
      {
        _subtitles.ToList().ForEach(s => s.Dispose());
        _subtitles.Clear();
        _clearOnNextRender = true;
      }
      return 0;
    }

    #endregion

    #region IDisposable Member

    public void Dispose()
    {
      lock (_subtitles)
      {
        _subtitles.ToList().ForEach(s => s.Dispose());
        _subtitles.Clear();
      }
      DisableSubtitleHandling();
    }

    #endregion
  }
}

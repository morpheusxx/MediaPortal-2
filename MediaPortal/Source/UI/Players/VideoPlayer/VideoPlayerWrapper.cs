//using System;
//using System.Drawing;
//using MediaPortal.Common.ResourceAccess;
//using MediaPortal.UI.Players.Video.Interfaces;
//using MediaPortal.UI.Players.Video.Tools;
//using MediaPortal.UI.Presentation.Geometries;
//using MediaPortal.UI.Presentation.Players;
//using MediaPortal.UI.SkinEngine.Players;
//using MediaPortal.UI.SkinEngine.SkinManagement;
//using SharpDX.Direct3D9;
//using Rectangle = SharpDX.Rectangle;

//namespace MediaPortal.UI.Players.Video
//{
//  public class VideoPlayerWrapper<T>
//    : IDisposable, IPlayerEvents, IInitializablePlayer, IMediaPlaybackControl, ISharpDXVideoPlayer, ISubtitlePlayer, IChapterPlayer, ITitlePlayer, IResumablePlayer
//     where T : VideoPlayer, new()
//  {
//    private T _videoPlayer;

//    public delegate void SetMediaItemDelegate(IResourceLocator locator, string mediaItemTitle);
//    public delegate TimeSpan TsDlg();

//    public VideoPlayerWrapper()
//    {
//      _videoPlayer = new T();
//    }

//    public virtual string Name { get { return _videoPlayer.Name; } }
//    public virtual PlayerState State { get { return _videoPlayer.State; } }
//    public virtual string MediaItemTitle { get { return _videoPlayer.MediaItemTitle; } }
//    public virtual void Stop()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.Stop());
//    }

//    public virtual void Dispose()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.Dispose());
//    }

//    public virtual void InitializePlayerEvents(PlayerEventDlgt started, PlayerEventDlgt stateReady, PlayerEventDlgt stopped, PlayerEventDlgt ended, PlayerEventDlgt playbackStateChanged, PlayerEventDlgt playbackError)
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.InitializePlayerEvents(started, stateReady, stopped, ended, playbackStateChanged, playbackError));
//    }

//    public virtual void ResetPlayerEvents()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.ResetPlayerEvents());
//    }

//    public virtual void SetMediaItem(IResourceLocator locator, string mediaItemTitle)
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.SetMediaItem(locator, mediaItemTitle));
//    }

//    public virtual TimeSpan CurrentTime
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.CurrentTime); }
//      set
//      {
//        _videoPlayer.CurrentTime = value;
//        //SkinContext.Form.InvokeIfRequired(c => _videoPlayer.CurrentTime = value);
//      }
//    }
//    public virtual TimeSpan Duration
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.Duration); }
//    }
//    public virtual double PlaybackRate
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.PlaybackRate); }
//    }
//    public virtual bool SetPlaybackRate(double value)
//    {
//      return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.SetPlaybackRate(value));
//    }

//    public virtual bool IsPlayingAtNormalRate { get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.IsPlayingAtNormalRate); } }
//    public virtual bool IsSeeking { get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.IsSeeking); } }
//    public virtual bool IsPaused { get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.IsPaused); } }
//    public virtual bool CanSeekForwards { get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.CanSeekForwards); } }
//    public virtual bool CanSeekBackwards { get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.CanSeekBackwards); } }
//    public virtual void Pause()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.Pause());
//    }

//    public virtual void Resume()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.Resume());
//    }

//    public virtual void Restart()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.Restart());
//    }

//    public virtual int Volume { get; set; }
//    public virtual bool Mute { get; set; }
//    public virtual Size VideoSize { get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.VideoSize); } }
//    public virtual SizeF VideoAspectRatio { get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.VideoAspectRatio); } }
//    public virtual IGeometry GeometryOverride
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.GeometryOverride); }
//      set { SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.GeometryOverride = value); }
//    }
//    public virtual string EffectOverride
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.EffectOverride); }
//      set { SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.EffectOverride = value); }
//    }
//    public virtual CropSettings CropSettings
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.CropSettings); }
//      set { SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.CropSettings = value); }
//    }
//    public virtual string[] AudioStreams
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.AudioStreams); }
//    }
//    public virtual void SetAudioStream(string audioStream)
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.SetAudioStream(audioStream));
//    }
//    public virtual string CurrentAudioStream
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.CurrentAudioStream); }
//    }
//    public virtual bool SetRenderDelegate(SkinEngine.Players.RenderDlgt dlgt)
//    {
//      return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.SetRenderDelegate(dlgt));
//    }

//    public virtual Texture Texture
//    {
//      get
//      {
//        return _videoPlayer.Texture;
//        //return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.Texture);
//      }
//    }

//    public virtual Rectangle CropVideoRect
//    {
//      get
//      {
//        return _videoPlayer.CropVideoRect;
//        //return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.CropVideoRect);
//      }
//    }

//    public virtual object SurfaceLock
//    {
//      get
//      {
//        return _videoPlayer.SurfaceLock;
//      }
//    }

//    public virtual void ReleaseGUIResources()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.ReleaseGUIResources());
//    }

//    public virtual void ReallocGUIResources()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.ReallocGUIResources());
//    }

//    public virtual string[] Subtitles
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.Subtitles); }
//    }
//    public virtual void SetSubtitle(string subtitle)
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.SetSubtitle(subtitle));
//    }

//    public virtual void DisableSubtitle()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.DisableSubtitle());
//    }

//    public virtual string CurrentSubtitle
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.CurrentSubtitle); }
//    }

//    public virtual string[] Chapters
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.Chapters); }
//    }
//    public void SetChapter(string chapter)
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.SetChapter(chapter));
//    }

//    public virtual bool ChaptersAvailable
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.ChaptersAvailable); }
//    }
//    public virtual void NextChapter()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.NextChapter());
//    }

//    public virtual void PrevChapter()
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.PrevChapter());
//    }

//    public virtual string CurrentChapter
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.CurrentChapter); }
//    }
//    public virtual string[] Titles
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.Titles); }
//    }
//    public virtual void SetTitle(string title)
//    {
//      SkinContext.Form.InvokeIfRequired(c => _videoPlayer.SetTitle(title));
//    }

//    public virtual string CurrentTitle
//    {
//      get { return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.CurrentTitle); }
//    }
//    public virtual bool GetResumeState(out IResumeState state)
//    {
//      IResumeState tmp = null;
//      var res = SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.GetResumeState(out tmp));
//      state = tmp;
//      return res;
//    }

//    public virtual bool SetResumeState(IResumeState state)
//    {
//      return _videoPlayer.SetResumeState(state);
//      //return SkinContext.Form.InvokeIfRequired2(c => _videoPlayer.SetResumeState(state));
//    }
//  }
//}

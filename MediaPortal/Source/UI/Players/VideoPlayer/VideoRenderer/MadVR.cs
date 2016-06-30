using System;
using System.Runtime.InteropServices;
using DirectShow;
using MediaPortal.UI.Players.Video.Tools;
using MediaPortal.UI.SkinEngine.SkinManagement;

namespace MediaPortal.UI.Players.Video.VideoRenderer
{
  public class MadVR : IVideoRenderer
  {
    // DirectShow objects
    protected IBaseFilter _evr;
    protected EVRCallback _evrCallback;
    protected IntPtr _presenterInstance;
    protected IBasicVideo _basicVideo;
    protected IVideoWindow _videoWindow;

    protected const string MADVR_FILTER_NAME = "madVR";

    #region Classes & interfaces

    [ComImport, Guid("E1A8B82A-32CE-4B0D-BE0D-AA68C772E423")]
    public class MadVideoRenderer { }

    [ComImport, Guid("51B4ABF3-748F-4E3B-A276-C828330E926A")]
    public class Vmr9VideoRenderer { }

    #endregion


    public void Dispose()
    {
      ReleaseVideoRenderer();
    }

    private void ReleaseVideoRenderer()
    {
      if (_videoWindow != null)
      {
        _videoWindow.put_Owner(IntPtr.Zero);
        _videoWindow.put_MessageDrain(IntPtr.Zero);
        _videoWindow.put_Visible(0);
        _videoWindow = null;
      }
      _basicVideo = null;
      FilterGraphTools.TryRelease(ref _evr);
    }

    public void AddToGraph(IGraphBuilder graphBuilder, uint streamCount)
    {
      _evr = (IBaseFilter)new MadVideoRenderer();
      _basicVideo = (IBasicVideo)_evr;
      _videoWindow = (IVideoWindow)_evr;

      graphBuilder.AddFilter(_evr, MADVR_FILTER_NAME);
    }

    public bool SyncRendering { get { return false; } }
    public IBaseFilter Filter { get { return _evr; } }
    public void OnGraphRunning()
    {
      if (_basicVideo != null)
      {
        //_basicVideo.SetSourcePosition(0, 0, 1280, 720);
        _basicVideo.SetDestinationPosition(0, 0, SkinContext.BackBufferWidth, SkinContext.BackBufferHeight);
      }

      if (_videoWindow != null)
      {
        _videoWindow.put_Visible(1);
        _videoWindow.put_Owner(SkinContext.Form.Handle);
        _videoWindow.put_MessageDrain(SkinContext.Form.Handle);
        _videoWindow.put_WindowStyle((int)(0x40000000L /*WindowStyle.Child*/| 0x04000000L /*WindowStyle.ClipSiblings*/| 0x02000000L /*WindowStyle.ClipChildren*/));
        _videoWindow.SetWindowPosition(0, 0, SkinContext.BackBufferWidth, SkinContext.BackBufferHeight);
      }
    }
  }
}

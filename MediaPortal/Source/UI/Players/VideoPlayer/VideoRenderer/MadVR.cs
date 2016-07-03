using System;
using System.Runtime.InteropServices;
using DirectShow;
using DirectShow.Helper;
using MediaPortal.UI.Players.Video.Tools;
using MediaPortal.UI.SkinEngine.Players;
using MediaPortal.UI.SkinEngine.SkinManagement;

namespace MediaPortal.UI.Players.Video.VideoRenderer
{
  public class MadVR : IVideoRenderer, IOverlayRenderer
  {
    // DirectShow objects
    protected DSFilter _evr;
    protected IBasicVideo _basicVideo;
    protected IVideoWindow _videoWindow;

    protected const string MADVR_FILTER_NAME = "madVR";

    #region Classes & interfaces

    [Guid("E1A8B82A-32CE-4B0D-BE0D-AA68C772E423")]
    public class MadVideoRenderer : DSFilter { }

    [Guid("51B4ABF3-748F-4E3B-A276-C828330E926A")]
    public class Vmr9VideoRenderer : DSFilter { }

    #endregion


    public void Dispose()
    {
      ReleaseVideoRenderer();
    }

    private void ReleaseVideoRenderer()
    {
      var videoWindow = _videoWindow;
      if (videoWindow != null)
      {
        videoWindow.put_Owner(IntPtr.Zero);
        videoWindow.put_MessageDrain(IntPtr.Zero);
        videoWindow.put_Visible(0);
      }
      FilterGraphTools.TryFinalRelease(ref _basicVideo);
      FilterGraphTools.TryFinalRelease(ref _videoWindow);
      FilterGraphTools.TryDispose(ref _evr);
    }

    public void AddToGraph(IGraphBuilder graphBuilder, uint streamCount)
    {
      _evr = new MadVideoRenderer();
      //_evr = new Vmr9VideoRenderer();
      _basicVideo = (IBasicVideo)_evr.Value;
      _videoWindow = (IVideoWindow)_evr.Value;

      _evr.FilterGraph = graphBuilder;
    }

    public bool SyncRendering { get { return false; } }
    public IBaseFilter Filter { get { return _evr.Value; } }

    public void OnGraphRunning()
    {
      var videoWindow = _videoWindow;
      if (videoWindow != null)
      {
        videoWindow.put_Owner(SkinContext.Form.Handle);
        videoWindow.put_MessageDrain(SkinContext.Form.Handle);
        videoWindow.put_WindowStyle((int)(0x40000000L /*WindowStyle.Child*/| 0x04000000L /*WindowStyle.ClipSiblings*/| 0x02000000L /*WindowStyle.ClipChildren*/));
      }
    }

    public void SetOverlayPosition(int left, int top, int width, int height)
    {
      bool isVisible = width != 0 && height != 0;
      if (_videoWindow == null || _basicVideo == null)
        return;
      var videoWindow = _videoWindow;
      videoWindow.put_Visible(isVisible ? 1 : 0);
      if (!isVisible)
        return;
      _basicVideo.SetDestinationPosition(left, top, width, height);
      videoWindow.SetWindowPosition(left, top, width, height);
    }
  }
}

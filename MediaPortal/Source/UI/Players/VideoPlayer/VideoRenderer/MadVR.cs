using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DirectShow;
using DirectShow.Helper;
using MediaPortal.Common;
using MediaPortal.Common.Async;
using MediaPortal.Common.Logging;
using MediaPortal.UI.Players.Video.Tools;
using MediaPortal.UI.SkinEngine.Players;
using MediaPortal.UI.SkinEngine.SkinManagement;

namespace MediaPortal.UI.Players.Video.VideoRenderer
{
  public class MadVR : IVideoRenderer, IOverlayRenderer
  {
    // DirectShow objects
    protected MadVideoRenderer _madVR;
    protected IBaseFilter _rendererFilter;
    protected IBasicVideo _basicVideo;
    protected IVideoWindow _videoWindow;
    protected volatile bool _shutdown;

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
      _shutdown = true;
      var videoWindow = _videoWindow;
      if (videoWindow != null)
      {
        videoWindow.put_Owner(IntPtr.Zero);
        videoWindow.put_MessageDrain(IntPtr.Zero);
        videoWindow.put_Visible(0);
      }
      ServiceRegistration.Get<ILogger>().Info("Disposing _madVR");
      FilterGraphTools.TryDispose(ref _madVR);
    }

    public async void AddToGraph(IGraphBuilder graphBuilder, uint streamCount)
    {
      await SkinContext.Form;
      _madVR = new MadVideoRenderer();
      _rendererFilter = _madVR.Value;
      _basicVideo = (IBasicVideo)_rendererFilter;
      _videoWindow = (IVideoWindow)graphBuilder;
      graphBuilder.AddFilter(_rendererFilter, MADVR_FILTER_NAME);
    }

    public bool SyncRendering { get { return false; } }
    public IBaseFilter Filter { get { return _rendererFilter; } }

    public async void OnGraphRunning()
    {
      await SkinContext.Form;
      var videoWindow = _videoWindow;
      if (videoWindow != null)
      {
        videoWindow.put_Owner(SkinContext.Form.Handle);
        videoWindow.put_MessageDrain(SkinContext.Form.Handle);
        videoWindow.put_WindowStyle((int)(0x40000000L /*WindowStyle.Child*/| 0x04000000L /*WindowStyle.ClipSiblings*/| 0x02000000L /*WindowStyle.ClipChildren*/));
      }
    }

    public async Task SetOverlayPositionAsync(int left, int top, int width, int height)
    {
      await SkinContext.Form;

      bool isVisible = width != 0 && height != 0;
      if (_videoWindow == null || _basicVideo == null || _shutdown)
        return;
      if (isVisible)
      {
        _basicVideo.SetDestinationPosition(left, top, width, height);
        _videoWindow.SetWindowPosition(left, top, width, height);
      }
      _videoWindow.put_Visible(isVisible ? 1 : 0);
    }
  }
}

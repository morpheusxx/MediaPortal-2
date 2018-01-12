using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DirectShow;
using DirectShow.Helper;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.UI.Players.Video.Tools;
using MediaPortal.UI.SkinEngine.Players;
using MediaPortal.UI.SkinEngine.SkinManagement;

namespace MediaPortal.UI.Players.Video.VideoRenderer
{
  public class MadVR : IVideoRenderer, IOverlayRenderer
  {
    // DirectShow objects
    protected IBaseFilter _evr;
    protected IBasicVideo _basicVideo;
    protected IVideoWindow _videoWindow;
    protected volatile bool _shutdown;

    protected const string MADVR_FILTER_NAME = "madVR";

    #region Classes & interfaces

    [ComImport, Guid("E1A8B82A-32CE-4B0D-BE0D-AA68C772E423")]
    public class MadVideoRenderer { }

    //[Guid("E1A8B82A-32CE-4B0D-BE0D-AA68C772E423")]
    //public class MadVideoRenderer : DSFilter { }

    [Guid("51B4ABF3-748F-4E3B-A276-C828330E926A")]
    public class Vmr9VideoRenderer : DSFilter { }

    [DllImport("EVRPresenter.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int CreateMadVR(out IBaseFilter presenterInstance);
    [DllImport("EVRPresenter.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int ReleaseMadVR(IBaseFilter presenterInstance);

    #endregion


    public void Dispose()
    {
      ReleaseVideoRenderer();
    }

    private void ReleaseVideoRenderer()
    {
      _shutdown = true;
      ReleaseMadVR(_evr);
      var videoWindow = _videoWindow;
      if (videoWindow != null)
      {
        videoWindow.put_Owner(IntPtr.Zero);
        videoWindow.put_MessageDrain(IntPtr.Zero);
        videoWindow.put_Visible(0);
      }
      ServiceRegistration.Get<ILogger>().Info("Releasing _basicVideo");
      FilterGraphTools.TryFinalRelease(ref _basicVideo);
      ServiceRegistration.Get<ILogger>().Info("Releasing _videoWindow");
      FilterGraphTools.TryFinalRelease(ref _videoWindow);
      ServiceRegistration.Get<ILogger>().Info("Final Releasing _evr");
      FilterGraphTools.TryFinalRelease(ref _evr);
      //FilterGraphTools.TryFinalRelease(ref _basicVideo);
      //FilterGraphTools.TryFinalRelease(ref _videoWindow);
      //FilterGraphTools.TryDispose(ref _evr);
    }

    public void AddToGraph(IGraphBuilder graphBuilder, uint streamCount)
    {
      //_evr = (IBaseFilter)new MadVideoRenderer();
      int hr = CreateMadVR(out _evr);
      //_evr = new Vmr9VideoRenderer();
      _basicVideo = (IBasicVideo)_evr;
      _videoWindow = (IVideoWindow)_evr;
      graphBuilder.AddFilter(_evr, MADVR_FILTER_NAME);
      //Marshal.ReleaseComObject(_videoWindow);
      //Marshal.ReleaseComObject(_basicVideo);
      //Marshal.ReleaseComObject(_evr);
    }

    public bool SyncRendering { get { return false; } }
    public IBaseFilter Filter { get { return _evr; } }

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
      _ = SetOverlayPositionAsync(left, top, width, height);
    }
    public async Task SetOverlayPositionAsync(int left, int top, int width, int height)
    {
      await Task.Yield();

      bool isVisible = width != 0 && height != 0;
      if (_videoWindow == null || _basicVideo == null || _shutdown)
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

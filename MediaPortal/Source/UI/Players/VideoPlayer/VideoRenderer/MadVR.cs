using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
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
    protected IBaseFilter _rendererFilter;
    protected MVRCallback _mvrCallback;
    protected IBasicVideo _basicVideo;
    protected IVideoWindow _videoWindow;
    protected volatile bool _shutdown;

    #region DLL imports

    [DllImport("EVRPresenter.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int MadVRInit(IMVRPresentCallback callback, int xposition, int yposition, int width, int height, IntPtr dwD3DDevice, IntPtr parent, ref IBaseFilter madFilter, IGraphBuilder pMediaControl);

    [DllImport("EVRPresenter.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void MadVRDeinit();

    #endregion

    public MadVR()
    {
      _mvrCallback = new MVRCallback();
    }

    public void Dispose()
    {
      ReleaseVideoRenderer();
    }

    private void ReleaseVideoRenderer()
    {
      _shutdown = true;
      ServiceRegistration.Get<ILogger>().Info("Disposing madVR");

      SkinContext.Form.Invoke((Action)delegate { MadVRDeinit(); });
      GC.Collect();

      _videoWindow = null;
      _basicVideo = null;
      _rendererFilter = null;
    }

    public void AddToGraph(IGraphBuilder graphBuilder, uint streamCount)
    {
      int hr = -1;
      IntPtr upDevice = SkinContext.Device.NativePointer;
      //Must be initialized synchroniously on gui thread
      SkinContext.Form.Invoke((Action)delegate
      {
        hr = MadVRInit(_mvrCallback, 0, 0, SkinContext.Form.Width, SkinContext.Form.Height, upDevice, SkinContext.Form.Handle, ref _rendererFilter, graphBuilder);
      });
      if (hr != 0)
        throw new VideoPlayerException("Initializing of madVR failed");

      _basicVideo = (IBasicVideo)_rendererFilter;
      _videoWindow = (IVideoWindow)graphBuilder;
    }

    public bool SyncRendering { get { return false; } }
    public IBaseFilter Filter { get { return _rendererFilter; } }

    public void OnGraphRunning()
    {
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

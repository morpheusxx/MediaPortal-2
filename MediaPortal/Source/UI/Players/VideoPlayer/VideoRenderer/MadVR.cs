using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
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
    protected MVRCallback _evrCallback;
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
      _evrCallback = new MVRCallback();
    }


    public void Dispose()
    {
      try
      {
        if (_evr != null)
        {
          //FilterGraphTools.DisconnectPins(_evr);

          Marshal.ReleaseComObject(_videoWindow);
          Marshal.ReleaseComObject(_basicVideo);
          //Marshal.ReleaseComObject(_evr);
        }
      }
      catch (Exception ex)
      {
      }
      _videoWindow = null;
      _basicVideo = null;
      _evr = null;
      GC.Collect();

      SkinContext.Form.Invoke(new Action(() =>
      {
        MadVRDeinit();
      }));
    }

    public void AddToGraph(IGraphBuilder graphBuilder, uint streamCount)
    {
      IntPtr upDevice = SkinContext.Device.NativePointer;

      SkinContext.Form.BeginInvoke(new Action(() =>
      {
        int hr = MadVRInit(_evrCallback, 0, 0, SkinContext.Form.Width, SkinContext.Form.Height, upDevice, SkinContext.Form.Handle, ref _evr, graphBuilder);
        if (hr != 0)
          throw new VideoPlayerException("Initializing of MadVR failed");

        //_evr = (IBaseFilter)new MadVideoRenderer();
        //_evr = new Vmr9VideoRenderer();
        _basicVideo = (IBasicVideo)_evr;
        _videoWindow = (IVideoWindow)_evr;
        //graphBuilder.AddFilter(_evr, MADVR_FILTER_NAME);
        //Marshal.ReleaseComObject(_videoWindow);
        //Marshal.ReleaseComObject(_basicVideo);
        //Marshal.ReleaseComObject(_evr);

        //var handle = SkinContext.Form.Handle;
        //_videoWindow.put_Owner(handle);
        ////_videoWindow.put_MessageDrain(handle);
        //_videoWindow.put_WindowStyle((int)(0x40000000L /*WindowStyle.Child*/| 0x04000000L /*WindowStyle.ClipSiblings*/| 0x02000000L /*WindowStyle.ClipChildren*/));
        //_videoWindow.put_Left(0);
        //_videoWindow.put_Top(0);
        //_videoWindow.put_Height(SkinContext.Form.Height / 2);
        //_videoWindow.put_Width(SkinContext.Form.Width);
      }));
    }

    public bool SyncRendering { get { return false; } }
    public IBaseFilter Filter { get { return _evr; } }

    public void OnGraphRunning()
    {
      //SkinContext.Form.BeginInvoke(new Action(() =>
      //{
      //  var videoWindow = _videoWindow;
      //  if (videoWindow != null)
      //  {
      //    var handle = SkinContext.Form.Handle;
      //    videoWindow.put_Owner(handle);
      //    videoWindow.put_MessageDrain(handle);
      //    videoWindow.put_WindowStyle((int)(0x40000000L /*WindowStyle.Child*/| 0x04000000L /*WindowStyle.ClipSiblings*/| 0x02000000L /*WindowStyle.ClipChildren*/));
      //    videoWindow.put_Left(0);
      //    videoWindow.put_Top(0);
      //    videoWindow.put_Height(SkinContext.Form.Height);
      //    videoWindow.put_Width(SkinContext.Form.Width);
      //  }
      //}));
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

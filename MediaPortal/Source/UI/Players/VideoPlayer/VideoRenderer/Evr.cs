using System;
using System.Runtime.InteropServices;
using System.Security;
using DirectShow;
using MediaPortal.Common;
using MediaPortal.Common.Settings;
using MediaPortal.UI.Players.Video.Settings;
using MediaPortal.UI.Players.Video.Tools;
using MediaPortal.UI.SkinEngine.SkinManagement;

namespace MediaPortal.UI.Players.Video.VideoRenderer
{
  public class Evr : IVideoRenderer, IEvrCallback
  {
    // DirectShow objects
    protected IBaseFilter _evr;
    protected EVRCallback _evrCallback;
    protected IntPtr _presenterInstance;

    protected const string EVR_FILTER_NAME = "Enhanced Video Renderer";

    [ComImport, Guid("fa10746c-9b63-4b6c-bc49-fc300ea5f256")]
    public class EnhancedVideoRenderer { }

    [ComImport, SuppressUnmanagedCodeSecurity,
     Guid("83E91E85-82C1-4ea7-801D-85DC50B75086"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEVRFilterConfig
    {
      int SetNumberOfStreams(uint dwMaxStreams);
      int GetNumberOfStreams(ref uint pdwMaxStreams);
    }

    #region DLL imports

    [DllImport("EVRPresenter.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int EvrInit(IEVRPresentCallback callback, uint dwD3DDevice, IBaseFilter evrFilter, IntPtr monitor, out IntPtr presenterInstance);

    [DllImport("EVRPresenter.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void EvrDeinit(IntPtr presenterInstance);

    #endregion

    public Evr(RenderDlgt renderFrameDlg, Action onTextureInvalidated, VideoSizePresentDlgt onVideoSizePresent)
    {
      _evrCallback = new EVRCallback(renderFrameDlg, onTextureInvalidated);
      _evrCallback.VideoSizePresent += onVideoSizePresent;
    }

    public void Dispose()
    {
      FilterGraphTools.TryFinalRelease(ref _evr);
      if (_presenterInstance != IntPtr.Zero)
        EvrDeinit(_presenterInstance);

      _presenterInstance = IntPtr.Zero;
      if (_evrCallback != null)
        _evrCallback.Dispose();
      _evrCallback = null;
    }

    public void AddToGraph(IGraphBuilder graphBuilder, uint streamCount)
    {
      _evr = (IBaseFilter)new EnhancedVideoRenderer();

      IntPtr upDevice = SkinContext.Device.NativePointer;
      int hr = EvrInit(_evrCallback, (uint)upDevice.ToInt32(), _evr, SkinContext.Form.Handle, out _presenterInstance);
      if (hr != 0)
        throw new VideoPlayerException("Initializing of EVR failed");

      // Check if CC is enabled, in this case the EVR needs one more input pin
      VideoSettings settings = ServiceRegistration.Get<ISettingsManager>().Load<VideoSettings>();
      if (settings.EnableClosedCaption)
        streamCount++;

      // Set the number of video/subtitle/cc streams that are allowed to be connected to EVR. This has to be done after the custom presenter is initialized.
      IEVRFilterConfig config = (IEVRFilterConfig)_evr;
      config.SetNumberOfStreams(streamCount);

      graphBuilder.AddFilter(_evr, EVR_FILTER_NAME);
    }

    public bool SyncRendering { get { return true; } }
    public IBaseFilter Filter { get { return _evr; } }

    public void OnGraphRunning()
    {
    }

    public EVRCallback EvrCallback
    {
      get
      {
        return _evrCallback;
      }
    }
  }
}

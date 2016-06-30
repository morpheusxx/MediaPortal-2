using System;
using DirectShow;

namespace MediaPortal.UI.Players.Video.VideoRenderer
{
  public interface IVideoRenderer : IDisposable
  {
    void AddToGraph(IGraphBuilder graphBuilder, uint streamCount);
    bool SyncRendering { get; }
    IBaseFilter Filter { get; }
    void OnGraphRunning();
  }

  public interface IEvrCallback : IVideoRenderer
  {
    EVRCallback EvrCallback { get; }
  }
}

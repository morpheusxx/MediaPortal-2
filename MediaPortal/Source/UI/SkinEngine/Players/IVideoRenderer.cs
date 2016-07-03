namespace MediaPortal.UI.SkinEngine.Players
{
  public interface IOverlayPlayer
  {
    IOverlayRenderer Renderer { get; }
  }

  public interface IOverlayRenderer
  {
    void SetOverlayPosition(int left, int top, int width, int height);
  }
}

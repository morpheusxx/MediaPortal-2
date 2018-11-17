using System.Threading.Tasks;

namespace MediaPortal.UI.SkinEngine.Players
{
  public interface IOverlayPlayer
  {
    IOverlayRenderer Renderer { get; }
  }

  public interface IOverlayRenderer
  {
    Task SetOverlayPositionAsync(int left, int top, int width, int height);
  }
}

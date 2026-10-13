using MediaPortal.Common.MediaManagement;
using MediaPortal.UI.Presentation.Players;

namespace MediaPortal.UI.Players.Mpv
{
  /// <summary>
  /// Creates <see cref="MpvPlayer"/> instances for video media items.
  /// </summary>
  public class MpvPlayerBuilder : IPlayerBuilder
  {
    #region IPlayerBuilder implementation

    public IPlayer GetPlayer(MediaItem mediaItem)
    {
      // TODO: Phase 3.1: create the libmpv based player
      return null;
    }

    #endregion
  }
}

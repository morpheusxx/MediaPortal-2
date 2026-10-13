#region Copyright (C) 2007-2021 Team MediaPortal

/*
    Copyright (C) 2007-2021 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using MediaPortal.UI.Presentation.Players;
using SharpDX;
using MediaPortal.UI.SkinEngine.Gpu;

namespace MediaPortal.UI.SkinEngine.Players
{
  /// <summary>
  /// Interface which has to be implemented by video players which provide their video frames as texture to the SkinEngine.
  /// </summary>
  /// <remarks>
  /// A video player renders its output into a <see cref="Texture"/> (for example a GPU texture which is filled by libmpv), which is
  /// drawn by the SkinEngine as brush (see <c>VideoBrush</c>). The SkinEngine doesn't know anything about how the frames are decoded.
  /// </remarks>
  public interface IVideoFrameSource : IVideoPlayer
  {
    /// <summary>
    /// Returns the render texture for the current frame. May be <c>null</c>.
    /// </summary>
    Texture Texture { get; }

    /// <summary>
    /// Gets the rectangle out of the video frame <see cref="Texture"/> which should be presented.
    /// </summary>
    /// <remarks>
    /// Video players may adjust the final video frame according to the <see cref="IVideoPlayer.CropSettings"/> in the
    /// <see cref="Texture"/> property or they may simply return a bigger frame, returning the crop rectangle in this property.
    /// In that case, the SkinEngine will crop the <see cref="Texture"/> by this rectangle.
    /// </remarks>
    Rectangle CropVideoRect { get; }

    /// <summary>
    /// Returns a mutex object to lock while accessing the <see cref="Texture"/>.
    /// </summary>
    object SurfaceLock { get; }

    /// <summary>
    /// Releases any GUI resources. Called before the graphics device is released or reset.
    /// </summary>
    void ReleaseGUIResources();

    /// <summary>
    /// Reallocs any GUI resources. Called after the graphics device was created or reset.
    /// </summary>
    void ReallocGUIResources();
  }

  /// <summary>
  /// Extended support for additional texture planes which are drawn over the video frame.
  /// </summary>
  public interface IVideoFrameOverlays : IVideoFrameSource
  {
    /// <summary>
    /// Returns additional texture planes that will be overlayed over the original <see cref="IVideoFrameSource.Texture"/>.
    /// This can be used for OSD (like BluRay player) or subtitles.
    /// </summary>
    Texture[] TexturePlanes { get; }
  }
}

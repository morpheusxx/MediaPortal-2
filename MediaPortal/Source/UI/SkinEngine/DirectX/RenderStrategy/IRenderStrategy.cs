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

namespace MediaPortal.UI.SkinEngine.DirectX.RenderStrategy
{
  /// <summary>
  /// Strategy how frames are paced: waiting for the next frame in software, synchronized to the vertical blank or unlimited.
  /// </summary>
  public interface IRenderStrategy
  {
    string Name { get; }

    /// <summary>
    /// Returns <c>true</c> if buffer swaps are synchronized with the vertical blank of the display.
    /// </summary>
    bool PresentVSync { get; }

    void SetTargetFrameRate(double frameRate);

    double TargetFrameRate { get; }

    double MsPerFrame { get; }

    /// <summary>
    /// Called before the frame is rendered.
    /// </summary>
    /// <param name="doWaitForNextFame"><c>true</c>, if this method should wait for the correct frame start time.</param>
    void BeginRender(bool doWaitForNextFame);

    /// <summary>
    /// Called after the frame was rendered.
    /// </summary>
    void EndRender();
  }
}

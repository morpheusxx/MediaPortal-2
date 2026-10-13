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

using System;
using SkiaSharp;

namespace MediaPortal.UI.SkinEngine.Gpu
{
  /// <summary>
  /// Anti-aliasing mode of the back buffer. The member names are identical to the Direct3D multisample types so existing
  /// user settings stay valid.
  /// </summary>
  public enum MultisampleType
  {
    None = 0,
    NonMaskable = 1,
    TwoSamples = 2,
    ThreeSamples = 3,
    FourSamples = 4,
    FiveSamples = 5,
    SixSamples = 6,
    SevenSamples = 7,
    EightSamples = 8,
    NineSamples = 9,
    TenSamples = 10,
    ElevenSamples = 11,
    TwelveSamples = 12,
    ThirteenSamples = 13,
    FourteenSamples = 14,
    FifteenSamples = 15,
    SixteenSamples = 16
  }

  /// <summary>
  /// The window (or other output) the SkinEngine renders to. Implemented by the platform window host.
  /// An OpenGL context must be current in the render thread, the host swaps the buffers after the frame was rendered.
  /// </summary>
  internal interface IRenderHost
  {
    /// <summary>
    /// Creates the Skia GPU context for the current OpenGL context.
    /// </summary>
    GRContext CreateGrContext();

    /// <summary>
    /// Width of the framebuffer in pixels.
    /// </summary>
    int FramebufferWidth { get; }

    /// <summary>
    /// Height of the framebuffer in pixels.
    /// </summary>
    int FramebufferHeight { get; }

    /// <summary>
    /// Width of the desktop (monitor) the window is shown on.
    /// </summary>
    int DesktopWidth { get; }

    /// <summary>
    /// Height of the desktop (monitor) the window is shown on.
    /// </summary>
    int DesktopHeight { get; }

    /// <summary>
    /// Refresh rate of the monitor the window is shown on in Hz.
    /// </summary>
    double RefreshRate { get; }

    /// <summary>
    /// Description of the graphics adapter, for logging.
    /// </summary>
    string RendererDescription { get; }

    /// <summary>
    /// Enables or disables synchronization of the buffer swap with the vertical blank.
    /// </summary>
    bool VSync { set; }

    /// <summary>
    /// Gets the number of samples of the back buffer.
    /// </summary>
    int MultiSampleCount { get; }

    /// <summary>
    /// Returns <c>true</c> if the calling thread owns the OpenGL context.
    /// </summary>
    bool IsRenderThread { get; }

    /// <summary>
    /// Executes the given action in the render thread and waits for its completion.
    /// </summary>
    void Invoke(Action action);
  }
}

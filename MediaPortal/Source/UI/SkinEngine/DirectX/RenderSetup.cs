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

using MediaPortal.UI.SkinEngine.Gpu;

namespace MediaPortal.UI.SkinEngine.DirectX
{
  /// <summary>
  /// Information about the output the graphics device renders to. Replacement of the former Direct3D setup class.
  /// </summary>
  internal class RenderSetup
  {
    /// <summary>
    /// The window host.
    /// </summary>
    public IRenderHost Host { get; set; }

    public int MultiSampleCount
    {
      get { return Host == null ? 0 : Host.MultiSampleCount; }
    }

    public bool IsMultiSample
    {
      get { return MultiSampleCount > 1; }
    }

    public int DesktopWidth
    {
      get { return Host == null ? 1920 : Host.DesktopWidth; }
    }

    public int DesktopHeight
    {
      get { return Host == null ? 1080 : Host.DesktopHeight; }
    }

    public bool IsRenderThread
    {
      get { return Host != null && Host.IsRenderThread; }
    }
  }
}

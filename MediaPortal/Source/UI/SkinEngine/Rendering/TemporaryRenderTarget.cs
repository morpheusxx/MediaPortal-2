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
using MediaPortal.UI.SkinEngine.DirectX;
using MediaPortal.UI.SkinEngine.Gpu;

namespace MediaPortal.UI.SkinEngine.Rendering
{
  /// <summary>
  /// Switches the render target of the graphics device until this object is disposed.
  /// </summary>
  public class TemporaryRenderTarget : IDisposable
  {
    // The active render target before we change it
    private readonly Surface _previousTarget;

    // The target surface to render on
    private readonly Surface _targetSurface;

    public TemporaryRenderTarget(Surface targetSurface)
    {
      _previousTarget = GraphicsDevice.GetRenderTarget();
      _targetSurface = targetSurface;
      GraphicsDevice.SetRenderTarget(_targetSurface);
    }

    public TemporaryRenderTarget(Texture targetTexture)
      : this(targetTexture.GetSurfaceLevel(0))
    { }

    public void Dispose()
    {
      // Restore the previous render target
      GraphicsDevice.SetRenderTarget(_previousTarget);
      _targetSurface.MarkChanged();
    }
  }
}

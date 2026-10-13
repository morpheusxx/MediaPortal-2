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

namespace MediaPortal.UI.SkinEngine.Players
{
  /// <summary>
  /// Gives players which render their video frames with OpenGL (like libmpv) access to the render thread and the OpenGL context of the SkinEngine.
  /// </summary>
  public static class VideoRenderHost
  {
    /// <summary>
    /// Raised in the render thread at the beginning of each frame, before the SkinEngine draws. Video players render their new frame here.
    /// </summary>
    public static event EventHandler SceneBegin
    {
      add { GraphicsDevice.DeviceSceneBegin += value; }
      remove { GraphicsDevice.DeviceSceneBegin -= value; }
    }

    /// <summary>
    /// Raised in the render thread after a frame was rendered.
    /// </summary>
    public static event EventHandler ScenePresented
    {
      add { GraphicsDevice.DeviceScenePresented += value; }
      remove { GraphicsDevice.DeviceScenePresented -= value; }
    }

    /// <summary>
    /// Returns <c>true</c> if the calling thread owns the OpenGL context.
    /// </summary>
    public static bool IsRenderThread
    {
      get { return GraphicsDevice.IsRenderThread; }
    }

    /// <summary>
    /// Executes the given action in the render thread and waits for its completion. If the caller is the render thread, the action is executed directly.
    /// </summary>
    public static void Invoke(Action action)
    {
      GraphicsDevice.ExecuteInMainThread(() => action());
    }

    /// <summary>
    /// Gets the address of an OpenGL function of the context of the render thread. Returns <see cref="IntPtr.Zero"/> if the function doesn't exist.
    /// </summary>
    public static IntPtr GetGlProcAddress(string name)
    {
      return GraphicsDevice.GetGlProcAddress(name);
    }

    /// <summary>
    /// Has to be called after a video player changed the OpenGL state, before the SkinEngine continues drawing.
    /// </summary>
    public static void ResetGlState()
    {
      GraphicsDevice.ResetGlState();
    }
  }
}

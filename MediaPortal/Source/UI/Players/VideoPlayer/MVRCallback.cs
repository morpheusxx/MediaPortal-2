#region Copyright (C) 2007-2017 Team MediaPortal

/*
    Copyright (C) 2007-2017 Team MediaPortal
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

/****************************************************************************
While the underlying libraries are covered by LGPL, this sample is released 
as public domain.  It is distributed in the hope that it will be useful, but 
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY 
or FITNESS FOR A PARTICULAR PURPOSE.  
*****************************************************************************/

using System;
using System.Runtime.InteropServices;
using MediaPortal.UI.Players.Video.Tools;
using SharpDX.Direct3D9;
using Size = SharpDX.Size2;
using SizeF = SharpDX.Size2F;
using PointF = SharpDX.Vector2;
using DirectShow;
using System.Drawing;
using System.Threading;

namespace MediaPortal.UI.Players.Video
{
  /// <summary>
  /// General helper class to add the Video Mixing Render9 filter to a graph
  /// , set it to renderless mode and provide it our own allocator/presentor
  /// This will allow us to render the video to a direct3d texture
  /// which we can use to draw the transparent OSD on top of it
  /// Some classes which work together:
  ///  VMR9Util								: general helper class
  ///  AllocatorWrapper.cs		: implements our own allocator/presentor for vmr9 by implementing
  ///                           IVMRSurfaceAllocator9 and IVMRImagePresenter9
  ///  PlaneScene.cs          : class which draws the video texture onscreen and mixes it with the GUI, OSD,...                          
  /// </summary>
  /// // {324FAA1F-7DA6-4778-833B-3993D8FF4151}

  #region IVMR9PresentCallback interface

  [ComVisible(true), ComImport,
   Guid("B13987BA-76C8-4521-B9B8-7C00C443AB30"),
   InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  public interface IMVRPresentCallback
  {
    [PreserveSig]
    void SetRenderTarget(IntPtr target);

    [PreserveSig]
    void SetSubtitleDevice(IntPtr device);

    [PreserveSig]
    void RenderSubtitle(long frameStart, Rectangle viewportRect, Rectangle croppedVideoRect, int xOffsetInPixels);

    [PreserveSig]
    void RenderFrame(Int16 cx, Int16 cy, Int16 arx, Int16 ary, IntPtr pSurface);

    [PreserveSig]
    void ForceOsdUpdate(bool pForce);

    [PreserveSig]
    void RestoreDeviceSurface(IntPtr pSurfaceDevice);
  }

  [ComVisible(true)]
  [ClassInterface(ClassInterfaceType.None)]
  public class MVRCallback : IMVRPresentCallback, IDisposable
  {
    public void SetRenderTarget(IntPtr target)
    {
      
    }

    public void SetSubtitleDevice(IntPtr device)
    {

    }

    public void RenderSubtitle(long frameStart, Rectangle viewportRect, Rectangle croppedVideoRect, int xOffsetInPixels)
    {

    }

    public void RenderFrame(short cx, short cy, short arx, short ary, IntPtr pSurface)
    {

    }

    public void ForceOsdUpdate(bool pForce)
    {

    }

    public void RestoreDeviceSurface(IntPtr pSurfaceDevice)
    {

    }

    public void Dispose()
    {

    }
  }

  #endregion
}

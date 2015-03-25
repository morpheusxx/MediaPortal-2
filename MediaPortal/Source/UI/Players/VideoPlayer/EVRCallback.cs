#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
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
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.UI.Players.Video.Tools;
using MediaPortal.UI.SkinEngine.DirectX11;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using Size = SharpDX.Size2;
using SizeF = SharpDX.Size2F;
using PointF = SharpDX.Vector2;

namespace MediaPortal.UI.Players.Video
{
  public delegate void VideoSizePresentDlgt(EVRCallback sender);

  [ComVisible(true), ComImport,
   Guid("324FAA1F-7DA6-4778-833B-3993D8FF4151"),
   InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  public interface IEVRPresentCallback
  {
    /// <summary>
    /// Callback from EVRPresenter.dll to display a DirectX texture.
    /// </summary>
    /// <param name="cx">Video width.</param>
    /// <param name="cy">Video height.</param>
    /// <param name="arx">Aspect Ratio X.</param>
    /// <param name="ary">Aspect Ratio Y.</param>
    /// <param name="sharedHandle">Address of the DirectX surface.</param>
    /// <returns><c>0</c>, if the method succeeded, <c>!= 0</c> else.</returns>
    [PreserveSig]
    int PresentSurface(Int16 cx, Int16 cy, Int16 arx, Int16 ary, ref IntPtr sharedHandle);
  }

  public delegate void RenderDlgt();

  [ComVisible(true)]
  [ClassInterface(ClassInterfaceType.None)]
  public class EVRCallback : IEVRPresentCallback, IDisposable
  {
    #region Variables

    private readonly object _lock = new object();
    private Size _originalVideoSize;
    private SizeF _aspectRatio;
    protected Bitmap1 _bitmap;

    private readonly RenderDlgt _renderDlgt;
    private readonly Action _onTextureInvalidated;
    private string _instanceKey;

    #endregion

    public EVRCallback(RenderDlgt renderDlgt, Action onTextureInvalidated)
    {
      _onTextureInvalidated = onTextureInvalidated;
      _renderDlgt = renderDlgt;
    }

    public void Dispose()
    {
      VideoSizePresent = null;
    }

    #region Public properties and events

    /// <summary>
    /// The first time the <see cref="OriginalVideoSize"/> property is present is when the EVR presenter
    /// delivered the first video frame. At that time, this event will be raised.
    /// </summary>
    public event VideoSizePresentDlgt VideoSizePresent;

    public Bitmap1 Bitmap
    {
      get
      {
        return _bitmap;
      }
    }

    public object BitmapLock
    {
      get { return _lock; }
    }

    /// <summary>
    /// Gets the size of the original video frame as it came from the EVR presenter.
    /// </summary>
    public Size OriginalVideoSize
    {
      get { return _originalVideoSize; }
    }

    /// <summary>
    /// Gets the aspect ratio.
    /// </summary>
    public SizeF AspectRatio
    {
      get { return _aspectRatio; }
    }

    #endregion

    #region IEVRPresentCallback implementation

    public int PresentSurface(short cx, short cy, short arx, short ary, ref IntPtr sharedHandle)
    {
      try
      {
        lock (_lock)
        {
          if (sharedHandle != IntPtr.Zero && cx != 0 && cy != 0)
          {
            if (cx != _originalVideoSize.Width || cy != _originalVideoSize.Height)
              _originalVideoSize = new Size(cx, cy);

            _aspectRatio.Width = arx;
            _aspectRatio.Height = ary;

            var tex = GraphicsDevice11.Instance.Device3D1.OpenSharedResource<Texture2D>(sharedHandle);
            var surface = tex.QueryInterface<SharpDX.DXGI.Surface>();
            FilterGraphTools.TryDispose(ref _bitmap);
            _bitmap = new Bitmap1(GraphicsDevice11.Instance.Context2D1, surface);
          }
        }

        VideoSizePresentDlgt vsp = VideoSizePresent;
        if (vsp != null)
        {
          vsp(this);
          VideoSizePresent = null;
        }

        // Inform caller that we have changed the texture
        if (_onTextureInvalidated != null)
          _onTextureInvalidated();

        if (_renderDlgt != null)
          _renderDlgt();
        return 0;
      }
      catch (SharpDXException e)
      {
        ServiceRegistration.Get<ILogger>().Warn("GraphicsDevice: DirectX Exception", e);
        GraphicsDevice11.Instance.HandleDeviceLost(e, true);
        return 0;
      }
    }

    #endregion
  }
}

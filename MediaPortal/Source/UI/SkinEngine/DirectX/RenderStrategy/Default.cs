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
using System.Threading;

namespace MediaPortal.UI.SkinEngine.DirectX.RenderStrategy
{
  /// <summary>
  /// Waits manually for the next frame time; the buffer swap is not synchronized to the display.
  /// </summary>
  class Default : AbstractStrategy
  {
    protected DateTime _frameRenderingStartTime = DateTime.MinValue;

    public Default(RenderSetup setup) : base(setup)
    {
      PresentVSync = false;
    }

    public override void BeginRender(bool doWaitForNextFame)
    {
      base.BeginRender(doWaitForNextFame);
      if (_frameRenderingStartTime != DateTime.MinValue && doWaitForNextFame)
        WaitForNextFrame();
      _frameRenderingStartTime = DateTime.Now;
    }

    private void WaitForNextFrame()
    {
      double msToNextFrame = _msPerFrame - (DateTime.Now - _frameRenderingStartTime).TotalMilliseconds;
      if (msToNextFrame > 0)
        Thread.Sleep(TimeSpan.FromMilliseconds(msToNextFrame));
    }
  }
}

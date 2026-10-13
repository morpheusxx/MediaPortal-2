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
using System.Runtime.InteropServices;
using MediaPortal.UI.SkinEngine.Players;

namespace MediaPortal.UI.Players.Mpv
{
  /// <summary>
  /// OpenGL texture with a frame buffer object which libmpv renders the video frames into. The texture is drawn by the SkinEngine like
  /// any other texture, so the video can be scaled, clipped, blurred or mirrored by the skin.
  /// </summary>
  /// <remarks>
  /// All methods have to be called from the render thread. The few OpenGL functions which are needed are loaded from the OpenGL context of the
  /// SkinEngine (<see cref="VideoRenderHost.GetGlProcAddress"/>), this avoids a dependency to an OpenGL binding.
  /// </remarks>
  internal sealed unsafe class VideoTarget : IDisposable
  {
    private const uint GL_TEXTURE_2D = 0x0DE1;
    private const uint GL_RGBA8 = 0x8058;
    private const uint GL_RGBA = 0x1908;
    private const uint GL_UNSIGNED_BYTE = 0x1401;
    private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
    private const uint GL_TEXTURE_MAG_FILTER = 0x2800;
    private const uint GL_TEXTURE_WRAP_S = 0x2802;
    private const uint GL_TEXTURE_WRAP_T = 0x2803;
    private const int GL_LINEAR = 0x2601;
    private const int GL_CLAMP_TO_EDGE = 0x812F;
    private const uint GL_FRAMEBUFFER = 0x8D40;
    private const uint GL_COLOR_ATTACHMENT0 = 0x8CE0;
    private const uint GL_FRAMEBUFFER_COMPLETE = 0x8CD5;
    private const uint GL_COLOR_BUFFER_BIT = 0x4000;

    private delegate* unmanaged[Cdecl]<int, uint*, void> _genTextures;
    private delegate* unmanaged[Cdecl]<int, uint*, void> _deleteTextures;
    private delegate* unmanaged[Cdecl]<uint, uint, void> _bindTexture;
    private delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void> _texImage2D;
    private delegate* unmanaged[Cdecl]<uint, uint, int, void> _texParameteri;
    private delegate* unmanaged[Cdecl]<int, uint*, void> _genFramebuffers;
    private delegate* unmanaged[Cdecl]<int, uint*, void> _deleteFramebuffers;
    private delegate* unmanaged[Cdecl]<uint, uint, void> _bindFramebuffer;
    private delegate* unmanaged[Cdecl]<uint, uint, uint, uint, int, void> _framebufferTexture2D;
    private delegate* unmanaged[Cdecl]<uint, uint> _checkFramebufferStatus;
    private delegate* unmanaged[Cdecl]<float, float, float, float, void> _clearColor;
    private delegate* unmanaged[Cdecl]<uint, void> _clear;

    private uint _fbo;
    private uint _texture;

    public VideoTarget()
    {
      _genTextures = (delegate* unmanaged[Cdecl]<int, uint*, void>) Load("glGenTextures");
      _deleteTextures = (delegate* unmanaged[Cdecl]<int, uint*, void>) Load("glDeleteTextures");
      _bindTexture = (delegate* unmanaged[Cdecl]<uint, uint, void>) Load("glBindTexture");
      _texImage2D = (delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void>) Load("glTexImage2D");
      _texParameteri = (delegate* unmanaged[Cdecl]<uint, uint, int, void>) Load("glTexParameteri");
      _genFramebuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>) Load("glGenFramebuffers");
      _deleteFramebuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>) Load("glDeleteFramebuffers");
      _bindFramebuffer = (delegate* unmanaged[Cdecl]<uint, uint, void>) Load("glBindFramebuffer");
      _framebufferTexture2D = (delegate* unmanaged[Cdecl]<uint, uint, uint, uint, int, void>) Load("glFramebufferTexture2D");
      _checkFramebufferStatus = (delegate* unmanaged[Cdecl]<uint, uint>) Load("glCheckFramebufferStatus");
      _clearColor = (delegate* unmanaged[Cdecl]<float, float, float, float, void>) Load("glClearColor");
      _clear = (delegate* unmanaged[Cdecl]<uint, void>) Load("glClear");
    }

    private static void* Load(string name)
    {
      IntPtr address = VideoRenderHost.GetGlProcAddress(name);
      if (address == IntPtr.Zero)
        throw new InvalidOperationException("OpenGL function '" + name + "' is not available");
      return (void*) address;
    }

    /// <summary>
    /// Gets the id of the frame buffer object to render into. <c>0</c> if the target wasn't created yet.
    /// </summary>
    public uint Fbo
    {
      get { return _fbo; }
    }

    /// <summary>
    /// Gets the id of the OpenGL texture which contains the rendered frame.
    /// </summary>
    public uint TextureId
    {
      get { return _texture; }
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>
    /// (Re-)creates the target if the size changed. Returns <c>true</c> if the target was recreated.
    /// </summary>
    public bool Ensure(int width, int height)
    {
      if (_fbo != 0 && width == Width && height == Height)
        return false;
      Delete();

      uint texture;
      _genTextures(1, &texture);
      _texture = texture;
      _bindTexture(GL_TEXTURE_2D, texture);
      _texImage2D(GL_TEXTURE_2D, 0, (int) GL_RGBA8, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, null);
      _texParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
      _texParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
      _texParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
      _texParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

      uint fbo;
      _genFramebuffers(1, &fbo);
      _fbo = fbo;
      _bindFramebuffer(GL_FRAMEBUFFER, fbo);
      _framebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, texture, 0);
      uint status = _checkFramebufferStatus(GL_FRAMEBUFFER);
      if (status != GL_FRAMEBUFFER_COMPLETE)
      {
        Delete();
        throw new InvalidOperationException("Video frame buffer is incomplete: 0x" + status.ToString("X"));
      }
      _clearColor(0f, 0f, 0f, 1f);
      _clear(GL_COLOR_BUFFER_BIT);
      _bindFramebuffer(GL_FRAMEBUFFER, 0);
      _bindTexture(GL_TEXTURE_2D, 0);

      Width = width;
      Height = height;
      return true;
    }

    private void Delete()
    {
      if (_fbo != 0)
      {
        uint fbo = _fbo;
        _deleteFramebuffers(1, &fbo);
      }
      if (_texture != 0)
      {
        uint texture = _texture;
        _deleteTextures(1, &texture);
      }
      _fbo = 0;
      _texture = 0;
      Width = 0;
      Height = 0;
    }

    public void Dispose()
    {
      Delete();
    }
  }
}

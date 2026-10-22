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
using System.IO;
using System.Runtime.InteropServices;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Extensions.BassLibraries;
using MediaPortal.UI.Players.BassPlayer.Interfaces;
using MediaPortal.UI.Players.BassPlayer.Utils;
using ManagedBass;

namespace MediaPortal.UI.Players.BassPlayer.InputSources
{
  /// <summary>
  /// Represents a file inputsource.
  /// </summary>
  /// <remarks>
  /// This class encapsulates the access to local filesystem resources as well as resources which aren't don't provide
  /// the <see cref="ILocalFsResourceAccessor"/> interface.
  /// </remarks>
  internal class BassAudioFileInputSource : AbstractBassResourceInputSource, IInputSource
  {
    /// <summary>
    /// Encapsulates stream access methods for a <see cref="FileProcedures"/> instance.
    /// </summary>
    protected class StreamInput
    {
      private readonly Stream _inputStream;
      private readonly FileProcedures _fileProcs;

      public StreamInput(Stream inputStream)
      {
        _inputStream = inputStream;
        _fileProcs = new FileProcedures { Close = CloseCallback, Length = LengthCalback, Read = ReadCallback, Seek = SeekCallback };
      }

      private bool SeekCallback(long offset, IntPtr user)
      {
        try
        {
          _inputStream.Seek(offset, SeekOrigin.Begin);
          return true;
        }
        catch
        {
          return false;
        }
      }

      private int ReadCallback(IntPtr buffer, int length, IntPtr user)
      {
        // Code taken from BASS help for FileProcedures class
        try
        {
          // At first we need to create a byte[] with the size of the requested length
          byte[] data = new byte[length];
          // Read the file into data
          int bytesread = _inputStream.Read(data, 0, length);
          // And now we need to copy the data to the buffer we write as many bytes as we read via the file operation.
          Marshal.Copy(data, 0, buffer, bytesread);
          return bytesread;
        }
        catch { return 0; }
      }

      private long LengthCalback(IntPtr user)
      {
        return _inputStream.Length;
      }

      private void CloseCallback(IntPtr user)
      {
        _inputStream.Close();
      }

      public FileProcedures FileProcs
      {
        get { return _fileProcs; }
      }
    }

    #region Static members

    /// <summary>
    /// Creates and initializes an new instance.
    /// </summary>
    /// <param name="resourceAccessor">The resource accessor to the media item to be handled by the instance.</param>
    /// <returns>The new instance.</returns>
    public static BassAudioFileInputSource Create(IFileSystemResourceAccessor resourceAccessor)
    {
      BassAudioFileInputSource inputSource = new BassAudioFileInputSource(resourceAccessor);
      inputSource.Initialize();
      return inputSource;
    }

    #endregion

    #region Fields

    protected BassStream _BassStream;
    protected StreamInput _streamInput = null;

    #endregion

    #region IInputSource Members

    public MediaItemType MediaItemType
    {
      get { return MediaItemType.AudioFile; }
    }

    public BassStream OutputStream
    {
      get { return _BassStream; }
    }

    public TimeSpan Length
    {
      get { return _BassStream.Length; }
    }

    #endregion

    #region IDisposable Members

    public override void Dispose()
    {
      base.Dispose();
      if (_BassStream != null)
        _BassStream.Dispose();
    }

    #endregion

    #region Private Members

    private BassAudioFileInputSource(IFileSystemResourceAccessor resourceAccessor) : base(resourceAccessor) { }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    private void Initialize()
    {
      Log.Debug("BassAudioFileInputSource.Initialize()");

      BassFlags flags = BassFlags.Decode | BassFlags.Float;

      int handle;
      ILocalFsResourceAccessor lfra = _accessor as ILocalFsResourceAccessor;
      if (lfra == null)
      { // Build stream reading procs for the resource's input stream
        flags |= BassFlags.Prescan;
        _streamInput = new StreamInput(_accessor.OpenRead());
        handle = Bass.CreateStream(
            StreamSystem.NoBuffer, flags, _streamInput.FileProcs, IntPtr.Zero);
      }
      else
        // Optimize access to local filesystem resource
        handle = lfra.RunWithLocalFileSystemAccess(() => Bass.CreateStream(lfra.LocalFileSystemPath, 0, 0, flags));

      if (handle == BassConstants.BassInvalidHandle)
        throw new BassLibraryException("BASS_StreamCreateFile");

      _BassStream = BassStream.Create(handle);
    }

    #endregion
  }
}

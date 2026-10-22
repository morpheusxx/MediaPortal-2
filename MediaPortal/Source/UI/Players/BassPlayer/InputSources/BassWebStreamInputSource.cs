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
using MediaPortal.Extensions.BassLibraries;
using MediaPortal.UI.Players.BassPlayer.Interfaces;
using MediaPortal.UI.Players.BassPlayer.Utils;
using ManagedBass;

namespace MediaPortal.UI.Players.BassPlayer.InputSources
{
  /// <summary>
  /// Represents a file inputsource.
  /// </summary>
  internal class BassWebStreamInputSource : IInputSource, ITagSource
  {
    #region Static members

    /// <summary>
    /// Creates and initializes an new instance.
    /// </summary>
    /// <param name="url">The URL to be handled by the instance.</param>
    /// <returns>The new instance.</returns>
    public static BassWebStreamInputSource Create(string url)
    {
      BassWebStreamInputSource inputSource = new BassWebStreamInputSource(url);
      inputSource.Initialize();
      return inputSource;
    }

    #endregion

    #region Fields

    private readonly string _url;
    private readonly TagInfo _tagInfo;
    private int _handle;
    private BassStream _bassStream;

    #endregion

    public string URL
    {
      get { return _url; }
    }

    /// <summary>
    /// Tries to get tags from current stream. Note: the information is retrieved from BASS library in each access to this property.
    /// </summary>
    public TagInfo Tags
    {
      get
      {
        // Update the tags
        _tagInfo.UpdateFromStream(_handle);
        return _tagInfo;
      }
    }

    #region IInputSource Members

    public MediaItemType MediaItemType
    {
      get { return MediaItemType.WebStream; }
    }

    public BassStream OutputStream
    {
      get { return _bassStream; }
    }

    public TimeSpan Length
    {
      get { return _bassStream.Length; }
    }

    #endregion

    #region IDisposable Members

    public void Dispose()
    {
      if (_bassStream != null)
        _bassStream.Dispose();
    }

    #endregion

    #region Public members

    #endregion

    #region Private Members

    private BassWebStreamInputSource(string url)
    {
      _url = url;
      _tagInfo = new TagInfo(_url);
    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    private void Initialize()
    {
      Log.Debug("BassWebStreamInputSource.Initialize()");

      const BassFlags flags = BassFlags.Decode | BassFlags.Float;

      _handle = Bass.CreateStream(_url, 0, flags, null, new IntPtr());

      if (_handle == BassConstants.BassInvalidHandle)
        throw new BassLibraryException("BASS_StreamCreateURL");

      _bassStream = BassStream.Create(_handle);
    }

    #endregion

    public override string ToString()
    {
      return GetType().Name + ": " + _url;
    }
  }
}

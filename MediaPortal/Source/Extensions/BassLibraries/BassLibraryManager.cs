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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MediaPortal.Utilities;
using MediaPortal.Utilities.FileSystem;
using MediaPortal.Utilities.SystemAPI;
using ManagedBass;

namespace MediaPortal.Extensions.BassLibraries
{
  /// <summary>
  /// Manages the Bass audio library and its plugins.
  /// </summary>
  public class BassLibraryManager : IDisposable
  {
    #region Static members

    private static readonly object _syncObj = new object();
    private static volatile int _refCount = 0;
    private readonly ICollection<int> _decoderPluginHandles = new List<int>();

    [Obsolete("Player plugins are now located in BassLibraries plugin. Setting other folder is no longer supported.")]
    public static BassLibraryManager Get(string playerPluginsDirectory)
    {
      return Get();
    }

    /// <summary>
    /// Loads and initializes the Bass library.
    /// </summary>
    /// <returns>The new instance.</returns>
    public static BassLibraryManager Get()
    {
      lock (_syncObj)
      {
        BassNativeLibrary.Install();
        string playerPluginsDirectory = BassNativeLibrary.PluginsDirectory;
        var bassLibraryManager = new BassLibraryManager();
        bassLibraryManager.Initialize(playerPluginsDirectory);
        return bassLibraryManager;
      }
    }

    #endregion

    #region Private members

    private BassLibraryManager()
    {
    }

    private void Initialize(string playerPluginsDirectory)
    {
      Interlocked.Increment(ref _refCount);
      if (_refCount > 1)
        return;

      // Initialize the NoSound device to enable calling BASS functions before our output device is properly initialized
      if (!Bass.Init(BassConstants.BassNoSoundDevice, 44100, DeviceInitFlags.Default, IntPtr.Zero))
        throw new BassLibraryException("BASS_Init");

      // Set volume to linear scale as requested by the IVolumeControl interface
      if (!Bass.Configure(Configuration.LogarithmicVolumeCurve, false))
        throw new BassLibraryException("BASS_SetConfig");

      // Disable update thread while the player is not running
      if (!Bass.Configure(Configuration.UpdatePeriod, 0))
        throw new BassLibraryException("BASS_SetConfig");

      LoadPlugins(playerPluginsDirectory);
    }

    private void LoadPlugins(string playerPluginsDirectory)
    {
      Log.Info("Loading player add-ins from '{0}'", playerPluginsDirectory);

      if (!Directory.Exists(playerPluginsDirectory))
      {
        Log.Error("Unable to find player add-ins folder '{0}'", playerPluginsDirectory);
        return;
      }

      // Fix for MP2-285: loading of some BASS plugin fails because unmanaged dlls are not found in executable folder.
      // The working directory is temporary changed while loading plugins.
      string currentDirectory = Directory.GetCurrentDirectory();
      Directory.SetCurrentDirectory(playerPluginsDirectory);

      int loaded = 0;
      foreach (string pluginFile in Directory.GetFiles(playerPluginsDirectory, BassNativeLibrary.PluginFilePattern))
      {
        int handle = Bass.PluginLoad(pluginFile);
        if (handle == 0)
        {
          Log.Debug("Unable to load plugin '{0}': {1}", pluginFile, Bass.LastError); // Probably loaded already or not a BASS add-in
          continue;
        }
        Log.Debug("Loaded plugin '{0}'", pluginFile);
        _decoderPluginHandles.Add(handle);
        loaded++;
      }

      if (loaded == 0)
        Log.Info("No audio decoders loaded; probably already loaded.");
      else
        Log.Info("Loaded {0} audio decoders.", loaded);

      Directory.SetCurrentDirectory(currentDirectory);
    }

    #endregion

    #region IDisposable Members

    public void Dispose()
    {
      lock (_syncObj)
      {
        Log.Debug("BassLibraryManager.Dispose()");
        Interlocked.Decrement(ref _refCount);
        if (_refCount > 0)
          return;

        Log.Debug("Unloading all BASS player plugins");
        foreach (int pluginHandle in _decoderPluginHandles)
          Bass.PluginFree(pluginHandle);
        _decoderPluginHandles.Clear();

        // Free the NoSound device
        Bass.CurrentDevice = BassConstants.BassNoSoundDevice;
        if (Bass.LastError != Errors.OK)
          throw new BassLibraryException("BASS_SetDevice");

        if (!Bass.Free())
          throw new BassLibraryException("BASS_Free");
      }
    }

    #endregion
  }
}

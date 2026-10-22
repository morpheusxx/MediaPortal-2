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
using System.Reflection;
using System.Runtime.InteropServices;
using ManagedBass;
using ManagedBass.Cd;
using ManagedBass.Mix;
using ManagedBass.Wasapi;

namespace MediaPortal.Extensions.BassLibraries
{
  /// <summary>
  /// Finds the native BASS libraries. They are delivered in <c>runtimes\&lt;rid&gt;\native</c> of this plugin; the BASS add-ins (decoders) are in
  /// the sub folder <c>Plugins</c>. The managed ManagedBass assemblies ask for the libraries by their short name (<c>bass</c>, <c>bassmix</c>, ...).
  /// </summary>
  public static class BassNativeLibrary
  {
    private static readonly object _syncObj = new object();
    private static bool _installed;

    /// <summary>
    /// Gets the runtime identifier of the current platform, as used for the folder names of native libraries.
    /// </summary>
    public static string CurrentRid
    {
      get
      {
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
          Architecture.X86 => "x86",
          Architecture.Arm => "arm",
          Architecture.Arm64 => "arm64",
          _ => "x64"
        };
        return (OperatingSystem.IsWindows() ? "win-" : OperatingSystem.IsMacOS() ? "osx-" : "linux-") + arch;
      }
    }

    /// <summary>
    /// Gets the folder which contains the native BASS libraries.
    /// </summary>
    public static string NativeDirectory
    {
      get { return Path.Combine(Path.GetDirectoryName(typeof(BassNativeLibrary).Assembly.Location) ?? AppContext.BaseDirectory, "runtimes", CurrentRid, "native"); }
    }

    /// <summary>
    /// Gets the folder which contains the BASS add-ins.
    /// </summary>
    public static string PluginsDirectory
    {
      get { return Path.Combine(NativeDirectory, "Plugins"); }
    }

    /// <summary>
    /// Gets the file name pattern of the BASS add-ins.
    /// </summary>
    public static string PluginFilePattern
    {
      get { return OperatingSystem.IsWindows() ? "bass*.dll" : OperatingSystem.IsMacOS() ? "libbass*.dylib" : "libbass*.so"; }
    }

    /// <summary>
    /// Makes sure that the managed BASS assemblies find the native libraries. Can be called multiple times.
    /// </summary>
    public static void Install()
    {
      lock (_syncObj)
      {
        if (_installed)
          return;
        _installed = true;
        foreach (Assembly assembly in new[] { typeof(Bass).Assembly, typeof(BassMix).Assembly, typeof(BassWasapi).Assembly, typeof(BassCd).Assembly })
          NativeLibrary.SetDllImportResolver(assembly, Resolve);
      }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
      string fileName = OperatingSystem.IsWindows() ? libraryName + ".dll" :
          OperatingSystem.IsMacOS() ? "lib" + libraryName + ".dylib" : "lib" + libraryName + ".so";
      foreach (string directory in new[] { NativeDirectory, PluginsDirectory })
      {
        string path = Path.Combine(directory, fileName);
        if (File.Exists(path) && NativeLibrary.TryLoad(path, out IntPtr handle))
          return handle;
      }
      return IntPtr.Zero; // The default probing (for example system wide installation) follows
    }
  }
}

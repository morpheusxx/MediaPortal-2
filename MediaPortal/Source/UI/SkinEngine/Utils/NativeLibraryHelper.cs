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
using System.Reflection;
using System.Runtime.InteropServices;
using MediaPortal.Common;
using MediaPortal.Common.Logging;

namespace MediaPortal.UI.SkinEngine.Utils
{
  /// <summary>
  /// Loads the native libraries which are delivered in the plugin directory (<c>runtimes\&lt;rid&gt;\native</c>).
  /// </summary>
  /// <remarks>
  /// The plugin assemblies are not part of the dependency context of the application, so the runtime doesn't probe the
  /// <c>runtimes</c> folder of the plugin. Native libraries which are loaded explicitly by their path are found by name afterwards,
  /// when the libraries which depend on them (Skia, GLFW) try to load them.
  /// </remarks>
  public static class NativeLibraryHelper
  {
    /// <summary>
    /// Gets the runtime identifier of the current platform, as used for the folder names of native libraries.
    /// </summary>
    public static string CurrentRid
    {
      get
      {
        string arch;
        switch (RuntimeInformation.ProcessArchitecture)
        {
          case Architecture.X86:
            arch = "x86";
            break;
          case Architecture.Arm:
            arch = "arm";
            break;
          case Architecture.Arm64:
            arch = "arm64";
            break;
          default:
            arch = "x64";
            break;
        }
        if (OperatingSystem.IsWindows())
          return "win-" + arch;
        if (OperatingSystem.IsMacOS())
          return "osx-" + arch;
        return "linux-" + arch;
      }
    }

    /// <summary>
    /// Loads the native libraries of the given names from <c>runtimes\&lt;rid&gt;\native</c> of the directory of the given assembly.
    /// Libraries which are missing are ignored.
    /// </summary>
    /// <param name="assembly">Assembly whose directory contains the <c>runtimes</c> folder.</param>
    /// <param name="windowsNames">File names of the libraries to load on Windows.</param>
    /// <param name="linuxNames">File names of the libraries to load on Linux.</param>
    /// <param name="macNames">File names of the libraries to load on macOS.</param>
    public static void Preload(Assembly assembly, string[] windowsNames, string[] linuxNames, string[] macNames)
    {
      string[] names = OperatingSystem.IsWindows() ? windowsNames : OperatingSystem.IsMacOS() ? macNames : linuxNames;
      string directory = Path.Combine(Path.GetDirectoryName(assembly.Location) ?? string.Empty, "runtimes", CurrentRid, "native");
      foreach (string name in names)
      {
        string path = Path.Combine(directory, name);
        if (!File.Exists(path))
          continue;
        try
        {
          NativeLibrary.Load(path);
        }
        catch (Exception ex)
        {
          ServiceRegistration.Get<ILogger>().Warn("NativeLibraryHelper: Unable to load '{0}'", ex, path);
        }
      }
    }
  }
}

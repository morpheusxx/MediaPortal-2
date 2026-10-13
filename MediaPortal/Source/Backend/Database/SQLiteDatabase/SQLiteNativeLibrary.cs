#region Copyright (C) 2007-2020 Team MediaPortal

/*
    Copyright (C) 2007-2020 Team MediaPortal
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
using SQLitePCL;

namespace MediaPortal.Database.SQLite
{
  /// <summary>
  /// Makes sure the native SQLite library (e_sqlite3, shipped by SQLitePCLRaw) is found. The plugin lives in its own directory
  /// which is not part of the application's native library probing, so the library is loaded from
  /// <c>runtimes\&lt;rid&gt;\native</c> next to the SQLitePCLRaw provider assembly (copied there by the build).
  /// </summary>
  internal static class SQLiteNativeLibrary
  {
    private const string LIBRARY_NAME = "e_sqlite3";

    private static readonly object _syncObj = new object();
    private static bool _initialized;

    public static void Initialize()
    {
      lock (_syncObj)
      {
        if (_initialized)
          return;
        Assembly provider = Assembly.Load("SQLitePCLRaw.provider.e_sqlite3");
        NativeLibrary.SetDllImportResolver(provider, ResolveLibrary);
        Batteries_V2.Init();
        _initialized = true;
      }
    }

    private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
      if (libraryName != LIBRARY_NAME)
        return IntPtr.Zero;

      string directory = Path.GetDirectoryName(assembly.Location);
      string rid = GetRuntimeIdentifier();
      string fileName = OperatingSystem.IsWindows() ? LIBRARY_NAME + ".dll" : OperatingSystem.IsMacOS() ? "lib" + LIBRARY_NAME + ".dylib" : "lib" + LIBRARY_NAME + ".so";
      string path = Path.Combine(directory, "runtimes", rid, "native", fileName);
      if (File.Exists(path) && NativeLibrary.TryLoad(path, out IntPtr handle))
        return handle;
      // Not found: fall back to the default probing of the runtime
      return IntPtr.Zero;
    }

    private static string GetRuntimeIdentifier()
    {
      string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
      if (os == "linux" && RuntimeInformation.RuntimeIdentifier.Contains("musl", StringComparison.OrdinalIgnoreCase))
        os = "linux-musl";
      string arch;
      switch (RuntimeInformation.ProcessArchitecture)
      {
        case Architecture.X86: arch = "x86"; break;
        case Architecture.Arm: arch = "arm"; break;
        case Architecture.Arm64: arch = "arm64"; break;
        default: arch = "x64"; break;
      }
      return os + "-" + arch;
    }
  }
}

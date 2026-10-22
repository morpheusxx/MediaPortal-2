# MediaPortal.Native.Bass

NuGet package with the native BASS libraries of **Linux** (x64, arm64) and **macOS** (x64, arm64) used by the `BassLibraries` plugin
(`runtimes/<rid>/native/libbass.*`, `libbassmix.*` and the decoder add-ins in `runtimes/<rid>/native/Plugins`).
The Windows x64 libraries come from the NuGet package `BASSCombined`, they are not part of this package.

The libraries are products of Un4seen Developments (https://www.un4seen.com), BASS is free for non-commercial use
(see https://www.un4seen.com/bass.html#license). The package contains unmodified copies of the downloads of https://www.un4seen.com/files.

Create the package (downloads the current libraries, needs the .NET SDK):

    python build-package.py                    # revision = today
    python build-package.py --revision 20261022

The package `feed/MediaPortal.Native.Bass.<2.4.0.revision>.nupkg` is picked up by `BassLibraries.csproj` from the local `feed` folder.
The `NOTICE.txt` in the package lists the SHA-256 of every downloaded archive.
Publish it on the Team MediaPortal feed to make it available to everybody:

    dotnet nuget push feed/MediaPortal.Native.Bass.2.4.0.20261022.nupkg -s https://www.myget.org/F/mediaportal/api/v2/package -k <api key>

To update the libraries: build and publish a new package, set `BassNativeVersion` in `BassLibraries.csproj`, run the playback smoke test
(play, pause, seek and stop an audio file in the client) on each platform you can test.

The add-ins are only loaded if they exist for the platform, not every add-in is available for macOS (for example `bassalac`).

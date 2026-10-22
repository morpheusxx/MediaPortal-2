# MediaPortal.Native.Mpv

NuGet package with the native libmpv library used by the `MpvPlayer` plugin (`runtimes/win-x64/native/libmpv-2.dll`).
Linux uses the libmpv package of the distribution (`libmpv2`), it is not part of this package.

Create a package from an upstream build (https://github.com/shinchiro/mpv-winbuild-cmake/releases, generic `x86_64`):

    python build-package.py                 # latest build
    python build-package.py --tag 20261021  # a specific build

The package (`feed/MediaPortal.Native.Mpv.<yyyy.M.d>.nupkg`) is picked up by `MpvPlayer.csproj` from the local `feed` folder.
Publish it on the Team MediaPortal feed to make it available to everybody:

    dotnet nuget push feed/MediaPortal.Native.Mpv.2026.10.21.nupkg -s https://www.myget.org/F/mediaportal/api/v2/package -k <api key>

To update libmpv: build and publish a new package, set `MpvNativeVersion` in `MpvPlayer.csproj`, run the playback smoke test
(play, pause, stop and replay a video in the client).

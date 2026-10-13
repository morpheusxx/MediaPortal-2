# Phase 0 dependency inventory (portable core: `MediaPortal.Utilities`, `MediaPortal.Common`, `UPnP`)

Status 2026-10-13: the portable core builds as plain `net10.0` with **zero CA1416 warnings**, and CA1416 is an **error** for every
non-`-windows` project (`Directory.Build.targets`). New Windows-only calls in portable projects therefore fail the build.

Note: `VersionInfo.cs` used to stamp `[assembly: SupportedOSPlatform("windows")]` on every project, which hid all CA1416 warnings.
It is now only emitted for projects with a `*-windows` TFM (`MP2_WINDOWS_TFM`, set in `Directory.Build.targets`).

CA1416 does not see P/Invoke (kernel32, user32, ...): those compile fine and only fail at runtime on Linux. They are tracked separately below.

## How the portable core was made clean

| Area | Approach |
|---|---|
| `System.Drawing` (`ImageUtilities`, `BaseInfo`, `BaseFanArtHandler`) | `ImageUtilities` rewritten on SkiaSharp; old API kept as `GdiImageUtilities` in `MediaPortal.Utilities.Windows` |
| WinForms/GDI helpers (SplashScreen, FormExtensions, ControlAwaiter, LauncherExceptionHandling, FormDpiAwareness, UiCrashLogger) | moved to `MediaPortal.Utilities.Windows` (`net10.0-windows`), namespaces unchanged |
| `WindowsAPI` (shutdown, suspend, keep-awake, window messages, screensaver, autostart registry) | public methods guarded with `OperatingSystem.IsWindows()`; harmless fallbacks (`0`/`false`/no-op), autostart Add/Remove throw `PlatformNotSupportedException`, `GetAutostartApplicationPath` returns `null` |
| `SystemStateService` | Windows paths unchanged; elsewhere shutdown/restart/suspend/hibernate run `systemctl`, logoff is logged as unsupported; power events only created on Windows (`PowerEventHandler` is `SupportedOSPlatform("windows")`) |
| Impersonation (`ImpersonationService`, `WindowsIdentityWrapper`, `TokenWrapper`, `LogonHelper`, `AsyncImpersonationProcess`) | marked Windows-only; new `NoImpersonationService` (runs everything as current user, credential registration returns `false`) is registered on other platforms |
| Dokan virtual drive (`Dokan`, `DokanFileInfo`, `Proxy`, `ResourceMountingService`) | marked Windows-only; new `NullResourceMountingService` on other platforms (mounting reported unavailable, which callers already handle) |
| `IpcServer` | Windows keeps ACL + message mode; elsewhere byte-mode pipe with `CurrentUserOnly` (protocol is length-prefixed, the client already uses byte mode) |
| `SingleInstanceHelper` | mutex ACL only on Windows; `SwitchToCurrentInstance` window broadcast is a no-op elsewhere (needs an IPC based replacement for Linux) |
| `MimeTypeDetector` | urlmon content sniffing and registry lookup only on Windows; the built-in extension table is the fallback |
| `ThreadingUtils.RunSTAThreaded` | STA only set on Windows |
| `CrashLoggerBase`, `ApplicationCore` | WMI/registry system info on Windows; `System.IO.DriveInfo`, `/proc/cpuinfo` and `RuntimeInformation` elsewhere |
| `DriveUtils.GetDiskSize` | WMI replaced by `DriveInfo.TotalSize` |
| `TaskScheduler` | wake-from-standby waitable timer only created on Windows |
| `SharesEnumerator` | UNC/share enumeration guarded (returns `false`/empty off Windows) |

## Remaining P/Invoke in portable projects (not guarded, Windows-only by nature)

These compile everywhere but must not be reached on Linux. Reachability today:

- `Utilities/FileSystem/DriveUtils.cs` (eject/volume P/Invoke, drive-letter assumptions): only used by the client's removable-media UI.
- `Utilities/Network/NetworkResourcesEnumerator.cs`: only used by `NetworkNeighborhoodResourceProvider` (Windows plugin).
- `Utilities/Security/SafeTokenHandle.cs`, `SafeThreadHandle.cs`, `Process/ImpersonationHelper.cs`, `ImpersonationProcess.cs`,
  `SystemAPI/NativeMethods.cs`: used by the Windows-only impersonation/power code, and by the `[Obsolete]` impersonating overloads in `ProcessUtils`.
- `Common/Services/Dokan/Native/DokanNativeMethods.cs`, `ImpersonationService/*`: only reachable from the Windows-only classes above.
- Native library wrappers (MediaInfo, FreeImage, OpenCV, BASS, FreeType): need per-RID native packaging, see Phase 1/4.

Known leftovers that are not CA1416-visible: `Thread.Abort` usage in `ThreadingUtils` (throws on .NET Core), drive-letter/DOS path
assumptions in `LocalFsResourceProviderBase.ToDosPath` and friends (Phase 1).

## Other System.Drawing users (outside the portable core)

`GDIThumbnailProvider` (Windows-only; a Skia-based thumbnail provider is needed for Linux), `ImageMetadataExtractor` (EXIF),
`TranscodingService`, `MP2Extended`, `OnlineLibraries`/TvdbLib, `BluRayMetadataExtractor` resources, `BassAudioMetadataExtractor`.
`ShellThumbnailProvider` is a legacy net4.7 project in no solution and was left untouched.

## Packaging notes

`SkiaSharp` is referenced with `PrivateAssets="all"` from `Utilities` and added to the executables (`Server` also gets
`SkiaSharp.NativeAssets.Linux.NoDependencies`, `Client` only the Windows/macOS assets). Without this every project's output received
~10 platform copies of libSkiaSharp (filled the disk). The future Linux client will want `SkiaSharp.NativeAssets.Linux` (fontconfig)
instead of the NoDependencies variant, not both.

## Server solution: portable vs Windows-only (59 of 69 projects are plain `net10.0`)

Retargeted to `net10.0` (and still building with CA1416 as error): `MediaPortal.Backend`, `ServerBase`, `ServerSettings.*`, `ServerStateService*`,
`ConfigurationManager`, `SQLiteDatabase`, `OnlineLibraries`, `FanArtService.*`, `MediaInfoLib`, `FFMpegLib`, `FreeImageLib`, `OpenCvLib`, `TagLib`,
`MatroskaLib`, `BassLibraries`, `BluRay`/`Video`/`Audio`/`BassAudio`/`Movie`/`Series`/`Subtitle`/`Nfo`/`Scriptable`/`Stub`/`TvMosaic` metadata extractors,
`VideoThumbnailer`, `OCVVideoThumbnailer`, `GenreProvider`, Iso/Zip/AudioCD resource providers, `MediaServer`, `TranscodingService*`, `WakeOnLan.*`,
SlimTv interface/integration/proxy projects, `codeRR`, `MediaPortal.Mock`, `Tests.MediaServer`.

Still `net10.0-windows`, with the reason:

| Project | Reason |
|---|---|
| `MediaPortal.Server` | WinForms `WinExe` + `ServiceBase`; Phase 1 replaces it with a generic host |
| `GDIThumbnailProvider`, `WICThumbnailProvider` | GDI+ / WIC (SharpDX); on other platforms `SkiaThumbnailProvider` (priority 120, after WIC 50 and GDI 110) is the image provider |
| `NetworkNeighborhoodResourceProvider` | Win32 network neighborhood (`mpr.dll`) |
| `MP2Extended` | ~49 `System.Drawing` call sites (`Images.cs`, `BaseGetArtwork`, `OnlineVideosThumbs`); port to `ImageUtilities` |
| `Tve3RecordingMetadataExtractor`, `SlimTv.Service35` | TV (out of scope), COM / TvEngine |
| `Tests.Server`, `Test.TranscodingService`, `DevTools` | reference the Windows projects above |

`OnlineLibraries`/TvdbLib: the in-memory `System.Drawing.Image` members of the banner classes (`BannerImage`, `ThumbImage`, `VignetteImage`,
cache `SaveToCache`/`LoadImageFromCache`) are now `byte[]`. MP2 itself never used the `Image` based API (it uses `LoadImageDataAsync()`).
`MediaServer` DLNA icon request now converts through `ImageUtilities` (Skia).

Known tooling issue: building/testing a single project outside its solution fails in the repo's `build.targets` copy steps
(`$(SolutionName)` is `*Undefined*`); build via the solution files (or pass `-p:SolutionName=... -p:SolutionDir=...`). The test projects were
therefore not run.

`ImageMetadataExtractor` is portable now: its EXIF code already read via FreeImage, `System.Drawing` was only used for an unused `Bitmap` thumbnail
member (removed). Note FreeImage still needs Linux native libraries (`FreeImageLib` packaging, Phase 1).

New `SkiaThumbnailProvider` plugin (`net10.0`, in `MP2-Server.sln` and `MP2-Client.sln`): uses `ImageUtilities.TryCreateThumbnail`
(EXIF orientation applied via `SKCodec.EncodedOrigin`, no upscaling, PNG if the source has alpha else JPEG q95). Verified with a scratch program
for all EXIF orientations used in practice (1, 2, 3, 6, 8), alpha PNG and invalid data.

Output layout: `Directory.Build.props` sets `AppendTargetFrameworkToOutputPath=false`. The per-project `build.targets` copy plugins to
`Bin\<solution>\<OutDir>`, which contained the TFM; with a mix of `net10.0` and `net10.0-windows` projects the plugins would have been split
over two folders (the server exe only sees its own). Everything now lands in `Bin\<solution>in\<config>\`.

## Next

1. (`MP2Extended` -> `ImageUtilities` was deliberately skipped for now.)
2. Linux server solution filter (`MP2-Server-Linux.slnf`) and a Linux CI job.
3. Server host without WinForms/Windows service (Phase 1).

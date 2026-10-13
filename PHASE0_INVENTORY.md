# Phase 0 dependency inventory (portable core: `MediaPortal.Utilities`, `MediaPortal.Common`, `UPnP`)

Generated from the CA1416 warnings of a Windows build with `Utilities`, `Common` and `UPnP` targeting plain `net10.0`
(2026-10-13), plus a grep for P/Invoke. **CA1416 does not see P/Invoke into kernel32/advapi32 etc.**: those compile fine
and only fail at runtime on Linux, so both lists matter.

Note: `VersionInfo.cs` used to stamp `[assembly: SupportedOSPlatform("windows")]` on every project, which hid all of this.
It is now only emitted for projects with a `*-windows` TFM (`MP2_WINDOWS_TFM`, set in `Directory.Build.targets`).

## CA1416 hits in portable projects (file: count)

- `Source/Core/MediaPortal.Utilities/Graphics/ImageUtilities.cs`: 36
- `Source/Core/MediaPortal.Common/Services/Logging/CrashLoggerBase.cs`: 13
- `Source/Core/MediaPortal.Common/Services/Dokan/Proxy.cs`: 13
- `Source/Core/MediaPortal.Utilities/SystemAPI/WindowsAPI.cs`: 9
- `Source/Core/MediaPortal.Utilities/Process/IpcServer.cs`: 7
- `Source/Core/MediaPortal.Utilities/Process/SingleInstanceHelper.cs`: 5
- `Source/Core/MediaPortal.Common/Services/ResourceAccess/ImpersonationService/WindowsIdentityWrapper.cs`: 5
- `Source/Core/MediaPortal.Common/Services/Runtime/SystemStateService.cs`: 4
- `Source/Core/MediaPortal.Utilities/FileSystem/DriveUtils.cs`: 3
- `Source/Core/MediaPortal.Utilities/SystemAPI/MimeTypeDetector.cs`: 2
- `Source/Core/MediaPortal.Common/FanArt/BaseFanArtHandler.cs`: 2
- `Source/Core/MediaPortal.Utilities/ThreadingUtils.cs`: 1
- `Source/Core/MediaPortal.Common/Services/Runtime/PowerEventHandler.cs`: 1
- `Source/Core/MediaPortal.Common/Services/ResourceAccess/ImpersonationService/LogonHelper.cs`: 1
- `Source/Core/MediaPortal.Common/Services/Dokan/DokanFileInfo.cs`: 1
- `Source/Core/MediaPortal.Common/MediaManagement/Helpers/BaseInfo.cs`: 1
- `Source/Core/MediaPortal.Common/ApplicationCore.cs`: 1

Main flagged APIs: `System.Drawing` (Image, ImageCodecInfo, Encoder*, ImageFormat, RotateFlipType: `ImageUtilities`, `BaseInfo`,
`BaseFanArtHandler`), `Registry*` (`WindowsAPI`, `ApplicationCore`, `MimeTypeDetector`, `CrashLoggerBase`),
`WindowsIdentity`/ACL types (impersonation, `IpcServer`, `SingleInstanceHelper`), `Thread.SetApartmentState`,
Dokan proxy, `SystemStateService`/`PowerEventHandler`.

## P/Invoke (not covered by CA1416) in portable projects

- `Source/Core/MediaPortal.Common/Services/Dokan/Native/DokanNativeMethods.cs`
- `Source/Core/MediaPortal.Common/Services/ResourceAccess/ImpersonationService/AsyncImpersonationProcess.cs`
- `Source/Core/MediaPortal.Common/Services/ResourceAccess/ImpersonationService/LogonHelper.cs`
- `Source/Core/MediaPortal.Common/Services/TaskScheduler/TaskWaitableTimer.cs`
- `Source/Core/MediaPortal.Utilities/FileSystem/DriveUtils.cs`
- `Source/Core/MediaPortal.Utilities/MimeTypeDetector.cs`
- `Source/Core/MediaPortal.Utilities/Network/NetworkResourcesEnumerator.cs`
- `Source/Core/MediaPortal.Utilities/Network/SharesEnumerator.cs`
- `Source/Core/MediaPortal.Utilities/Security/SafeThreadHandle.cs`
- `Source/Core/MediaPortal.Utilities/Security/SafeTokenHandle.cs`
- `Source/Core/MediaPortal.Utilities/SystemAPI/MimeTypeDetector.cs`
- `Source/Core/MediaPortal.Utilities/SystemAPI/NativeMethods.cs`
- `Source/Core/MediaPortal.Utilities/SystemAPI/WindowsAPI.cs`

## Already split out into `MediaPortal.Utilities.Windows` (net10.0-windows)

`Screens/SplashScreen`, `Screens/FormExtensions` (from Utilities); `ControlAwaiter`, `LauncherExceptionHandling`,
`FormDpiAwareness`, `UiCrashLogger` (from Common). Namespaces were kept so callers did not change.

## Suggested next work, in order

1. `System.Drawing` -> SkiaSharp/ImageSharp in `ImageUtilities`, `BaseInfo`, `BaseFanArtHandler` (largest group; shared with the client Skia work).
2. Registry/`WindowsAPI`/`MimeTypeDetector`/`ApplicationCore`: guard with `OperatingSystem.IsWindows()` or move behind an interface.
3. Impersonation, `IpcServer`/`SingleInstanceHelper`, Dokan, `DriveUtils`, `NetworkResourcesEnumerator`/`SharesEnumerator`,
   `TaskWaitableTimer`, power/system-state: introduce platform interfaces with Windows implementations (and Linux stubs).
4. Once warnings are zero, make CA1416 an error on the portable projects so regressions fail the build.
5. Then continue outward: `UPnP` is already plain `net10.0`; next candidates are `MediaPortal.Backend`, `ServerBase`,
   `ServerSettings.*`, `SQLiteDatabase`, and the metadata extractors with no Windows dependency.

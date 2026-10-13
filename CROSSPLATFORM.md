# MediaPortal 2 – Cross-Platform Rework

Handoff from the planning phase (architecture evaluation + proof of concept).
Keep this file updated as work progresses: decisions, status, next steps.

## Goal and scope

Make MP2 a real cross-platform solution (Windows, Linux; macOS optional later).

- **In scope:** MP2 Server on Linux, the client's generic UI (SkinEngine/MPF), video playback, audio playback.
- **Out of scope for now:** TV (TVE3/TVE35 integration), OnlineVideos and other non-core plugins.

## Current state of the codebase (as understood before starting)

- The code was ported from .NET 4.8 to .NET 6 in MP2 2.5. .NET 6 is out of support → upgrade to .NET 8 or 10 LTS.
- Server: no UI, manages the media database. Expected Windows dependencies to verify:
  Windows Service hosting, System.Drawing (Windows-only since .NET 6; likely thumbnails/image metadata),
  drive-letter path assumptions in resource providers, the network-neighborhood/SMB provider, registry and %ProgramData% paths,
  per-RID packaging of native libraries (MediaInfo, SQLite, FFmpeg).
- Client: Windows-only. SkinEngine = custom XAML parser + MPF control library (platform-neutral C#)
  on top of a SharpDX rendering layer (Direct3D 9Ex + Direct2D/DirectWrite, HLSL effects).
  Video via DirectShow + native C++ EVR presenter. Music via BASS (BassPlayer).
- **The points above were assumptions; the first code review found (before Phase 0 changes):**
  - 133 of ~138 projects multi-targeted `net48;net6.0-windows` (not a plain .NET 6 port); no `Directory.Build.props`,
    `global.json` or central package management; `UseWindowsForms` on 78 projects (incl. `MediaPortal.Common`/`Utilities`),
    `UseWPF` on 16; 23 projects pinned to `PlatformTarget=x86`.
  - Server (`Main/MediaPortal.Server`) is a WinForms `WinExe` + `ServiceBase` Windows service.
  - Win32 P/Invoke is concentrated in `MediaPortal.Utilities` (DriveUtils, NativeMethods, WindowsAPI, Impersonation*,
    network/share enumerators) and `MediaPortal.Common` (Dokan, LogonHelper, TaskWaitableTimer, registry in ApplicationCore).
  - `System.Drawing` is used in ~87 files (ImageUtilities, BaseInfo, fanart, EXIF, thumbnailers, TvdbLib, TranscodingService).
  - `SQLiteDatabase` uses `System.Data.SQLite.Core` (assumed Windows-only native; verify for Linux).
  - Paths use `CommonApplicationData` + backslash literals in the launchers; build is `.bat`/MSBuild + WiX + AppVeyor.

## Decisions (Phase 0)

- **net48 is dropped. Single target framework: `net10.0`** (`net10.0-windows` for Windows-only projects).
- `global.json` pins SDK 10; `MediaPortal/Directory.Build.props` sets `EnableWindowsTargeting` + analyzers;
  `Directory.Build.targets` suppresses WFO1000 (WinForms designer analyzer) for legacy forms.
- TV is out of scope: `SlimTv.Service3` (TvEngine3 .NET Framework binaries) was removed from `MP2-Server.sln`.
- `Setup/*` (WiX, bootstrapper, `CustomActions`) still targets net48/net6 and is **not** migrated yet.

## Architecture decisions

1. **Keep MPF and the skins; replace the rendering backend underneath.** Introduce a rendering abstraction
   below MPF and implement it with SkiaSharp (GPU via OpenGL). Avalonia migration was rejected:
   it would mean rewriting the SkinEngine and converting all skins.
2. **Window, GL context, input:** Silk.NET (GLFW backend). SDL3 is the alternative.
3. **Video:** libmpv render API, rendering into an offscreen GL texture that the UI uses as a brush.
   LibVLC was the fallback option (better DVD menus, weaker frame-level compositing).
   DirectShow may remain as an optional Windows-only player plugin during the transition.
4. **Player abstraction:** current video player interfaces expose SharpDX surfaces/textures to the SkinEngine.
   Replace with a backend-neutral "video frame source" contract so mpv and legacy players can coexist.
5. **Audio:** keep BASS, swap the BASS.NET wrapper for ManagedBass; WASAPI/ASIO become Windows-only options.
   Check BASS licensing for Linux distribution.
6. **HLSL effects** (transitions, image effects) must be ported to SkSL runtime effects or GLSL.

## Proof of concept – results

A standalone PoC (`MpvSkiaPoc`, separate from the MP2 code) validated decisions 1–3 on Windows 11
(AMD CPU/iGPU + Nvidia dGPU): playback works well, and the overlay/OSD composites correctly over video.

What the PoC proved works:
- Silk.NET OpenGL 3.3 core context shared by libmpv and Skia.
- mpv renders into an FBO-backed texture; Skia wraps it via `SKImage.FromTexture`, and it's usable as a brush:
  fullscreen, scaled preview tile with rounded clip and shadow, mirrored reflection, blurred frosted-glass OSD.
- Animated transitions while video plays; layout in a virtual 1920x1080 space scaled uniformly (like MP2 skins).

Technical learnings to carry over:
- Order per frame: reset GL state → mpv renders to FBO (only when the update callback signalled a new frame,
  or forced after FBO recreation) → `GRContext.ResetContext()` → Skia draws → flush → swap → `mpv_render_context_report_swap`.
- Before handing GL to mpv: bind FBO 0, disable scissor/stencil/depth/blend/cull, `UseProgram(0)`,
  reset pixel-store alignment and row length.
- Render to FBO with `MPV_RENDER_PARAM_FLIP_Y = 0` and wrap the texture with `GRSurfaceOrigin.TopLeft`.
- `MPV_RENDER_PARAM_BLOCK_FOR_TARGET_TIME = 0` by default so the UI loop isn't blocked by video timing.
- `mpv_opengl_init_params`: pass a third NULL pointer (extra_exts) so libmpv API 1.x and 2.x both work.
- Resolve libmpv per platform with `NativeLibrary.SetDllImportResolver`
  (`libmpv-2.dll`, `libmpv.so.2`/`.so.1`, `libmpv.2.dylib`).
- Package versions used: Silk.NET 2.21.0, SkiaSharp 2.88.8 (+ SkiaSharp.NativeAssets.Linux).
- SkiaSharp 2.88: `SKFont.MeasureText` has no `string` overload; use `SKPaint.MeasureText` (set Typeface/TextSize from the font).
- Hybrid GPU systems: the rendering GPU should be the one driving the display; check `GL_RENDERER`.

The PoC source lives in `poc/MpvSkiaPoc/` as a reference implementation.

## Roadmap

| Phase | Scope |
|---|---|
| 0 | Upgrade to .NET 8/10 LTS; split projects into portable (`net8.0`) vs Windows-only (`net8.0-windows`); enable CA1416 platform analyzer; Linux CI build |
| 1 | Server on Linux/Docker |
| 2 | Rendering abstraction under MPF + Skia/Silk.NET backend; default skin renders correctly |
| 3 | Player abstraction refactor + libmpv video player plugin |
| 4 | Audio via ManagedBass, input (incl. libCEC), display/refresh-rate switching, packaging |

## Next steps (start here)

1. Verify the "current state" assumptions above against the actual code. Correct this file where they're wrong.
2. Produce a dependency inventory: per project, list Windows-only APIs and native dependencies
   (SharpDX, DirectShow, WinForms, System.Drawing, P/Invoke to Win32, registry, COM).
   Use CA1416 warnings from a build targeting plain `net8.0` as the primary source.
3. Classify each project as portable / needs abstraction / Windows-only plugin.
4. Based on that, propose the concrete interface for the rendering abstraction (what MPF actually calls today)
   and for the video frame source. Discuss before implementing.

## Working conventions

- Work on a dedicated branch; small, reviewable commits.
- The existing Windows client must keep building and running throughout. The DirectX backend stays
  until the Skia backend is at parity.
- Don't modify skin files to make the new backend work; fix the backend instead. Skin compatibility is the point.
- Update the status section of this file at the end of each session.

## Phase 1 (server on Linux) - progress

- `MediaPortal.Server.Hosting` (`net10.0`): `ServerRuntime` = the server life cycle (services, plugins, backend, IPC, suspend level) without UI.
  Used by `MP2-Server.exe` (WinForms/Windows service, now thin wrappers) and by the new `MP2-Server-Host` (`net10.0` generic host: console/SIGTERM,
  systemd `Type=notify`, Windows service, Docker; see `Main/MediaPortal.Server.Host/Deployment`).
- `SQLiteDatabase` plugin moved from System.Data.SQLite (Windows-only natives) to Microsoft.Data.Sqlite; natives for all RIDs are copied into the plugin folder and
  resolved by `SQLiteNativeLibrary`. GUIDs are still stored as 16 byte blobs; PRAGMAs replace the former connection string options.
- `PathManager`: path patterns (`<DATA>Config`) are converted to the platform separator; `COMMON_APPLICATION_DATA` is the per-user data folder off Windows.
- Verified on Windows: new host starts, creates the database schema, loads plugins, reaches `Running`, shuts down cleanly via IPC; `MP2-Server.exe -c` still reaches `Running`.
  **Not run on Linux.**
- Open for Phase 1: local file system / `ResourcePath` handling for POSIX paths (share import), native libraries per RID (MediaInfo, FreeImage, OpenCV, FFmpeg), skipping
  Windows-only plugins cleanly in the portable host, verifying an existing System.Data.SQLite database with the new provider, Linux run + Docker image.

## Status

- [x] Architecture evaluation
- [x] PoC: libmpv + SkiaSharp + Silk.NET compositing validated on Windows 11
- [x] Phase 0a (branch `phase0-net10`): all projects retargeted to `net10.0-windows`/`net10.0`; `MP2-Server.sln`,
      `MP2-Client.sln`, `MP2-ServiceMonitor.sln` build with 0 errors on Windows (SDK 10.0.400). Fixes needed beyond the TFM:
      RID `win7-*` → `win-*` (Client), `BinaryFormatter` removed (unused `BinaryCacheProvider` deleted,
      `DiskCachedDictionary` → System.Text.Json), `#if NET6_0` → `NET6_0_OR_GREATER`, BASS.NET NU1107 (direct reference).
      Not yet run: tests, the client/server themselves.
- [~] Phase 0b (in progress): `MediaPortal.Utilities`, `MediaPortal.Common` and `UPnP` are now plain `net10.0`.
      WinForms/GDI-only code moved to new `MediaPortal.Utilities.Windows` (`net10.0-windows`, in `MP2-Client.sln`;
      referenced by Client, ClientLauncher, ServiceMonitor, Server, SkinEngine). CA1416 inventory: see `PHASE0_INVENTORY.md`.
      `VersionInfo.cs` no longer stamps `SupportedOSPlatform("windows")` on every assembly (it hid all CA1416 warnings).
      `ImageUtilities` is now SkiaSharp-based (old API -> `GdiImageUtilities`). The CA1416 list in `Utilities`/`Common` is cleared (guards, Windows-only classes, null services; see `PHASE0_INVENTORY.md`)
      and CA1416 is now an error for all non-`-windows` projects. 59 of 69 server-solution projects are now plain `net10.0`
      (remaining Windows-only ones and reasons: `PHASE0_INVENTORY.md`); new portable `SkiaThumbnailProvider`; output folder no longer
      contains the TFM. Skipped for now: `MP2Extended` image port. Added `MP2-Server-Linux.slnf` (generated by `Build/generate_linux_slnf.py`) and a GitHub Actions Linux build job
      (unverified on a real Linux runner). Phase 0 remaining: Setup/WiX/AppVeyor/.bat migration, dead net48 conditionals, tests.
- [ ] Phase 0c: Setup/WiX/AppVeyor/.bat migration; remove dead `.NETFramework`-conditional ItemGroups/build.targets branches;
      Tests retargeted but not run

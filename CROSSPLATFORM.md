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
- `Setup/*` (WiX, bootstrapper app, tests) is migrated to a single net10.0-windows / x64 flavour, see "Setup (Phase 0c) - done" below. `Setup/CustomActions` (WiX Dtf, net48) is unused (all its actions are commented out in the wxs) and was removed from `MP2-Setup.sln`.

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

## Phase 2/3 plan (client GUI) - decided 2026-10-13

**Decision: the Direct3D 9 backend is dropped completely** (old MP2 versions remain available for D3D9/DirectShow). Consequences:
the existing Windows client does not need to *run* between commits of this phase (it must keep *building*); no D3D9-vs-Skia parity harness;
everything that needs a D3D9 texture (DirectShow `VideoPlayer`, `EVRPresenter`, `DirectShowWrapper`, `BDHandler`) goes away with it and is replaced by libmpv.

Measured surface of the old backend (SkinEngine: 494 files / 97k lines): `GraphicsDevice.Device` is used in only 18 files and ~100 calls
(SetRenderState 25, SetSamplerState 9, StretchRectangle 5, Get/SetRenderTarget 9, Clear/BeginScene/EndScene/Present, one DrawPrimitives);
shaders are 18 `.fx` files / 1,530 lines of HLSL; the 140 files with `using SharpDX` mostly use only `SharpDX.Mathematics` (Vector2/Matrix/RectangleF/Color4),
which is pure managed and stays for now (replace by System.Numerics later, optional).

Approach: keep tessellation (`PositionColoredTextured` vertex lists) and the control/brush/effect model; replace the device layer.
- `PrimitiveBuffer` -> `SKVertices` + `DrawVertices`; brushes -> `SKShader`; `.fx` effects -> SkSL `SKRuntimeEffect`; render targets -> `SKSurface`; images -> `SKCodec`; text -> Skia typefaces.
- A thin D3D9-shaped facade (`Texture`, `Surface`, `Format`, `PrimitiveType`, ...) in `SkinEngine/Gpu` lets the 140 files compile unchanged; it is then simplified.
- Window/GL context/input via Silk.NET (`IWindowHost`) replaces WinForms `MainForm`; Windows-only input (raw input, APPCOMMAND, HID/IR) stays in a Windows plugin.
- Video: backend-neutral `IVideoFrameSource` replaces `ISharpDXVideoPlayer`; new portable libmpv player plugin ported from the PoC. `ImagePlayer` becomes portable; BASS -> ManagedBass in Phase 4.

Steps: 2.1 SkinEngine off WinForms/D3D9 compile (facade + stubs) -> 2.2 Silk.NET window + GL/Skia device, first frame -> 2.3 vertices/brushes/images/render targets -> 2.4 text -> 2.5 SkSL effects/transitions
-> 2.6 input -> 3.1 `IVideoFrameSource` + mpv plugin -> 3.2 remove DirectShow players, port ImagePlayer. Milestone 1: default skin main menu renders on Windows. Milestone 2: video with OSD overlay. Milestone 3: same on Linux.

### Phase 2 progress (2026-10-13)

Done (builds, **not yet run**):
- `MediaPortal.UI` and `Configuration` are plain `net10.0` (WinForms `Message`/`MouseButtons`/`NativeWindow` replaced by `WindowsMessage`, own `MouseButtons`, message-queue based `DeviceVolumeMonitor`).
- **`SkinEngine` is plain `net10.0`**: D3D9 device layer replaced by `Gpu/` (`Texture`, `Surface`, `Effect` = SkSL runtime effect, `SamplerState`...), `DirectX/GraphicsDevice` now owns the Skia `GRContext`,
  the back buffer (`GRBackendRenderTarget`) and the draw state (current shader, matrix, blend state); `PrimitiveBuffer` draws `SKVertices`; render targets are `SKSurface`s; images are decoded with Skia (`ImageUtilities.LoadBitmap`);
  text uses Skia fonts (`FontAssetCore` = metrics only, `TextBuffer` draws lines with clip/scroll/fade; FreeType wrapper removed); `System.Drawing.Drawing2D` paths replaced by `DirectX/Triangulate/Drawing2D.cs`.
- `GUI/MainWindow` (Silk.NET window + GL context, input, screen saver, window modes) replaces `MainForm`; the client runs `IScreenControl.RunMainLoop()` instead of `Application.Run()`.
- `ISharpDXVideoPlayer` -> `IVideoFrameSource` (+ `IVideoFrameOverlays`), `ISharpDXImagePlayer` -> `IImageFrameSource`; `IAutoRepeatPlayer` added.
- Removed from `MP2-Client.sln` (sources kept in the repo for porting): DirectShow `VideoPlayers`, `BDHandler`, `DirectShowWrapper`, `DotNetStreamSource`, `VideoEffectChanger`, TV client (`SlimTv.Client`, `SlimTv.TvMosaicProvider`),
  `BluRayPlayer`, `Cinema`, `UPnPRenderer`, `WifiRemote`, `Tests.Client`, `Tests.TVMosaic`.
- Windows-only input plugins (`HidInput`, `InputDeviceManager`, `MceRemoteReceiver`, ...) are `net10.0-windows` with explicit WinForms; they receive `WindowsMessage` via `WindowsMessageExtensions`.

**Milestone 1 reached (2026-10-13, Windows): the client starts and the Nereus skin renders (configuration screen with images, text, blur, gradients) via Skia/OpenGL.**
- All 35 HLSL shaders were ported to SkSL (`*.sksl`, shared prelude `Gpu/EffectPrelude.cs`; 61 effect/transform/transition combinations compile). This needs SkiaSharp 3.116 (modern SkSL); the repo now uses SkiaSharp 3.116.1.
- Native GLFW/Skia libraries of the plugin directory are preloaded by `NativeLibraryHelper` (plugin assemblies are not probed by the runtime).
- New plugin `MpvPlayer` (same plugin id as the old `VideoPlayers`, so skins keep their dependency); its builder is a stub until Phase 3.1.

**Phase 3.1 (libmpv player) - Windows verified (2026-10-13): a 1920x816 HD trailer plays in the Nereus fullscreen video screen (picture identical to mpv's own frame, pause overlay works); closing the player context and playing again works.**
- `MpvPlayer` plugin: `MpvPlayer` implements `IVideoFrameSource`, `ISubtitlePlayer`, `IChapterPlayer`, `ITitlePlayer`, `IResumablePlayer`, `IMediaPlaybackControl`, `IAutoRepeatPlayer`, `IReusablePlayer`, `IPlayerEvents` (video only; audio files stay with the audio player). libmpv renders into an OpenGL texture (`VideoTarget`) at the beginning of each SkinEngine frame (`VideoRenderHost.SceneBegin`), `Texture.FromOpenGlTexture` wraps it for Skia without copy. Audio goes through mpv's own audio output.
- libmpv is not in the repository (120 MB): `build.targets` copies `libmpv-2.dll` from `native\win-x64\` of the plugin project (git-ignored), `-p:MpvNativeDir=...` or the PoC folder. Linux uses the system `libmpv2`.
- Not verified yet: audio output, seeking/resume, audio/subtitle stream switching, DVD (`.ifo` -> `dvd://`; `IDVDPlayer` menu navigation is not implemented), Bluray, network/streamed resources (via `LocalFsResourceAccessorHelper`), hardware decoding choice, Linux, the OSD of the skins while playing.

Open (next steps, in order):
2. Look at the remaining runtime issues (CloseTopmostDialog parameter conversion, "Cannot open texture", path separators), check other skins, fullscreen/window modes, input.
3. Win32 message hook for Windows-only features (`WM_INPUT`/HID, `WM_APPCOMMAND`, single-instance message, touch), kerning (HarfBuzz) and skin sounds on other platforms.
4. Verify and complete the libmpv player (see above), port `ImagePlayer` details, TV client + Bluray/Cinema/UPnP renderer on top of it.

## Working conventions

- Work on a dedicated branch; small, reviewable commits.
- The existing Windows client must keep building throughout. (Phase 2/3: D3D9 backend is dropped, so it may not run between commits, see "Phase 2/3 plan".)
- Don't modify skin files to make the new backend work; fix the backend instead. Skin compatibility is the point.
- Update the status section of this file at the end of each session.

## Decision: Windows is x64 only

x86 is dropped. Done: the `|x86` build configurations were removed from 86 SDK-style csprojs and from `MP2-Server.sln`, `MP2-Client.sln`, `MP2-ServiceMonitor.sln`;
default platform of the few projects that defaulted to x86 is now AnyCPU; the Windows executables (`MP2-Server.exe`, `MP2-Client.exe`, `MP2-ClientLauncher.exe`,
`MP2-ServiceMonitor.exe`) are built with `PlatformTarget=x64` (verified in the PE header), the client's `RuntimeIdentifiers` is `win-x64`. Libraries and the portable
`MP2-Server-Host` stay AnyCPU (arm64 Linux must keep working).
Part 3 done with the Setup migration: `Build/Build.proj` (no duplicate x64 client/OnlineVideos build, setup with `Platform=x64`), x86 native copies removed from the
plugin `build.targets` (Bass, FFMpeg, FreeImage, Matroska, MediaInfo, OpenCV, VideoPlayer, SkinEngine), the client's second `x64` platform and the `MP2-Client (x64).exe` copy removed.
Still open: `win-x86`/`linux-x86`/arm runtime folders of SQLite and Skia, legacy non-SDK projects (Tools, `ShellThumbnailProvider`, old DB plugins, not part of any solution).

## Setup (Phase 0c) - done

- One flavour: `MP2-Setup.wixproj` + `MP2-Bootstrapper.wixproj` (WiX 4.0.4), x64 (`Platforms=x64`, `ProgramFiles64Folder`, bundle registry searches `always64`, `InstallerVersion=500`).
  Removed: `*-NetCore.wixproj`, `ReferencedTargetFramework`/`IsNetCore` switches, net48 BA host, TVE3 (`SlimTv.Service3`) feature, the `MP2-Client (x64)` components/shortcuts.
- Bundle prerequisites: .NET 10.0.12 Desktop Runtime x64 (primary prereq) + ASP.NET Core 10.0.12 runtime x64 (UPnP uses `Microsoft.AspNetCore.App`), `Fragments/NetCore/NetCore10.0.12_x64.wxs`
  (URLs/SHA512 from Microsoft's release metadata, sizes from HTTP headers; file name of the helper include still `NetCore6_Platform.wxi`).
- `MP2BootstrapperApp` and `Setup/Tests` are `net10.0-windows`. Fixed on the way: `MediaPortal.Client/build.targets` copied `Docs`/`Defaults`/splash into a `net10.0-windows` subfolder.
- `appveyor.yml` now archives `Bin\MP2-Setup\Release\en-us\MP2-Setup.exe` only.
- Verified: `dotnet build MP2-Setup.sln -c Release -p:Platform=x64` (after Release builds of the three solutions) produces `MP2-Setup.msi` (template `x64`) and the 250 MB bundle `MP2-Setup.exe`. NOT verified: running the installer, upgrade from an old x86 install
  (same UpgradeCode, but different install folder/registry view, so previous install-dir/shortcut choices are not picked up), `Build.proj`/`.bat` flow (needs VS MSBuild, `AssemblyInfoHelper`, Transifex), the BA on a machine that has only .NET 10.
- `Setup/Tests`: 68/69 pass; `ConverterTests.ReturnAppropriateFileSizeUnit` fails on a German culture ("2,1 GB" vs "2.1 GB").
- Still x86-era leftovers in the bundle: VC++ 2008/2010/2013/2015 x86 redists (`VCRedists.wxs`, `PackageId`/`PlanContext` in the BA and its tests). Trim once we know which native libs still need them.

## Phase 1 (server on Linux) - progress

- `MediaPortal.Server.Hosting` (`net10.0`): `ServerRuntime` = the server life cycle (services, plugins, backend, IPC, suspend level) without UI.
  Used by `MP2-Server.exe` (WinForms/Windows service, now thin wrappers) and by the new `MP2-Server-Host` (`net10.0` generic host: console/SIGTERM,
  systemd `Type=notify`, Windows service, Docker; see `Main/MediaPortal.Server.Host/Deployment`).
- `SQLiteDatabase` plugin moved from System.Data.SQLite (Windows-only natives) to Microsoft.Data.Sqlite; natives for all RIDs are copied into the plugin folder and
  resolved by `SQLiteNativeLibrary`. GUIDs are still stored as 16 byte blobs; PRAGMAs replace the former connection string options.
- `PathManager`: path patterns (for example `<DATA>\Config`) are converted to the platform separator; `COMMON_APPLICATION_DATA` is the per-user data folder off Windows.
- Verified on Windows: new host starts, creates the database schema, loads plugins, reaches `Running`, shuts down cleanly via IPC; `MP2-Server.exe -c` still reaches `Running`.
  **Not run on Linux.**
- Local file system provider handles POSIX paths: provider paths are `//home/user/Music` (root `//`), the virtual root `/` has the single child `//` off Windows (drives on Windows),
  `ToDosPath` rejects non-rooted results, change tracking compares paths case-sensitively, `FileWatcher` no longer requires NTFS off Windows. `ResourcePath` (de)serialization
  of such paths is verified; the provider itself was not run on Linux. Remaining: `ResourcePath.Equals` is still case-insensitive.
- Plugin descriptors can declare `Platforms="Windows"` (also `Linux`, `macOS`, comma separated); `PluginManager` does not enable such plugins on other platforms (info log; dependents are
  rejected via the dependency check). Set for GDI/WIC thumbnails, NetworkNeighborhood, MP2Extended, SlimTv.Service35, Tve3Recording, BassAudioMetadataExtractor, AudioCDResourceProvider.
  `BassLibraries` stays platform neutral because `AudioMetadataExtractor` (TagLib based) references it. The mechanism is verified (descriptor parsing, check on Windows); the skipping itself was not seen on Linux.
- Open for Phase 1: native libraries per RID (MediaInfo, FreeImage, OpenCV, FFmpeg), skipping
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
- [~] Phase 0c: Setup/WiX/AppVeyor done (see "Setup (Phase 0c) - done"); open: `.bat`/`Build.proj` flow, remove dead `.NETFramework`-conditional ItemGroups/build.targets branches,
      trim x86 VC redists, run the other tests (only Setup/Tests were run)

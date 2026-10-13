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
- **These points are assumptions from documentation, not yet verified against the code.** Verify first.

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

## Status

- [x] Architecture evaluation
- [x] PoC: libmpv + SkiaSharp + Silk.NET compositing validated on Windows 11
- [ ] Phase 0: assumptions verified, dependency inventory

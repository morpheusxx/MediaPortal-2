# MP2 cross-platform PoC: libmpv + SkiaSharp + Silk.NET

Standalone proof of concept for the proposed MediaPortal 2 client stack. It does not use any MP2 code.
It answers one question: can a portable GPU UI layer composite a skin-like UI over, around and with video,
with smooth frame pacing and hardware decoding, on Windows and Linux?

| PoC component | Replaces in MP2 |
|---|---|
| Silk.NET window, GL context, input | WinForms host form, DirectX device setup, key handling |
| `MpvPlayer` (libmpv render API) | DirectShow graph + native EVR presenter |
| `VideoTarget` (GL texture wrapped as `SKImage`) | Video surface handed to the SkinEngine / video brush |
| `SceneRenderer` (SkiaSharp) | SkinEngine DirectX rendering primitives below MPF |

What the scene exercises: fullscreen video with OSD, an animated transition into a "home screen" where the
video becomes a rounded preview tile with shadow and a mirrored reflection, a frosted-glass OSD made from the
blurred video, an animated background, focus animation, text, and a stats panel with a constant-speed ticker
that makes stutter visible.

> This code was written without being compiled (no .NET SDK or network in my environment).
> Expect a few small fixes on first build; paste compiler errors and I'll correct them.

## Windows 11

1. Install the .NET 8 SDK.
2. Download a libmpv dev build: `mpv-dev-x86_64-<date>-git-<hash>.7z` from the releases of
   <https://github.com/shinchiro/mpv-winbuild-cmake>. (The `x86_64-v3` variant is fine on modern AMD CPUs.)
3. Create a folder `native` next to `MpvSkiaPoc.csproj` and copy `libmpv-2.dll` into it.
4. Build and run:

   ```
   dotnet run -c Release -- "D:\Videos\test.mkv"
   ```

### Hybrid GPU (AMD iGPU + Nvidia card)

The **GL** line in the stats panel shows which GPU renders. To choose:
Settings → System → Display → Graphics → add `bin\Release\net8.0\MpvSkiaPoc.exe` → Options → pick the GPU.
Best results come when the GPU that renders is the one the monitor is plugged into. Otherwise every frame
crosses the PCIe bus.

### hwdec experiments

The default is `hwdec=auto-safe`. Check the **hwdec** line; `no` means software decoding. Try:

```
dotnet run -c Release -- video.mkv --mpv hwdec=nvdec          # Nvidia, CUDA/GL interop
dotnet run -c Release -- video.mkv --mpv hwdec=dxva2           # DXVA2 via WGL_NV_DX_interop
dotnet run -c Release -- video.mkv --mpv hwdec=d3d11va-copy    # works anywhere, costs a copy
```

## WSL (Linux path check only)

WSLg renders OpenGL through Mesa's D3D12 driver, so this proves the Linux build and runtime path but says little
about real Linux performance. Hardware decoding is usually unavailable there.

```
sudo apt install dotnet-sdk-8.0 libmpv2 libfontconfig1 libgl1 libx11-6 libxrandr2 libxi6 libxcursor1 libxinerama1
# Ubuntu 22.04: libmpv1 instead of libmpv2
dotnet run -c Release -- /mnt/c/Users/<you>/Videos/test.mkv
```

The GL line should show something like `D3D12 (NVIDIA ...)`. If it shows `llvmpipe`, rendering is on the CPU.

## Controls

| Key | Action |
|---|---|
| Space | Pause / play |
| Left / Right | Seek ±10 s (fullscreen mode) |
| Up / Down | Seek ±60 s, or move focus in home mode |
| Enter / M | Toggle fullscreen video ↔ home screen with preview |
| O / S | Toggle OSD / stats |
| F / F11 | Toggle window fullscreen |
| Q / Esc | Quit |

Other options: `--fullscreen`, `--mpv key=value` (repeatable), `--block` (mpv waits for the frame's target time),
`--display-sync` (experimental `video-sync=display-resample`).

## What to evaluate

Every 5 seconds a `[stats]` line is printed to the console, which is easy to copy into a report.

Suggested test clips: 1080p H.264 8-bit, 4K HEVC 10-bit, 4K HDR (gamma `pq`), 23.976 fps on a 60 Hz display,
and interlaced material with `--mpv deinterlace=yes`.

For each clip, check:

- **hwdec**: is it something other than `no`?
- **Render fps** at the display refresh rate, with few **hitches** and a smoothly moving ticker.
- **Video new frames/s** close to the source fps, with drop counters not increasing.
- **CPU mpv / skia ms** low. This is CPU submission time only, not GPU time.
- **Transitions**: Enter while playing. The animation should stay smooth while video plays inside the moving tile.
- **Fullscreen** (F) at native resolution, where GPU load is highest.

## Known limitations

- Video is rendered at window size and letterboxed by mpv; in the preview tile it is scaled down by Skia.
  A production backend would render at the target rect size.
- No HDR output: HDR is tone-mapped to SDR by mpv.
- Audio is played by mpv here. The BASS-based music player is outside this PoC.
- If the video appears upside down, flip `GRSurfaceOrigin.TopLeft` to `BottomLeft` in `VideoTarget.CreateImage`.

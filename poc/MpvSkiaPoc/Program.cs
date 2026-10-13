using MpvSkiaPoc;
using MpvSkiaPoc.Interop;

var options = AppOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine("""
        Usage: MpvSkiaPoc <file-or-url> [options]

          --fullscreen          start fullscreen
          --mpv key=value       pass an mpv option (repeatable), e.g. --mpv hwdec=nvdec
          --block               mpv render blocks until the frame's target time
          --display-sync        experimental: video-sync=display-resample (implies --block)
        """);
    return 1;
}

LibMpv.InstallResolver();

try
{
    new App(options).Run();
    return 0;
}
catch (DllNotFoundException ex)
{
    Console.Error.WriteLine($"Could not load libmpv: {ex.Message}");
    Console.Error.WriteLine(OperatingSystem.IsWindows()
        ? "Put libmpv-2.dll into the 'native' folder next to the .csproj and rebuild (see README)."
        : "Install libmpv (e.g. 'sudo apt install libmpv2' or 'libmpv1').");
    return 2;
}

namespace MpvSkiaPoc
{
    internal sealed record AppOptions(
        string Media,
        List<KeyValuePair<string, string>> MpvOptions,
        bool Fullscreen,
        bool BlockForTargetTime)
    {
        public static AppOptions? Parse(string[] args)
        {
            string? media = null;
            var mpv = new List<KeyValuePair<string, string>>();
            bool fullscreen = false, block = false;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "--fullscreen": fullscreen = true; break;
                    case "--block": block = true; break;
                    case "--display-sync":
                        mpv.Add(new("video-sync", "display-resample"));
                        block = true;
                        break;
                    case "--mpv":
                        if (i + 1 >= args.Length) return null;
                        string kv = args[++i];
                        int eq = kv.IndexOf('=');
                        if (eq <= 0) return null;
                        mpv.Add(new(kv[..eq], kv[(eq + 1)..]));
                        break;
                    default:
                        if (a.StartsWith("--"))
                        {
                            Console.Error.WriteLine($"Unknown option {a}");
                            return null;
                        }
                        media = a;
                        break;
                }
            }
            return media is null ? null : new AppOptions(media, mpv, fullscreen, block);
        }
    }
}

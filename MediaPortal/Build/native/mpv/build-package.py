#!/usr/bin/env python3
"""Creates the NuGet package MediaPortal.Native.Mpv from a build of https://github.com/shinchiro/mpv-winbuild-cmake.

The upstream project keeps only about the last 30 daily builds, so the binaries cannot be referenced directly. This script downloads a
build, repackages the libmpv library as runtimes/win-x64/native/libmpv-2.dll and packs it, so it can be published on the Team MediaPortal
feed and referenced by a fixed version. Linux uses the libmpv of the distribution and is not part of the package.

    python build-package.py [--tag 20261021] [--out feed]      (default tag: latest release, default output: ./feed)

The package version is derived from the build date (tag 20261021 -> 2026.10.21). Requires 7-Zip (7z/7zz/7za on PATH or the default
Windows location) or the Python package py7zr, and the .NET SDK. Publish with:  dotnet nuget push feed/MediaPortal.Native.Mpv.<version>.nupkg -s <feed>
Before using a new build, run the playback smoke test (play, stop, replay a video) in the client.
"""
import argparse, hashlib, json, os, re, shutil, subprocess, sys, tempfile, urllib.request

REPO = 'shinchiro/mpv-winbuild-cmake'
HERE = os.path.dirname(os.path.abspath(__file__))


def get_json(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'MediaPortal-build'})
    with urllib.request.urlopen(request) as response:
        return json.load(response)


def find_7z():
    for name in ('7z', '7zz', '7za'):
        path = shutil.which(name)
        if path:
            return path
    for path in (r'C:\Program Files\7-Zip\7z.exe', r'C:\Program Files (x86)\7-Zip\7z.exe'):
        if os.path.exists(path):
            return path
    return None


def extract(archive, target):
    exe = find_7z()
    if exe:
        subprocess.run([exe, 'x', '-y', '-o' + target, archive], check=True, stdout=subprocess.DEVNULL)
        return
    try:
        import py7zr
    except ImportError:
        sys.exit('7-Zip (7z/7zz/7za) or the Python package py7zr is required to extract the archive')
    with py7zr.SevenZipFile(archive) as z:
        z.extractall(target)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--tag', help='release tag of the upstream build, for example 20261021 (default: latest)')
    parser.add_argument('--out', default=os.path.join(HERE, 'feed'), help='output folder of the package')
    args = parser.parse_args()

    url = 'https://api.github.com/repos/%s/releases/%s' % (REPO, 'tags/' + args.tag if args.tag else 'latest')
    release = get_json(url)
    tag = release['tag_name']
    asset = next((a for a in release['assets'] if re.match(r'^mpv-dev-x86_64-\d+-git-[0-9a-f]+\.7z$', a['name'])), None)
    if asset is None:
        sys.exit('No generic x86_64 mpv-dev asset in release ' + tag)
    commit = re.search(r'-git-([0-9a-f]+)\.7z$', asset['name']).group(1)
    version = '%s.%d.%d' % (tag[0:4], int(tag[4:6]), int(tag[6:8]))
    print('Upstream build %s (mpv commit %s) -> package version %s' % (tag, commit, version))

    work = tempfile.mkdtemp(prefix='mpvpkg')
    try:
        archive = os.path.join(work, asset['name'])
        print('Downloading', asset['browser_download_url'])
        urllib.request.urlretrieve(asset['browser_download_url'], archive)
        sha256 = hashlib.sha256(open(archive, 'rb').read()).hexdigest()
        print('SHA-256 of the upstream archive:', sha256)

        unpacked = os.path.join(work, 'unpacked')
        extract(archive, unpacked)
        dll = os.path.join(unpacked, 'libmpv-2.dll')
        if not os.path.exists(dll):
            sys.exit('libmpv-2.dll not found in the archive')

        staging = os.path.join(HERE, 'staging')
        shutil.rmtree(staging, ignore_errors=True)
        native = os.path.join(staging, 'runtimes', 'win-x64', 'native')
        os.makedirs(native)
        shutil.copy(dll, native)
        with open(os.path.join(staging, 'NOTICE.txt'), 'w', encoding='utf-8') as f:
            f.write(
                'libmpv for MediaPortal 2\n\n'
                'The file runtimes/win-x64/native/libmpv-2.dll is an unmodified build of mpv (https://mpv.io, https://github.com/mpv-player/mpv)\n'
                'and its dependencies (FFmpeg, libass, ...) made by https://github.com/%s.\n'
                'Upstream release: %s, mpv commit %s, archive %s\n'
                'SHA-256 of the archive: %s\n\n'
                'The libraries are distributed under the licenses of the respective projects (mpv: GPLv2+/LGPLv2.1+ depending on the build options,\n'
                'FFmpeg: LGPL/GPL). The source code is available at the repositories above.\n'
                % (REPO, tag, commit, asset['name'], sha256))

        os.makedirs(args.out, exist_ok=True)
        subprocess.run(['dotnet', 'pack', os.path.join(HERE, 'MediaPortal.Native.Mpv.csproj'), '-c', 'Release', '-nologo', '-v', 'q',
                        '-p:Version=' + version, '-p:PackageReleaseNotes=Upstream build %s (mpv %s)' % (tag, commit),
                        '-o', os.path.abspath(args.out)], check=True)
        print('Package written to', os.path.join(args.out, 'MediaPortal.Native.Mpv.%s.nupkg' % version))
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == '__main__':
    main()

#!/usr/bin/env python3
"""Creates the NuGet package MediaPortal.Native.Bass with the native BASS libraries of Linux (x64, arm64) and macOS (x64, arm64).

The libraries are downloaded from https://www.un4seen.com/files (the current release of each component), so the package pins them at a
fixed version for the build. The Windows libraries are delivered by the package BASSCombined.

    python build-package.py [--revision 20261022] [--out feed]      (default revision: today, default output: ./feed)

Layout: runtimes/<rid>/native/libbass.so|dylib, libbassmix, ... and the decoder add-ins in runtimes/<rid>/native/Plugins.
The package version is <BASS major.minor>.0.<revision>, for example 2.4.0.20261022. Requires the .NET SDK. Publish with:
    dotnet nuget push feed/MediaPortal.Native.Bass.<version>.nupkg -s <feed>
"""
import argparse, datetime, hashlib, io, os, re, shutil, subprocess, sys, urllib.request, zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
BASE = 'https://www.un4seen.com/files/'

CORE = ['bass', 'bassmix']
# add-ins; not every add-in exists for every platform
PLUGINS = ['bassflac', 'bassopus', 'basswv', 'bassape', 'bassdsd', 'bassalac', 'bassmidi', 'basshls']

# runtime id -> (zip suffix, folder inside the zip for Linux, library extension)
LINUX = {'linux-x64': 'x86_64', 'linux-arm64': 'aarch64'}
OSX = ['osx-x64', 'osx-arm64']  # the macOS libraries are universal binaries

hashes = {}


def download(name):
    url = BASE + name
    request = urllib.request.Request(url, headers={'User-Agent': 'MediaPortal-build'})
    try:
        with urllib.request.urlopen(request) as response:
            data = response.read()
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return None
        raise
    hashes[name] = hashlib.sha256(data).hexdigest()
    return zipfile.ZipFile(io.BytesIO(data))


def extract_file(archive, member, target):
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with archive.open(member) as source, open(target, 'wb') as out:
        shutil.copyfileobj(source, out)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--revision', default=datetime.date.today().strftime('%Y%m%d'))
    parser.add_argument('--out', default=os.path.join(HERE, 'feed'))
    args = parser.parse_args()

    staging = os.path.join(HERE, 'staging')
    shutil.rmtree(staging, ignore_errors=True)
    bass_version = None

    for component in CORE + PLUGINS:
        is_plugin = component in PLUGINS
        sub = 'Plugins' if is_plugin else ''

        archive = download('%s24-linux.zip' % component)
        if archive is None:
            print('  no Linux build of', component)
        else:
            if component == 'bass':
                header = archive.read('c/bass.h').decode('latin-1')
                m = re.search(r'#define BASSVERSIONTEXT\s+"([0-9.]+)"', header)  # only major.minor, the patch level is not part of the header
                if m:
                    bass_version = m.group(1) + '.0'
            for rid, folder in LINUX.items():
                member = 'libs/%s/lib%s.so' % (folder, component)
                if member in archive.namelist():
                    extract_file(archive, member, os.path.join(staging, 'runtimes', rid, 'native', sub, 'lib%s.so' % component))

        archive = download('%s24-osx.zip' % component)
        if archive is None:
            print('  no macOS build of', component)
        else:
            member = 'lib%s.dylib' % component
            if member in archive.namelist():
                for rid in OSX:
                    extract_file(archive, member, os.path.join(staging, 'runtimes', rid, 'native', sub, 'lib%s.dylib' % component))

    if bass_version is None:
        sys.exit('BASS version not found')
    version = '%s.%s' % (bass_version, args.revision)
    print('BASS %s -> package version %s' % (bass_version, version))

    with open(os.path.join(staging, 'NOTICE.txt'), 'w', encoding='utf-8') as f:
        f.write('Native BASS libraries for MediaPortal 2\n\n'
                'The libraries (BASS, BASSmix and the decoder add-ins) are products of Un4seen Developments (https://www.un4seen.com) and unmodified copies\n'
                'of the downloads at %s. BASS is free for non-commercial use; see https://www.un4seen.com/bass.html#license for the license terms.\n\n'
                'SHA-256 of the downloaded archives:\n' % BASE +
                ''.join('  %s  %s\n' % (h, n) for n, h in sorted(hashes.items())))

    os.makedirs(args.out, exist_ok=True)
    subprocess.run(['dotnet', 'pack', os.path.join(HERE, 'MediaPortal.Native.Bass.csproj'), '-c', 'Release', '-nologo', '-v', 'q',
                    '-p:Version=' + version, '-o', os.path.abspath(args.out)], check=True)
    print('Package written to', os.path.join(args.out, 'MediaPortal.Native.Bass.%s.nupkg' % version))
    for root, dirs, files in os.walk(os.path.join(staging, 'runtimes')):
        for name in files:
            print('  ', os.path.relpath(os.path.join(root, name), staging))


if __name__ == '__main__':
    main()

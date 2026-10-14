#!/usr/bin/env python3
"""Generates Source/MP2-Server-Linux.slnf from Source/MP2-Server.sln and Source/MP2-Client-Linux.slnf from Source/MP2-Client.sln.

The filter contains every project of the solution that targets plain 'net10.0' (portable). Projects targeting a
'-windows' TFM (or still using a legacy project format) are left out. A project which selects its TFM depending on the platform
(the client: property 'MP2WindowsBuild') is included. Re-run this script after retargeting projects:

    python Build/generate_linux_slnf.py [server|client]   (default: both)
"""
import json, os, re, sys

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
source = os.path.join(root, 'Source')

CONDITIONAL_TFM = re.compile(r'<TargetFramework Condition="[^"]*MP2WindowsBuild[^"]*!=[^"]*">net10\.0</TargetFramework>')


def generate(name):
    sln = os.path.join(source, 'MP2-%s.sln' % name)
    projects = []
    with open(sln, encoding='utf-8-sig') as f:
        for m in re.finditer(r'^Project\("\{[^}]+\}"\) = "[^"]*", "([^"]+\.csproj)"', f.read(), re.M):
            projects.append(m.group(1))

    portable, excluded = [], []
    for rel in projects:
        path = os.path.normpath(os.path.join(source, rel.replace(chr(92), os.sep)))
        text = open(path, encoding='utf-8-sig').read()
        tfm = re.search(r'<TargetFramework>([^<]+)</TargetFramework>', text)
        ok = bool(tfm and tfm.group(1).strip() == 'net10.0') or bool(CONDITIONAL_TFM.search(text))
        (portable if ok else excluded).append((rel, tfm.group(1) if tfm else 'legacy'))

    out = {'solution': {'path': 'MP2-%s.sln' % name, 'projects': sorted(p for p, _ in portable)}}
    target = os.path.join(source, 'MP2-%s-Linux.slnf' % name)
    with open(target, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(out, f, indent=2)
        f.write('\n')

    print(f'{len(portable)} portable projects written to {os.path.relpath(target, root)}')
    for rel, tfm in excluded:
        print(f'  excluded ({tfm}): {rel}')


for which in sys.argv[1:] or ['Server', 'Client']:
    generate(which.capitalize())

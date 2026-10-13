#!/usr/bin/env python3
"""Generates Source/MP2-Server-Linux.slnf from Source/MP2-Server.sln.

The filter contains every project of the server solution that targets plain 'net10.0' (portable). Projects targeting a
'-windows' TFM (or still using a legacy project format) are left out. Re-run this script after retargeting projects:

    python Build/generate_linux_slnf.py
"""
import json, os, re, sys

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
source = os.path.join(root, 'Source')
sln = os.path.join(source, 'MP2-Server.sln')

projects = []
with open(sln, encoding='utf-8-sig') as f:
    for m in re.finditer(r'^Project\("\{[^}]+\}"\) = "[^"]*", "([^"]+\.csproj)"', f.read(), re.M):
        projects.append(m.group(1))

portable, excluded = [], []
for rel in projects:
    path = os.path.normpath(os.path.join(source, rel.replace(chr(92), os.sep)))
    text = open(path, encoding='utf-8-sig').read()
    tfm = re.search(r'<TargetFramework>([^<]+)</TargetFramework>', text)
    (portable if tfm and tfm.group(1).strip() == 'net10.0' else excluded).append((rel, tfm.group(1) if tfm else 'legacy'))

out = {'solution': {'path': 'MP2-Server.sln', 'projects': sorted(p for p, _ in portable)}}
target = os.path.join(source, 'MP2-Server-Linux.slnf')
with open(target, 'w', encoding='utf-8', newline='\n') as f:
    json.dump(out, f, indent=2)
    f.write('\n')

print(f'{len(portable)} portable projects written to {os.path.relpath(target, root)}')
for rel, tfm in excluded:
    print(f'  excluded ({tfm}): {rel}')

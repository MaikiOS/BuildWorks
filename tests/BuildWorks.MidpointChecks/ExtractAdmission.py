"""Link the actual admission methods to the inert hierarchy; only expose private entry points."""
import hashlib
import json
import pathlib
import sys

root = pathlib.Path(__file__).resolve().parents[2]
source_root = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else root
output = root / 'artifacts/fb-0006-20261105/supplement-p1/generated'
output.mkdir(parents=True, exist_ok=True)
pins = []

def read(name):
    path = source_root / 'src/BuildWorks' / name
    pins.append({'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest().upper()})
    return path.read_text(encoding='utf-8-sig')

def method(source, signature):
    start = source.index(signature)
    brace = source.index('{', start)
    arrow = source.find('=>', start, brace)
    if arrow >= 0:
        return source[start:source.index(';', arrow) + 1]
    depth = 0
    for index in range(brace, len(source)):
        if source[index] == '{': depth += 1
        elif source[index] == '}':
            depth -= 1
            if not depth: return source[start:index + 1]
    raise ValueError('Unbalanced method: ' + signature)

storage = read('BlueprintStorageCapture.cs')
remove = read('BlueprintConstructionCancellationRemoval.cs')
unbound = read('BlueprintUnboundImport.cs')
code = 'using System;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Globalization;using UnityEngine;using OstrixMods.BuildWorks.Geometry;\nnamespace OstrixMods.BuildWorks {\n'
code += 'internal static class BlueprintStorageCapture {\n' + method(storage, 'internal static bool? SupportsKgStateProjection(') + '\n' + method(storage, 'internal static bool KgOrdinaryComponent(') + '\n}\n'
code += 'internal static class AdmissionProbe {\n' + method(remove, 'private static bool NativeStandRoot(').replace('private static bool', 'internal static bool', 1) + '\n}\n'
code += 'internal static class BlueprintUnboundImport {\n' + method(unbound, 'private static IEnumerable<UnboundItemStandItem> ItemStandItems(').replace('private static IEnumerable', 'internal static IEnumerable', 1) + '\n}\n}\n'
(output / 'AdmissionSource.cs').write_text(code, encoding='utf-8')
pins.append({'path': 'AdmissionSource.cs', 'sha256': hashlib.sha256((output / 'AdmissionSource.cs').read_bytes()).hexdigest().upper()})
(output / 'INPUTS.json').write_text(json.dumps(pins, indent=2) + '\n', encoding='utf-8')
print('Extracted actual KG/stand/Unbound admission methods; native host boundary substituted.')

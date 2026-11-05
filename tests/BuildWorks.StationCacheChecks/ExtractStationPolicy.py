"""Extract the production component guard; geometry/placement are not simulated here."""
import hashlib
import pathlib
import sys

root = pathlib.Path(__file__).resolve().parents[2]
source = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'src/BuildWorks/BuildWorksTestStations.cs'
output = root / 'artifacts/fb-0006-station-fix-20261105/generated/StationPolicy.cs'
text = source.read_text(encoding='utf-8-sig')
anchor = 'if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component =>'
assert text.count(anchor) == 1
start = text.index(anchor)
end = text.index(';', start) + 1
guard = text[start:end]
assert 'Unreviewed station behaviour in QA prefab: ' in guard
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text('using System; using System.IO; using System.Linq; using UnityEngine;\n'
                  'namespace OstrixMods.BuildWorks { internal static class StationPolicy {\n'
                  'internal static void Check(GameObject prefab) {\n' + guard + '\n} } }\n', encoding='utf-8')
print('Production station guard extracted from SHA256 ' + hashlib.sha256(source.read_bytes()).hexdigest().upper())

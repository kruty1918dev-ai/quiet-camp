"""Validate compact composition authoring without modifying sources."""
import json,sys
from pathlib import Path
import jsonschema
schema=json.loads(Path('tools/scene-composition/scene-composition.schema.json').read_text())
if sys.argv[1]=='--catalog':
 schema_file=Path('tools/scene-composition')/sys.argv[2]
 if schema_file.name not in ('visual-assets.schema.json','ensemble-templates.schema.json'):raise SystemExit('Unknown catalog schema')
 jsonschema.Draft202012Validator(json.loads(schema_file.read_text())).validate(json.load(sys.stdin));print('PASS catalog schema');raise SystemExit(0)
if sys.argv[1]=='--stdin':
 jsonschema.Draft202012Validator(schema).validate(json.load(sys.stdin));print('PASS patch schema');raise SystemExit(0)
root=Path(sys.argv[1]);manifest=json.loads((root/'manifest.json').read_text())
jsonschema.Draft202012Validator(json.loads(Path('tools/scene-composition/manifest.schema.json').read_text())).validate(manifest)
for filename,schema_name in [(manifest['assets'],'visual-assets.schema.json'),(manifest['templates'],'ensemble-templates.schema.json')]:
 if Path(filename).name!=filename:raise SystemExit('Path traversal in catalog manifest')
 jsonschema.Draft202012Validator(json.loads((Path('tools/scene-composition')/schema_name).read_text())).validate(json.loads((root/filename).read_text()))
for filename in manifest['regions']:
 if Path(filename).name!=filename:raise SystemExit('Path traversal in manifest')
 doc=json.loads((root/filename).read_text());errors=sorted(jsonschema.Draft202012Validator(schema).iter_errors(doc),key=lambda e:list(map(str,e.path)))
 if errors:raise SystemExit('\n'.join(f'{filename}/'+('/'.join(map(str,e.path)))+': '+e.message for e in errors))
print(f'PASS composition JSON Schema: {len(manifest["regions"])} region documents')

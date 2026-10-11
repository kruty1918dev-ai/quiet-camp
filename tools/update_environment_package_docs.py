#!/usr/bin/env python3
"""Prepare per-package API guides and truthful verification receipts."""
import json
from pathlib import Path
from verify_environment_cores import ROOT,PACKAGES,SLUGS

def main():
    report=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement'
    guide=(report/'TOOLS-UA.md').read_text()
    common=guide.split('## Спільна модель роботи\n',1)[1].split('## scene-placement',1)[0]
    core=json.loads((report/'core-contracts.json').read_text())
    unity=json.loads((report/'unity-source-checks.json').read_text())
    for index,slug in enumerate(SLUGS):
        folder=PACKAGES/f'com.kruty1918.{slug}';docs=folder/'Documentation~';docs.mkdir(exist_ok=True)
        section=guide.split(f'## {slug}\n',1)[1].split('\n## ',1)[0]
        example=(folder/'Samples~/Example.cs').read_text()
        (docs/'API-UA.md').write_text(f'# {slug}: API та контракти\n\n'+common+'\n'+section+'\n\n## Мінімальний приклад\n\n```csharp\n'+example+'```\n\nNative tests/import ще Pending. Core-contract success не доводить visual acceptance.\n')
        verification=folder/'Validation~';verification.mkdir(exist_ok=True)
        entry=next(e for e in core['checks'] if e['package']==slug)
        prefix=str(folder.relative_to(ROOT))+'/'
        local_sources={k[len(prefix):] if k.startswith(prefix) else 'dependency/'+k.split('com.kruty1918.',1)[1]:v for k,v in entry['sources'].items()}
        (verification/'receipt.json').write_text(json.dumps({'captured_utc':core['captured_utc'],'core_contract':entry['assertions'],'compiler_peak_rss_kib':entry['compiler_peak_rss_kib'],'core_sources':local_sources,'cached_unity_source_assemblies':[e['assembly'] for e in unity['checks'] if e['assembly'].split('.')[1]==('ScenePlacement','MeshStyle','ModelWeathering','FenceAuthoring','RoadAuthoring','PowerLines','RockPlacement','VegetationPlacement')[index]],'cached_compiler_check':True,'unity_launched':False,'native_import_tests':'pending','quiet_camp_visual_acceptance':'pending','player_build':False},indent=2)+'\n')
        readme=folder/'README.md';text=readme.read_text().split('\nДокладні parameters, принцип роботи й обмеження:',1)[0];text+='\nДокладні parameters, принцип роботи й обмеження: [API-UA](Documentation~/API-UA.md).\n[Receipt](Validation~/receipt.json) відокремлює Core/cached compilation від native acceptance.\n'
        readme.write_text(text)
    print('Prepared eight API guides and verification receipts; native acceptance remains pending.')

if __name__=='__main__':main()

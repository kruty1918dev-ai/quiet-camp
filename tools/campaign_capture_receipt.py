"""Check that a native frame still represents its draft and loaded source files."""
import hashlib,json
from pathlib import Path

_hashes={}

def sha(path):
    path=Path(path);stat=path.stat();key=(str(path),stat.st_mtime_ns,stat.st_size)
    if key not in _hashes:_hashes[key]=hashlib.sha256(path.read_bytes()).hexdigest()
    return _hashes[key]

def current(project,image,receipt,draft):
    try:
        evidence=json.loads(Path(receipt).read_text(encoding='utf-8'))
        if evidence.get('kind')!='native-unity-production-world-components' or evidence.get('errors') or evidence.get('shaderErrors'):return False
        if sha(image)!=evidence.get('imageSha256') or sha(draft)!=evidence.get('draftSha256'):return False
        source=evidence.get('sourceReceipt',{})
        if not source.get('sources') or not source.get('assemblies'):return False
        if any(sha(resolve_source(project,row['path']))!=row['sha256'] for row in source['sources']):return False
        return all(sha(Path(project)/'Library/ScriptAssemblies'/(row['name']+'.dll'))==row['sha256'] for row in source['assemblies'])
    except (OSError,ValueError,KeyError):return False

def resolve_source(project,relative):
    project=Path(project);path=project/relative
    if path.exists() or not relative.startswith('Packages/'):return path
    package,tail=relative.removeprefix('Packages/').split('/',1)
    lock=json.loads((project/'Packages/packages-lock.json').read_text(encoding='utf-8'))['dependencies'][package]
    candidates=[]
    for folder in (project/'Library/PackageCache').glob(package+'@*'):
        metadata=json.loads((folder/'package.json').read_text(encoding='utf-8'))
        if metadata.get('name')==package and metadata.get('version')==lock['version'] and (folder/tail).exists():candidates.append(folder/tail)
    if len(candidates)!=1:raise ValueError('Cannot resolve one locked package source: '+relative)
    return candidates[0]

"""WorldOps transport for the existing QuietCamp Editor. Editing authority stays in C#."""
import argparse
import json
import os
from pathlib import Path
import sys
import time
import uuid
import hashlib
import subprocess
import tempfile
from collections import Counter


def watch(directory, seconds=1800):
    """Observe the C# journal; never launch Unity or decide whether art is acceptable."""
    deadline = time.monotonic() + seconds
    previous = None
    while time.monotonic() < deadline:
        try:
            run = json.loads((directory / 'run.json').read_text(encoding='utf-8-sig'))
        except (FileNotFoundError, PermissionError, json.JSONDecodeError):
            time.sleep(.5); continue
        counts = dict(Counter(item['status'] for item in run['levels']))
        progress = (run['status'], counts, run.get('nativeLevel'), run.get('nativeProfile'), run.get('nativePhase'))
        if progress != previous:
            print(json.dumps({'runStatus': run['status'], 'levels': counts, 'level': run.get('nativeLevel'),
                              'profile': run.get('nativeProfile'), 'phase': run.get('nativePhase')}, ensure_ascii=False), flush=True)
            previous = progress
        if run['status'] not in ('preparing', 'running', 'capturing', 'applying', 'cancelling'):
            print(json.dumps({'runId': run['id'], 'report': str(directory / 'index.html'), 'error': run.get('error')}, ensure_ascii=False), flush=True)
            return 1 if run['status'] == 'error' else 0
        time.sleep(1)
    raise SystemExit('Watch timed out. The Editor job continues; inspect ' + str(directory / 'index.html'))


def git(repo, *args):
    return subprocess.run(['git', '-C', str(repo), *args], check=True, capture_output=True).stdout


def dirty_paths(repo):
    paths = set()
    for args in [('diff', '--name-only', '-z'), ('diff', '--cached', '--name-only', '-z'),
                 ('ls-files', '--others', '--exclude-standard', '-z')]:
        paths.update(p.decode('utf-8') for p in git(repo, *args).split(b'\0') if p)
    return sorted(paths)


def deliver(proof_path, message, push):
    proof = json.loads(proof_path.read_text(encoding='utf-8-sig'))
    if not proof.get('agentDelivery'):
        raise SystemExit('This run has no agent delivery baseline. UI runs never trigger Git.')
    repo = Path(proof['repository']).resolve()
    protected = {os.path.normcase(p) for p in proof['preexistingDirtyPaths']}
    owned, excluded = {}, []
    for group, files in proof['groups'].items():
        relative = {Path(p).resolve().relative_to(repo).as_posix(): (Path(p), sha) for p, sha in files.items()}
        if any(os.path.normcase(p) in protected for p in relative): excluded.append(group)
        else: owned.update(relative)
    for relative, (path, sha) in owned.items():
        if hashlib.sha256(path.read_bytes()).hexdigest() != sha:
            raise SystemExit('Published file changed after delivery verification: ' + relative)
    changed = set(dirty_paths(repo))
    paths = sorted(set(owned) & changed)
    if not paths:
        print(json.dumps({'delivery': 'nothing-to-commit', 'excludedGroups': excluded})); return
    if push and git(repo, 'rev-list', '@{upstream}..HEAD').strip():
        raise SystemExit('Unpublished commits already exist; refusing to push unrelated history.')
    with tempfile.TemporaryDirectory(prefix='qc-world-delivery-') as temp:
        specs = Path(temp) / 'paths'; specs.write_bytes(b''.join(p.encode('utf-8') + b'\0' for p in paths))
        body = Path(temp) / 'message'; body.write_text(message + '\n', encoding='utf-8')
        git(repo, 'add', '--pathspec-from-file=' + str(specs), '--pathspec-file-nul')
        git(repo, 'commit', '--only', '--pathspec-from-file=' + str(specs), '--pathspec-file-nul', '--file=' + str(body))
    committed = set(git(repo, 'diff-tree', '--no-commit-id', '--name-only', '-r', 'HEAD').decode('utf-8').splitlines())
    if not committed <= set(paths): raise SystemExit('Unexpected commit ownership; push stopped.')
    if push: git(repo, 'push')
    print(json.dumps({'delivery': 'pushed' if push else 'committed', 'commit': git(repo, 'rev-parse', 'HEAD').decode().strip(), 'files': len(committed), 'excludedGroups': excluded}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True, help='Already open QA Unity project')
    parser.add_argument('--timeout', type=float, default=60)
    sub = parser.add_subparsers(dest='command', required=True)
    run = sub.add_parser('run')
    run.add_argument('recipe', type=Path)
    run.add_argument('--repository-project', type=Path, default=Path(__file__).resolve().parents[1] / 'QuietCamp')
    run.add_argument('--agent-delivery', action='store_true', help='Record protected user files for later explicit delivery')
    sub.add_parser('status')
    teaser = sub.add_parser('teaser', help='Isolated real-game marketing capture through the WorldOps extension')
    teaser.add_argument('action', choices=['create', 'validate', 'preview', 'record', 'status', 'cancel', 'reframe', 'bake'])
    teaser.add_argument('--level')
    teaser.add_argument('--purpose')
    teaser.add_argument('--version',choices=['v01','v02','v03-a','v03-b','v03-c','v03-rough','v03'],default='v01')
    teaser.add_argument('--pitch',type=float)
    teaser.add_argument('--yaw',type=float)
    teaser.add_argument('--shot', type=int, default=-1, help='Zero-based individual take, or all seven by default')
    teaser.add_argument('--target', type=float, nargs=3)
    teaser.add_argument('--ortho', type=float)
    teaser.add_argument('--end-target',type=float,nargs=3)
    teaser.add_argument('--end-ortho',type=float)
    teaser.add_argument('--end-yaw',type=float)
    observing = sub.add_parser('watch', help='Stream progress from the current Editor journal until it stops')
    observing.add_argument('--duration', type=float, default=1800, help='Maximum observation time in seconds; does not cancel the job')
    resume = sub.add_parser('resume'); resume.add_argument('--run-id')
    sub.add_parser('cancel')
    review = sub.add_parser('review'); review.add_argument('verdict', type=Path)
    rollback = sub.add_parser('rollback'); rollback.add_argument('level_id')
    sub.add_parser('apply')
    sub.add_parser('verify-publication')
    delivery = sub.add_parser('deliver')
    delivery.add_argument('--message', default='Apply reviewed QuietCamp WorldOps changes')
    delivery.add_argument('--push', action='store_true')
    args = parser.parse_args()
    project = args.project.resolve()
    if not (project / 'Temp/UnityLockfile').exists():
        raise SystemExit('The selected Unity project is not open. No Editor was launched.')
    temp = project / 'Temp/ai'; temp.mkdir(parents=True, exist_ok=True)
    inbox = temp / 'qc-world-request.json'; outbox = temp / 'qc-world-result.json'
    # One writer per native bridge. Do not replace an outstanding request.
    payload = {'requestId': uuid.uuid4().hex, 'command': 'delivery' if args.command == 'deliver' else 'status' if args.command == 'watch' else args.command}
    if args.command == 'teaser':
        payload.update(action=args.action, shot=args.shot, version=args.version)
        if args.level is not None: payload['levelId'] = args.level
        if args.purpose is not None: payload['purpose'] = args.purpose
        if args.pitch is not None: payload['pitch'] = args.pitch
        if args.yaw is not None: payload['yaw'] = args.yaw
        if args.target is not None: payload['target'] = args.target
        if args.ortho is not None: payload['ortho'] = args.ortho
        if args.end_target is not None: payload['endTarget'] = args.end_target
        if args.end_ortho is not None: payload['endOrtho'] = args.end_ortho
        if args.end_yaw is not None: payload['endYaw'] = args.end_yaw
    if args.command == 'run':
        payload.update(recipe=str(args.recipe.resolve()), repositoryProject=str(args.repository_project.resolve()))
        if args.agent_delivery:
            payload.update(agentDelivery=True, preexistingDirtyPaths=dirty_paths(args.repository_project.resolve().parent))
    if args.command == 'review': payload['verdict'] = str(args.verdict.resolve())
    if args.command == 'resume': payload['runId'] = args.run_id
    if args.command == 'rollback': payload['levelId'] = args.level_id
    pending = inbox.with_suffix('.' + payload['requestId'] + '.pending')
    pending.write_text(json.dumps(payload), encoding='utf-8')
    try:
        # Windows rename refuses an existing target. POSIX link provides the same exclusion.
        if os.name == 'nt': pending.rename(inbox)
        else: os.link(pending, inbox); pending.unlink()
    except FileExistsError:
        pending.unlink(missing_ok=True)
        raise SystemExit('A WorldOps request is pending; wait for its result.')
    deadline = time.monotonic() + args.timeout
    while time.monotonic() < deadline:
        try: result = json.loads(outbox.read_text(encoding='utf-8-sig'))
        except (FileNotFoundError, PermissionError, json.JSONDecodeError): result = {}
        if result.get('requestId') == payload['requestId']:
            if args.command == 'watch' and result.get('status') == 'ok' and result.get('path'):
                return watch(Path(result['path']), args.duration)
            if args.command == 'deliver' and result.get('status') == 'ok':
                deliver(Path(result['path']) / 'delivery.json', args.message, args.push)
            if args.command == 'status' and result.get('path'):
                state = json.loads((Path(result['path']) / 'run.json').read_text(encoding='utf-8-sig'))
                counts = {}
                for level in state['levels']: counts[level['status']] = counts.get(level['status'], 0) + 1
                result['levels'] = counts
                result['exceptions'] = [{'id': x['levelId'], 'status': x['status'], 'error': x.get('error'),
                                         'defects': len(x.get('findings', []))}
                                        for x in state['levels'] if x['status'] in ('exception', 'conflict')]
                result['reviewPackets'] = [str(Path(result['path']) / x['levelId'].replace(':', '_') / ('review-%s.json' % x['round']))
                                          for x in state['levels'] if x['status'] == 'review']
            print(json.dumps(result, ensure_ascii=False, indent=2))
            return 0 if result.get('status') == 'ok' else 1
        time.sleep(.2)
    raise SystemExit('Existing Editor did not answer. Request retained; no second Editor was launched.')


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    raise SystemExit(main())

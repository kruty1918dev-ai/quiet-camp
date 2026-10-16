"""WorldOps transport for the existing QuietCamp Editor. Editing authority stays in C#."""
import argparse
import json
import os
from pathlib import Path
import sys
import time
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True, help='Already open QA Unity project')
    parser.add_argument('--timeout', type=float, default=60)
    sub = parser.add_subparsers(dest='command', required=True)
    run = sub.add_parser('run')
    run.add_argument('recipe', type=Path)
    run.add_argument('--repository-project', type=Path, default=Path(__file__).resolve().parents[1] / 'QuietCamp')
    sub.add_parser('status')
    resume = sub.add_parser('resume'); resume.add_argument('--run-id')
    sub.add_parser('cancel')
    review = sub.add_parser('review'); review.add_argument('verdict', type=Path)
    rollback = sub.add_parser('rollback'); rollback.add_argument('level_id')
    sub.add_parser('apply')
    args = parser.parse_args()
    project = args.project.resolve()
    if not (project / 'Temp/UnityLockfile').exists():
        raise SystemExit('The selected Unity project is not open. No Editor was launched.')
    temp = project / 'Temp/ai'; temp.mkdir(parents=True, exist_ok=True)
    inbox = temp / 'qc-world-request.json'; outbox = temp / 'qc-world-result.json'
    # One writer per native bridge. Do not replace an outstanding request.
    payload = {'requestId': uuid.uuid4().hex, 'command': args.command}
    if args.command == 'run':
        payload.update(recipe=str(args.recipe.resolve()), repositoryProject=str(args.repository_project.resolve()))
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
        except (FileNotFoundError, json.JSONDecodeError): result = {}
        if result.get('requestId') == payload['requestId']:
            if args.command == 'status' and result.get('path'):
                state = json.loads((Path(result['path']) / 'run.json').read_text(encoding='utf-8-sig'))
                counts = {}
                for level in state['levels']: counts[level['status']] = counts.get(level['status'], 0) + 1
                result['levels'] = counts
                result['exceptions'] = [{'id': x['levelId'], 'status': x['status'], 'error': x.get('error'),
                                         'defects': len(x.get('findings', []))}
                                        for x in state['levels'] if x['status'] in ('exception', 'conflict')]
                result['reviewPackets'] = [str(Path(result['path']) / x['levelId'] / ('review-%s.json' % x['round']))
                                          for x in state['levels'] if x['status'] == 'review']
            print(json.dumps(result, ensure_ascii=False, indent=2))
            return 0 if result.get('status') == 'ok' else 1
        time.sleep(.2)
    raise SystemExit('Existing Editor did not answer. Request retained; no second Editor was launched.')


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    raise SystemExit(main())

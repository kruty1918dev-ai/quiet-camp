"""Compatibility wrapper: all campaign edits and Game View captures go through WorldOps."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import tempfile


def recipe_for(args):
    if args.start < 1 or args.end > 100 or args.start > args.end:
        raise ValueError('Expected an inclusive campaign range within 1..100.')
    if args.compose and args.recipe is None:
        raise ValueError('--compose requires --recipe with allow-listed WorldOps operations.')
    recipe = json.loads(args.recipe.read_text(encoding='utf-8-sig')) if args.recipe else {
        'version': 1, 'id': 'campaign-game-view-audit', 'operations': [{'id': 'inspect', 'kind': 'audit'}],
        'capture': True, 'autoApply': False, 'criteria': {'autoRepair': False, 'requireVisualReview': True}}
    if recipe.get('levelIds'):
        raise ValueError('Use qc_world.py directly for stable-ID selections; a range wrapper must not widen them.')
    orders = list(range(args.start, args.end + 1))
    recipe['orders'] = [n for n in orders if not recipe.get('orders') or n in recipe['orders']]
    if not recipe['orders']:
        raise ValueError('The requested range does not intersect the recipe selection.')
    if args.round:
        recipe['label'] = args.round
    return recipe


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=Path('C:/Users/Public/qcui-20261006/QuietCamp'))
    parser.add_argument('--recipe', type=Path)
    parser.add_argument('--start', type=int, default=1)
    parser.add_argument('--end', type=int, default=100)
    parser.add_argument('--round')
    parser.add_argument('--compose', action='store_true')
    parser.add_argument('--no-sheets', action='store_true', help='Compatibility option; WorldOps provides its own report')
    args = parser.parse_args()
    try:
        recipe = recipe_for(args)
    except ValueError as error:
        parser.error(str(error))
    # This is recipe input only. Unity owns drafts, placement, evidence and publication.
    with tempfile.TemporaryDirectory(prefix='qc-world-recipe-') as directory:
        source = Path(directory) / 'recipe.json'
        source.write_text(json.dumps(recipe, ensure_ascii=False, indent=2), encoding='utf-8')
        return subprocess.call([sys.executable, str(Path(__file__).with_name('qc_world.py')),
                                '--project', str(args.project), 'run', str(source)])


if __name__ == '__main__':
    raise SystemExit(main())

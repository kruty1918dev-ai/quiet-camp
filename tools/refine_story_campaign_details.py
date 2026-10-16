"""Compatibility entry point: run an explicit WorldOps recipe, never patch generated levels."""
import argparse
from pathlib import Path
import subprocess
import sys

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('recipe', type=Path)
    parser.add_argument('--project', type=Path, default=Path('C:/Users/Public/qcui-20261006/QuietCamp'))
    args = parser.parse_args()
    raise SystemExit(subprocess.call([sys.executable, str(Path(__file__).with_name('qc_world.py')),
                                     '--project', str(args.project), 'run', str(args.recipe)]))

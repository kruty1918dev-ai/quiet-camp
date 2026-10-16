"""Compatibility entry point: publication belongs to the WorldOps C# transaction service."""
import argparse
from pathlib import Path
import subprocess
import sys

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=Path('C:/Users/Public/qcui-20261006/QuietCamp'))
    args = parser.parse_args()
    raise SystemExit(subprocess.call([sys.executable, str(Path(__file__).with_name('qc_world.py')),
                                     '--project', str(args.project), 'apply']))

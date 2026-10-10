#!/usr/bin/env python3
"""Prepare the pinned, local-only encoder used by roadmap Editor evidence.

No system packages or Python environment changes. The wheel and executable
digests match the encoder recorded in the existing 2026-10-10 video receipt.
"""
import hashlib
import io
import os
from pathlib import Path
import urllib.request
import zipfile

URL = 'https://files.pythonhosted.org/packages/a0/2d/43c8522a2038e9d0e7dbdf3a61195ecc31ca576fb1527a528c877e87d973/imageio_ffmpeg-0.6.0-py3-none-manylinux2014_x86_64.whl'
WHEEL_SHA = 'c7e46fcec401dd990405049d2e2f475e2b397779df2519b544b8aab515195282'
EXECUTABLE_SHA = 'e7e7fb30477f717e6f55f9180a70386c62677ef8a4d4d1a5d948f4098aa3eb99'
TARGET = Path.home() / '.cache/quietcamp/roadmap-tools/ffmpeg-7.0.2'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    if not TARGET.exists() or digest(TARGET.read_bytes()) != EXECUTABLE_SHA:
        # Keep the compressed download in memory rather than retaining a duplicate
        # on this space-constrained laptop. Never extract arbitrary archive paths.
        with urllib.request.urlopen(URL, timeout=60) as response:
            wheel = response.read(40 * 1024 * 1024)
        if digest(wheel) != WHEEL_SHA:
            raise SystemExit('Encoder wheel digest mismatch')
        with zipfile.ZipFile(io.BytesIO(wheel)) as archive:
            candidates = [n for n in archive.namelist() if n.startswith('imageio_ffmpeg/binaries/ffmpeg-linux')]
            if len(candidates) != 1:
                raise SystemExit('Unexpected encoder archive')
            executable = archive.read(candidates[0])
        if digest(executable) != EXECUTABLE_SHA:
            raise SystemExit('Encoder executable digest mismatch')
        TARGET.parent.mkdir(parents=True, exist_ok=True)
        temporary = TARGET.with_suffix('.preparing')
        temporary.write_bytes(executable)
        temporary.chmod(0o700)
        os.replace(temporary, TARGET)
    print(TARGET)


if __name__ == '__main__':
    main()

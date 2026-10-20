"""Prepare Firebase 14 UPM archives with the project's existing Unity EDM.

Only Firebase Core's package.json dependency is adapted. SDK binaries, native
libraries, editor hooks and license files are copied byte-for-byte from Google.
"""
import hashlib
import gzip
import io
import json
from pathlib import Path
import tarfile
import urllib.request

ROOT = Path(__file__).resolve().parent.parent
VERSION = "14.0.0"
ORIGINAL_SHA256 = {
    "app": "734acab59427b8f51d7aaa326f8a6e9ec61c54b37cbb64ea19eefd1f30e79926",
    "analytics": "406c9b8efdb34c13f93c8371ec895e59b826d04d09742685dd7818b90eccf544",
    "crashlytics": "8cd97c49ea5ab5f2133bf1c93178bff3c3568bf9b8df1faa615774c9701112b8",
}


def main():
    cache = ROOT / "QuietCamp/Temp/ai/firebase"
    output = ROOT / "QuietCamp/GooglePackages"
    cache.mkdir(parents=True, exist_ok=True)
    output.mkdir(parents=True, exist_ok=True)
    for product in ("app", "analytics", "crashlytics"):
        name = "com.google.firebase." + product
        filename = name + "-" + VERSION + ".tgz"
        source = cache / filename
        url = "https://dl.google.com/games/registry/unity/" + name + "/" + filename
        if not source.exists() or source.stat().st_size == 0:
            with urllib.request.urlopen(url, timeout=60) as response:
                source.write_bytes(response.read())
        destination = output / filename
        assert hashlib.sha256(source.read_bytes()).hexdigest() == ORIGINAL_SHA256[product], "Unexpected Google archive checksum: " + name
        if product == "app":
            with tarfile.open(source) as original, destination.open("wb") as raw, \
                    gzip.GzipFile(fileobj=raw, mode="wb", filename="", mtime=0) as compressed, \
                    tarfile.open(fileobj=compressed, mode="w") as adapted:
                for member in original:
                    stream = original.extractfile(member) if member.isfile() else None
                    if member.name == "package/package.json":
                        metadata = json.load(stream)
                        assert metadata["version"] == VERSION
                        assert metadata["dependencies"] == {"com.google.external-dependency-manager": "1.2.190"}
                        metadata["dependencies"] = {"com.unity.external-dependency-manager": "2.1.0"}
                        content = (json.dumps(metadata, indent=2) + "\n").encode()
                        member.size = len(content)
                        stream = io.BytesIO(content)
                    adapted.addfile(member, stream)
            with tarfile.open(source) as original, tarfile.open(destination) as adapted:
                for member in original:
                    if member.isfile() and member.name != "package/package.json":
                        assert original.extractfile(member).read() == adapted.extractfile(member.name).read()
        else:
            destination.write_bytes(source.read_bytes())
        print(filename + " original SHA256=" + hashlib.sha256(source.read_bytes()).hexdigest())
    print("Prepared three Firebase 14.0.0 packages; only Core EDM metadata differs.")


if __name__ == "__main__":
    main()

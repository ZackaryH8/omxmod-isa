#!/usr/bin/env python3
"""Store a build-only public SDK archive in the release workflow's repository secret."""

import argparse
import base64
import io
from pathlib import Path
import subprocess
import tarfile


def encode_sdk(directory: Path) -> bytes:
    buffer = io.BytesIO()
    with tarfile.open(fileobj=buffer, mode="w:xz", preset=9) as archive:
        for name in ("OMX.ModAPI.Shared.dll", "OMX.ModAPI.Client.dll"):
            data = (directory / name).read_bytes()
            entry = tarfile.TarInfo(name)
            entry.size = len(data)
            entry.mode = 0o644
            entry.mtime = 0
            archive.addfile(entry, io.BytesIO(data))
    encoded = base64.b64encode(buffer.getvalue())
    if len(encoded) > 48 * 1024:
        raise ValueError("SDK archive exceeds GitHub's 48 KiB repository secret limit.")
    return encoded


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path, help="OMX API 2.1+ SDK directory")
    parser.add_argument("--repo", required=True, help="GitHub owner/repository to configure")
    args = parser.parse_args()
    subprocess.run(
        ["gh", "secret", "set", "OMX_SDK_ARCHIVE", "--repo", args.repo],
        input=encode_sdk(args.directory),
        check=True,
    )
    print(f"Configured build-only SDK for {args.repo}; the release ZIP excludes SDK assemblies.")


if __name__ == "__main__":
    main()

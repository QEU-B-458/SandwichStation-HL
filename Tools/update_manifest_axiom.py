#!/usr/bin/env python3
"""
Updates manifest.json with the latest server build from GitHub Releases.
Used by the publish-axiom.yml GitHub Actions workflow.
"""

import hashlib
import json
import os
import subprocess
import sys
import time
import urllib.request

REPO = os.environ["REPO"]  # e.g. QEU-B-458/SandwichStation-HL
VERSION = os.environ["VERSION"]  # short git SHA
GITHUB_TOKEN = os.environ["GITHUB_TOKEN"]

MANIFEST_FILE = "manifest.json"
RELEASE_TAG = f"build-{VERSION}"
ZIP_NAME = "SS14.Server_linux-x64.zip"
ZIP_URL = f"https://github.com/{REPO}/releases/download/{RELEASE_TAG}/{ZIP_NAME}"

def sha256_of_url(url: str) -> tuple[str, int]:
    """Download file and compute SHA256 + size."""
    req = urllib.request.Request(url, headers={"Authorization": f"token {GITHUB_TOKEN}"})
    h = hashlib.sha256()
    size = 0
    with urllib.request.urlopen(req) as resp:
        while chunk := resp.read(65536):
            h.update(chunk)
            size += len(chunk)
    return h.hexdigest().upper(), size

print(f"Computing SHA256 for {ZIP_URL} ...")
sha256, size = sha256_of_url(ZIP_URL)
print(f"  SHA256: {sha256}  Size: {size} bytes")

# Load existing manifest or start fresh
if os.path.exists(MANIFEST_FILE):
    with open(MANIFEST_FILE) as f:
        manifest = json.load(f)
else:
    manifest = {"builds": {}}

# Add new build entry
manifest["builds"][VERSION] = {
    "time": int(time.time()),
    "server": {
        "linux-x64": {
            "url": ZIP_URL,
            "sha256": sha256,
            "size": size,
        }
    }
}

# Keep only the 10 most recent builds to avoid bloat
builds = manifest["builds"]
if len(builds) > 10:
    sorted_keys = sorted(builds, key=lambda k: builds[k]["time"])
    for old in sorted_keys[:-10]:
        del builds[old]

with open(MANIFEST_FILE, "w") as f:
    json.dump(manifest, f, indent=2)

print(f"manifest.json updated with build {VERSION}")

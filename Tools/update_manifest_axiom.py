#!/usr/bin/env python3
"""
Updates manifest.json with the latest server build from GitHub Releases.
Used by the publish-axiom.yml GitHub Actions workflow.
"""

import hashlib
import json
import os
import urllib.request
from datetime import datetime, timezone

REPO = os.environ["REPO"]  # e.g. QEU-B-458/SandwichStation-HL
VERSION = os.environ["VERSION"]  # short git SHA
GITHUB_TOKEN = os.environ["GITHUB_TOKEN"]

MANIFEST_FILE = "manifest.json"
RELEASE_TAG = f"build-{VERSION}"
SERVER_ZIP = "SS14.Server_linux-x64.zip"
CLIENT_ZIP = "SS14.Client_windows-x64.zip"
SERVER_URL = f"https://github.com/{REPO}/releases/download/{RELEASE_TAG}/{SERVER_ZIP}"
CLIENT_URL = f"https://github.com/{REPO}/releases/download/{RELEASE_TAG}/{CLIENT_ZIP}"

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

print(f"Computing SHA256 for {SERVER_URL} ...")
server_sha256, server_size = sha256_of_url(SERVER_URL)
print(f"  SHA256: {server_sha256}  Size: {server_size} bytes")

print(f"Computing SHA256 for {CLIENT_URL} ...")
client_sha256, client_size = sha256_of_url(CLIENT_URL)
print(f"  SHA256: {client_sha256}  Size: {client_size} bytes")

# Load existing manifest or start fresh
if os.path.exists(MANIFEST_FILE):
    with open(MANIFEST_FILE) as f:
        manifest = json.load(f)
else:
    manifest = {"builds": {}}

# Add new build entry — time must be ISO 8601 for Watchdog's DateTimeOffset deserializer
manifest["builds"][VERSION] = {
    "time": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "client": {
        "url": CLIENT_URL,
        "sha256": client_sha256,
    },
    "server": {
        "linux-x64": {
            "url": SERVER_URL,
            "sha256": server_sha256,
        }
    }
}

# Keep only the 10 most recent builds to avoid bloat
builds = manifest["builds"]
if len(builds) > 10:
    sorted_keys = sorted(builds, key=lambda k: builds[k]["time"])
    for old in sorted_keys[:-10]:
        del builds[old]

print(f"manifest.json updated with build {VERSION}")

with open(MANIFEST_FILE, "w") as f:
    json.dump(manifest, f, indent=2)

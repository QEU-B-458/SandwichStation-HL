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

# Environment Variables from GitHub Actions
REPO = os.environ["REPO"]           # e.g. QEU-B-458/SandwichStation-HL
VERSION = os.environ["VERSION"]     # short git SHA (e.g. 60f59bea2)
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
    try:
        with urllib.request.urlopen(req) as resp:
            while chunk := resp.read(65536):
                h.update(chunk)
                size += len(chunk)
    except Exception as e:
        print(f"Error downloading {url}: {e}")
        raise
    return h.hexdigest().upper(), size

# 1. Generate Data for the New Build
print(f"Computing SHA256 for {SERVER_URL} ...")
server_sha256, server_size = sha256_of_url(SERVER_URL)

print(f"Computing SHA256 for {CLIENT_URL} ...")
client_sha256, client_size = sha256_of_url(CLIENT_URL)

# 2. Load Existing Manifest or Initialize
if os.path.exists(MANIFEST_FILE):
    with open(MANIFEST_FILE, "r") as f:
        try:
            manifest = json.load(f)
        except json.JSONDecodeError:
            manifest = {"builds": {}}
else:
    manifest = {"builds": {}}

# Ensure the 'builds' key exists
if "builds" not in manifest:
    manifest["builds"] = {}

# 3. Add the New Build Entry
# Using ISO 8601 for Watchdog compatibility
manifest["builds"][VERSION] = {
    "time": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "client": {
        "url": CLIENT_URL,
        "sha256": client_sha256,
        "size": client_size
    },
    "server": {
        "linux-x64": {
            "url": SERVER_URL,
            "sha256": server_sha256,
            "size": server_size
        }
    }
}

# 4. Update the 'Latest' Pointer
# This tells the Watchdog exactly which build in the list to pull
manifest["version"] = VERSION

# 5. Cleanup Old Builds (Keep only 10)
builds = manifest["builds"]
if len(builds) > 10:
    # Sort keys based on the 'time' value inside each build object
    sorted_keys = sorted(builds.keys(), key=lambda k: builds[k].get("time", ""))
    
    # Remove oldest entries until we are back to 10
    while len(builds) > 10:
        oldest_key = sorted_keys.pop(0)
        # Never delete the version we just added
        if oldest_key != VERSION:
            del builds[oldest_key]

# 6. Save the Manifest
with open(MANIFEST_FILE, "w") as f:
    json.dump(manifest, f, indent=2)

print(f"Successfully updated {MANIFEST_FILE} with build {VERSION}")

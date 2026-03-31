#!/usr/bin/env python3
import hashlib, json, os, urllib.request
from datetime import datetime, timezone

REPO = os.environ["REPO"]
VERSION = os.environ["VERSION"]
GITHUB_TOKEN = os.environ["GITHUB_TOKEN"]
MANIFEST_FILE = "manifest-beta.json"
RELEASE_TAG = f"beta-build-{VERSION}"
SERVER_ZIP = "SS14.Server_linux-x64.zip"
CLIENT_ZIP = "SS14.Client_windows-x64.zip"
SERVER_URL = f"https://github.com/{REPO}/releases/download/{RELEASE_TAG}/{SERVER_ZIP}"
CLIENT_URL = f"https://github.com/{REPO}/releases/download/{RELEASE_TAG}/{CLIENT_ZIP}"

def sha256_of_url(url):
    req = urllib.request.Request(url, headers={"Authorization": f"token {GITHUB_TOKEN}"})
    h = hashlib.sha256(); size = 0
    try:
        with urllib.request.urlopen(req) as resp:
            while chunk := resp.read(65536):
                h.update(chunk); size += len(chunk)
    except Exception as e:
        print(f"Error downloading {url}: {e}"); raise
    return h.hexdigest().upper(), size

server_sha256, server_size = sha256_of_url(SERVER_URL)
client_sha256, client_size = sha256_of_url(CLIENT_URL)

manifest = json.load(open(MANIFEST_FILE)) if os.path.exists(MANIFEST_FILE) else {"builds": {}}
if "builds" not in manifest: manifest["builds"] = {}

manifest["builds"][VERSION] = {
    "time": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "client": {"url": CLIENT_URL, "sha256": client_sha256, "size": client_size},
    "server": {"linux-x64": {"url": SERVER_URL, "sha256": server_sha256, "size": server_size}}
}
manifest["version"] = VERSION

builds = manifest["builds"]
if len(builds) > 10:
    sorted_keys = sorted(builds.keys(), key=lambda k: builds[k].get("time", ""))
    while len(builds) > 10:
        oldest_key = sorted_keys.pop(0)
        if oldest_key != VERSION: del builds[oldest_key]

json.dump(manifest, open(MANIFEST_FILE, "w"), indent=2)
print(f"Successfully updated {MANIFEST_FILE} with beta build {VERSION}")

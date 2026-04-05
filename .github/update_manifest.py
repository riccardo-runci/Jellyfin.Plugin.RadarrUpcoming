import json
import os
import hashlib
import datetime
import yaml

artifact = os.environ["ARTIFACT"]
repo     = os.environ["REPO"]
tag      = os.environ["TAG"]
version  = os.environ["VERSION"]

filename   = os.path.basename(artifact)
source_url = f"https://github.com/{repo}/releases/download/{tag}/{filename}"
checksum   = hashlib.md5(open(artifact, "rb").read()).hexdigest()
timestamp  = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")

build = yaml.safe_load(open("build.yaml"))

new_entry = {
    "version":   version,
    "changelog": str(build.get("changelog", "")).strip(),
    "targetAbi": str(build.get("targetAbi", "10.9.0.0")),
    "sourceUrl": source_url,
    "checksum":  checksum,
    "timestamp": timestamp,
}

manifest_path = "manifest.json"
if os.path.exists(manifest_path):
    with open(manifest_path) as f:
        manifest = json.load(f)
else:
    manifest = [{
        "guid":        str(build["guid"]),
        "name":        str(build["name"]),
        "description": str(build.get("description", build.get("overview", ""))).strip(),
        "overview":    str(build.get("overview", "")).strip(),
        "owner":       str(build.get("owner", "")),
        "category":    str(build.get("category", "General")),
        "versions":    [],
    }]

plugin = manifest[0]
plugin["versions"] = [v for v in plugin.get("versions", []) if v["version"] != version]
plugin["versions"].insert(0, new_entry)

with open(manifest_path, "w") as f:
    json.dump(manifest, f, indent=4)
    f.write("\n")

print(f"manifest.json updated — version={version}, sourceUrl={source_url}, checksum={checksum}")

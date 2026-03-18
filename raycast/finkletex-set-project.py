#!/usr/bin/env python3

# Required parameters:
# @raycast.schemaVersion 1
# @raycast.title Set Project
# @raycast.mode fullOutput

# Optional parameters:
# @raycast.icon 📁
# @raycast.argument1 { "type": "text", "placeholder": "Project name (or empty to list all)", "optional": true }
# @raycast.packageName Finkletex

# Documentation:
# @raycast.description Set Raycast override project for Finkletex
# @raycast.author Ole Magnus

import json
import subprocess
import sys
from pathlib import Path

RAYCAST_CONFIG_PATH = Path.home() / ".raycast-finkletex.json"
FINKLETEX = "/usr/local/bin/finkletex"


def load_json(path):
    if path.exists():
        return json.loads(path.read_text())
    return {}


def save_json(path, data):
    path.write_text(json.dumps(data, indent=2) + "\n")


def format_project(p):
    name = p.get("name") or p.get("displayName") or "Unnamed"
    return f"  [{p.get('id')}] {name}"


def main():
    search_term = (sys.argv[1].strip() if len(sys.argv) > 1 else "")

    if search_term.lower() in ("0", "none", "no project"):
        raycast = load_json(RAYCAST_CONFIG_PATH)
        raycast["projectId"] = 0
        raycast["projectName"] = "(No project)"
        raycast.pop("activityId", None)
        raycast.pop("activityName", None)
        save_json(RAYCAST_CONFIG_PATH, raycast)
        print("Project set to: (No project)")
        return

    cmd = [FINKLETEX, "--json", "project"]
    if search_term:
        cmd += ["search", "--name", search_term]
    else:
        cmd += ["list"]

    result = subprocess.run(cmd, capture_output=True, text=True, stdin=subprocess.DEVNULL)

    if result.returncode != 0:
        error = result.stderr.strip() or result.stdout.strip() or "Unknown error"
        print(f"Failed: {error}")
        sys.exit(1)

    try:
        projects = json.loads(result.stdout)
    except json.JSONDecodeError:
        print("Failed to parse project list")
        sys.exit(1)

    if len(projects) == 0:
        print(f"No projects found for '{search_term}'")
        sys.exit(1)

    if len(projects) != 1:
        print(f"Projects ({len(projects)}):\n")
        for p in projects:
            print(format_project(p))
        print(f"\nRe-run with an exact name or use the ID.")
        return

    project = projects[0]
    project_id = project.get("id")
    project_name = project.get("name") or project.get("displayName") or f"Project {project_id}"

    raycast = load_json(RAYCAST_CONFIG_PATH)
    raycast["projectId"] = project_id
    raycast["projectName"] = project_name
    raycast.pop("activityId", None)
    raycast.pop("activityName", None)
    save_json(RAYCAST_CONFIG_PATH, raycast)

    print(f"Project set to: {project_name}")


if __name__ == "__main__":
    main()

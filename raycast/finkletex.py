#!/usr/bin/env python3

# Required parameters:
# @raycast.schemaVersion 1
# @raycast.title Log Hours
# @raycast.mode compact

# Optional parameters:
# @raycast.icon ⏱️
# @raycast.argument1 { "type": "text", "placeholder": "Hours (e.g. 7.5)" }
# @raycast.argument2 { "type": "text", "placeholder": "Comment", "optional": true }
# @raycast.packageName Finkletex

# Documentation:
# @raycast.description Log timesheet hours using saved defaults
# @raycast.author Ole Magnus

import json
import subprocess
import sys
from datetime import date
from pathlib import Path

CONFIG_PATH = Path.home() / ".tripletex-employee" / "config.json"
RAYCAST_CONFIG_PATH = Path.home() / ".raycast-finkletex.json"
FINKLETEX = "/usr/local/bin/finkletex"


def load_json(path):
    if path.exists():
        return json.loads(path.read_text())
    return {}


def main():
    hours = sys.argv[1]
    comment = sys.argv[2] if len(sys.argv) > 2 else ""

    try:
        float(hours)
    except ValueError:
        print(f"Invalid hours: {hours}")
        sys.exit(1)

    if not CONFIG_PATH.exists():
        print("Not configured. Run 'finkletex login' first.")
        sys.exit(1)

    config = load_json(CONFIG_PATH)
    raycast = load_json(RAYCAST_CONFIG_PATH)

    project_id = raycast.get("projectId") if "projectId" in raycast else config.get("defaultProjectId")
    activity_id = raycast.get("activityId") if "activityId" in raycast else config.get("defaultActivityId")
    project_name = raycast.get("projectName") or config.get("defaultProjectName") or f"Project {project_id}"

    if project_id is None or activity_id is None:
        print("No defaults set. Run 'finkletex l' in terminal to set them.")
        sys.exit(1)

    cmd = [
        FINKLETEX, "--yes", "l", hours,
        "--project-id", str(project_id),
        "--activity-id", str(activity_id),
        "--date", date.today().isoformat(),
    ]
    if comment:
        cmd.extend(["--comment", comment])

    result = subprocess.run(cmd, capture_output=True, text=True, stdin=subprocess.DEVNULL)

    if result.returncode == 0:
        print(f"Logged {hours}h to {project_name}")
    else:
        error = result.stderr.strip() or result.stdout.strip() or "Unknown error"
        print(f"Failed: {error}")
        sys.exit(1)

if __name__ == "__main__":
    main()

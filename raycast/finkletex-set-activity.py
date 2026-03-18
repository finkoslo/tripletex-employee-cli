#!/usr/bin/env python3

# Required parameters:
# @raycast.schemaVersion 1
# @raycast.title Set Activity
# @raycast.mode fullOutput

# Optional parameters:
# @raycast.icon 🎯
# @raycast.argument1 { "type": "text", "placeholder": "Activity name (or empty to list all)", "optional": true }
# @raycast.packageName Finkletex

# Documentation:
# @raycast.description Set Raycast override activity for Finkletex
# @raycast.author Ole Magnus

import json
import subprocess
import sys
from pathlib import Path

FINKLETEX_CONFIG_PATH = Path.home() / ".tripletex-employee" / "config.json"
RAYCAST_CONFIG_PATH = Path.home() / ".raycast-finkletex.json"
FINKLETEX = "/usr/local/bin/finkletex"


def load_json(path):
    if path.exists():
        return json.loads(path.read_text())
    return {}


def save_json(path, data):
    path.write_text(json.dumps(data, indent=2) + "\n")


def fuzzy_match(query, name):
    return query.lower() in (name or "").lower()


def format_activity(a):
    name = a.get("displayName") or a.get("name") or "Unnamed"
    return f"  [{a.get('id')}] {name}"


def main():
    search_term = (sys.argv[1].strip() if len(sys.argv) > 1 else "")

    raycast = load_json(RAYCAST_CONFIG_PATH)
    config = load_json(FINKLETEX_CONFIG_PATH)

    project_id = raycast.get("projectId") if "projectId" in raycast else config.get("defaultProjectId")
    if project_id is None:
        print("No project set. Use 'Set Project' first.")
        sys.exit(1)


    cmd = [FINKLETEX, "--json", "activity", "list", "--project-id", str(project_id)]
    result = subprocess.run(cmd, capture_output=True, text=True, stdin=subprocess.DEVNULL)

    if result.returncode != 0:
        error = result.stderr.strip() or result.stdout.strip() or "Unknown error"
        print(f"Failed: {error}")
        sys.exit(1)

    try:
        activities = json.loads(result.stdout)
    except json.JSONDecodeError:
        print("Failed to parse activity list")
        sys.exit(1)

    if search_term:
        matches = [
            a for a in activities
            if fuzzy_match(search_term, a.get("displayName") or a.get("name"))
        ]
    else:
        matches = activities

    if len(matches) == 0:
        print(f"No activities found matching '{search_term}'")
        sys.exit(1)

    if len(matches) != 1:
        project_name = raycast.get("projectName") or config.get("defaultProjectName") or f"Project {project_id}"
        print(f"Activities for {project_name} ({len(matches)}):\n")
        for a in matches:
            print(format_activity(a))
        print(f"\nRe-run with an exact name to select.")
        return

    activity = matches[0]
    activity_id = activity.get("id")
    activity_name = activity.get("displayName") or activity.get("name") or f"Activity {activity_id}"

    raycast["activityId"] = activity_id
    raycast["activityName"] = activity_name
    save_json(RAYCAST_CONFIG_PATH, raycast)

    print(f"Activity set to: {activity_name}")


if __name__ == "__main__":
    main()

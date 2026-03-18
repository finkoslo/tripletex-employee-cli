#!/usr/bin/env python3

# Required parameters:
# @raycast.schemaVersion 1
# @raycast.title Week Summary
# @raycast.mode fullOutput

# Optional parameters:
# @raycast.icon 📊
# @raycast.packageName Finkletex

# Documentation:
# @raycast.description Show weekly timesheet summary
# @raycast.author Ole Magnus

import subprocess
import sys

FINKLETEX = "/usr/local/bin/finkletex"

def main():
    result = subprocess.run(
        [FINKLETEX, "w", "--style", "compact"],
        capture_output=True, text=True,
        env={"PATH": "/usr/local/bin:/usr/bin:/bin", "HOME": __import__("os").environ["HOME"], "TERM": "dumb"},
    )

    output = result.stdout.strip()
    if result.returncode == 0 and output:
        print(output)
    else:
        error = result.stderr.strip() or "Failed to fetch week summary"
        print(error)
        sys.exit(1)

if __name__ == "__main__":
    main()

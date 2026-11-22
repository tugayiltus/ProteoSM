#!/usr/bin/env bash
# Re-run tools/progress.sh on a fixed interval, clearing the screen each time. Ctrl+C stops it.
# Usage: tools/progress-watch.sh [interval-seconds] [plan-file]   (default interval: 300)
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DEFAULT_INTERVAL_SECONDS=300

interval="${1:-$DEFAULT_INTERVAL_SECONDS}"
shift || true

while true; do
  clear
  tools/progress.sh "$@" || echo "progress.sh failed (exit $?)"
  printf '\n\e[2mUpdated %s, next refresh in %ss. Ctrl+C to stop.\e[0m\n' "$(date '+%H:%M:%S')" "$interval"
  sleep "$interval"
done

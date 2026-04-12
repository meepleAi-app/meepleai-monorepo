#!/usr/bin/env bash
# Monitors dotnet watch output for restart loops (NFR-OBS-1).
# Tails dotnet-watch.log and counts 'Started' events within a sliding window.
# If 3+ restarts in 60s, prints a red banner and exits with code 2.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"
LOG_FILE="$INFRA_DIR/dotnet-watch.log"

if [ ! -f "$LOG_FILE" ]; then
  echo "Log file not found: $LOG_FILE"
  exit 0
fi

WINDOW_SECS=60
THRESHOLD=3

export LOG_FILE

tail -F "$LOG_FILE" 2>/dev/null | awk -v window=$WINDOW_SECS -v thresh=$THRESHOLD '
  /Started/ {
    now = systime()
    restarts[++n] = now
    while (restarts[1] && (now - restarts[1]) > window) {
      for (i = 1; i < n; i++) restarts[i] = restarts[i+1]
      delete restarts[n]; n--
    }
    if (n >= thresh) {
      print "\033[0;31m"
      print "============================================"
      print "  BACKEND CRASH LOOP DETECTED"
      print "  " n " restarts in " window "s — aborting"
      print "  Check: " ENVIRON["LOG_FILE"]
      print "============================================"
      print "\033[0m"
      exit 2
    }
  }
'

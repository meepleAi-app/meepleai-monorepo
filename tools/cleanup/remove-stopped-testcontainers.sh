#!/bin/sh
# Removes the STOPPED Testcontainers containers, and only those.
# Usage: sh tools/cleanup/remove-stopped-testcontainers.sh
#
# Issue #4310. The pre-push hook used to run "docker rm -f" on every container labelled
# org.testcontainers=true. A running one is the Postgres or Redis of a test run in progress -
# often of another checkout working on this machine - and removing it fails that run with
# dozens of "Exception while reading from stream" at the same instant.
#
# So:
#   - only "exited" and "dead" containers are listed. Not "created": that is a container between
#     its creation and its start, i.e. a test run that is starting;
#   - "docker rm" without -f: a container restarted in the meantime is refused, not killed.
# The containers of a run whose process died are reaped by Ryuk; what is left here is what
# survives a Docker restart.
#
# Tested in infra/scripts/tests/remove-stopped-testcontainers.bats.

if ! command -v docker >/dev/null 2>&1; then
  exit 0
fi

STOPPED=$(docker ps -a \
  --filter "label=org.testcontainers=true" \
  --filter "status=exited" \
  --filter "status=dead" \
  -q 2>/dev/null)

if [ -z "$STOPPED" ]; then
  echo "   No stopped Testcontainers to remove"
  exit 0
fi

COUNT=$(echo "$STOPPED" | wc -l | tr -d ' ')
echo "$STOPPED" | xargs docker rm >/dev/null 2>&1 || true
echo "   Removed $COUNT stopped Testcontainers (running ones are left alone)"

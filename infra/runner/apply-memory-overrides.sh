#!/usr/bin/env bash
# Apply the systemd MemoryMax + OOMScoreAdjust + Restart override for the
# GitHub Actions self-hosted runner. Idempotent — safe to re-run.
#
# Issue #2019 — staging runner OOM-killed during pnpm install on 2026-06-08.
#
# Usage:
#   sudo ./apply-memory-overrides.sh
#
# Verify after apply:
#   systemctl show <runner-service> -p MemoryMax,OOMScoreAdjust,Restart,RestartSec
#
# Refs:
#   https://github.com/meepleAi-app/meepleai-monorepo/issues/2019

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OVERRIDE_SRC="${SCRIPT_DIR}/systemd-overrides/10-memory-limits.conf"

if [ ! -f "${OVERRIDE_SRC}" ]; then
  echo "ERROR: override file not found at ${OVERRIDE_SRC}" >&2
  exit 1
fi

if [ "$(id -u)" -ne 0 ]; then
  echo "ERROR: this script must be run as root (sudo)." >&2
  exit 1
fi

# Auto-discover the runner service name. The actions/runner installer
# generates a unit named `actions.runner.<owner>-<repo>.<runner-name>.service`.
RUNNER_SERVICE="$(systemctl list-units --type=service --no-legend --all \
  | awk '/actions\.runner\./{print $1; exit}')"

if [ -z "${RUNNER_SERVICE}" ]; then
  echo "ERROR: no actions.runner.* service found on this host." >&2
  echo "       Has the runner been installed? See infra/runner/setup-runner.sh" >&2
  exit 1
fi

OVERRIDE_DIR="/etc/systemd/system/${RUNNER_SERVICE}.d"
OVERRIDE_DST="${OVERRIDE_DIR}/10-memory-limits.conf"

echo "=== Applying memory overrides to: ${RUNNER_SERVICE} ==="

mkdir -p "${OVERRIDE_DIR}"
install -m 0644 "${OVERRIDE_SRC}" "${OVERRIDE_DST}"
echo "  installed: ${OVERRIDE_DST}"

systemctl daemon-reload
echo "  systemctl daemon-reload OK"

# Restart the runner so the new cgroup limits take effect. Without restart
# the override is loaded but not applied to the running process.
systemctl restart "${RUNNER_SERVICE}"
echo "  systemctl restart ${RUNNER_SERVICE} OK"

# Wait briefly then verify the effective values.
sleep 2

echo ""
echo "=== Verification ==="
systemctl show "${RUNNER_SERVICE}" \
  -p MemoryMax -p OOMScoreAdjust -p Restart -p RestartSec

EXPECTED_MEM="3221225472"  # 3G in bytes
EXPECTED_OOM="-500"
EXPECTED_RESTART="on-failure"

ACTUAL_MEM="$(systemctl show "${RUNNER_SERVICE}" -p MemoryMax --value)"
ACTUAL_OOM="$(systemctl show "${RUNNER_SERVICE}" -p OOMScoreAdjust --value)"
ACTUAL_RESTART="$(systemctl show "${RUNNER_SERVICE}" -p Restart --value)"

if [ "${ACTUAL_MEM}" = "${EXPECTED_MEM}" ] \
   && [ "${ACTUAL_OOM}" = "${EXPECTED_OOM}" ] \
   && [ "${ACTUAL_RESTART}" = "${EXPECTED_RESTART}" ]; then
  echo ""
  echo "OK — override active. Issue #2019 mitigation in place."
  exit 0
fi

echo ""
echo "WARN — values do not match expected. Got:" >&2
echo "       MemoryMax=${ACTUAL_MEM} (expected ${EXPECTED_MEM})" >&2
echo "       OOMScoreAdjust=${ACTUAL_OOM} (expected ${EXPECTED_OOM})" >&2
echo "       Restart=${ACTUAL_RESTART} (expected ${EXPECTED_RESTART})" >&2
exit 2

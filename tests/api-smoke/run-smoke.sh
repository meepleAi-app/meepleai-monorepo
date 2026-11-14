#!/usr/bin/env bash
# meepleai-api-smoke runner (Linux/macOS/CI)
#
# Invokes Bruno CLI against the bruno-collection/ directory with the chosen env.
# Prints a JSON summary + table.
#
# Usage:
#   tests/api-smoke/run-smoke.sh --env local
#   tests/api-smoke/run-smoke.sh --env staging --collection private-game
#   tests/api-smoke/run-smoke.sh --help
#
# Exit codes:
#   0  = all scenarios passed
#   1  = one or more scenarios failed, or Bruno ran 0 requests
#   2  = invalid arguments / Bruno CLI not found
#   other = Bruno's own exit code (e.g. 4: not the root of a collection)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COLLECTION_ROOT="$SCRIPT_DIR/bruno-collection"
BRUNO_VERSION="$(cat "$SCRIPT_DIR/.bruno-version" 2>/dev/null || echo "2.15.1")"

ENV_NAME=""
SUB_COLLECTION=""

usage() {
  cat <<EOF
Usage: run-smoke.sh [OPTIONS]

Run meepleai API smoke tests via Bruno CLI.

Options:
  --env <name>          Environment name (local|staging). REQUIRED.
  --collection <name>   Run only one sub-collection (private-game|kb|agents|sessions).
                        If omitted, runs all 4.
  --help                Show this help and exit.

Environment:
  SMOKE_AARON_PASSWORD  Password of smoke-aaron. With --env local it defaults to the one
                        of tests/fixtures/smoke-test-users.sql.
  SMOKE_API_BASE        Overrides apiBase of the env file (e.g. an API on another port).

Examples:
  run-smoke.sh --env local
  run-smoke.sh --env staging --collection private-game

Exit codes:
  0  all scenarios passed
  1  one or more scenarios failed, or Bruno ran 0 requests
  2  invalid arguments / Bruno CLI not found
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env)
      if [[ $# -lt 2 || -z "${2:-}" || "${2:-}" == --* ]]; then
        echo "Error: --env requires a value" >&2
        usage >&2
        exit 2
      fi
      ENV_NAME="$2"
      shift 2
      ;;
    --collection)
      if [[ $# -lt 2 || -z "${2:-}" || "${2:-}" == --* ]]; then
        echo "Error: --collection requires a value" >&2
        usage >&2
        exit 2
      fi
      SUB_COLLECTION="$2"
      shift 2
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$ENV_NAME" ]]; then
  echo "Error: --env is required" >&2
  usage >&2
  exit 2
fi

ENV_FILE="$COLLECTION_ROOT/environments/${ENV_NAME}.bru"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "Error: env file not found: $ENV_FILE" >&2
  exit 2
fi

# #4978: Bruno runs only from the root of a collection (the folder with bruno.json), and takes the
# sub-collection as a path relative to it. Passing an absolute path from elsewhere failed with
# "You can run only at the root of a collection".
TARGET="."
if [[ -n "$SUB_COLLECTION" ]]; then
  TARGET="$SUB_COLLECTION"
  if [[ ! -d "$COLLECTION_ROOT/$TARGET" ]]; then
    echo "Error: sub-collection not found: $COLLECTION_ROOT/$TARGET" >&2
    exit 2
  fi
fi

# The local env reads the password from the environment, like staging; this is the fixture's.
if [[ "$ENV_NAME" == "local" ]]; then
  export SMOKE_AARON_PASSWORD="${SMOKE_AARON_PASSWORD:-SmokeAaron1!!}"
fi

# #4978: -r, because without it Bruno runs only the requests directly inside the target folder: the
# collection root has none, and a run of the whole collection executed 0 requests and reported PASS.
# The JSON report, relative to the collection root so that the same path works for node on Windows, is
# read below to refuse such a run, and kept for the job's artifacts.
REPORT_REL="../reports/bruno-report.json"
mkdir -p "$SCRIPT_DIR/reports"
rm -f "$SCRIPT_DIR/reports/bruno-report.json"

BRU_ARGS=(run -r --env "$ENV_NAME" --reporter-json "$REPORT_REL")
if [[ -n "${SMOKE_API_BASE:-}" ]]; then
  BRU_ARGS+=(--env-var "apiBase=$SMOKE_API_BASE")
fi

echo "Running Bruno (v${BRUNO_VERSION}) — env=${ENV_NAME}, target=bruno-collection/${TARGET#./}"
cd "$COLLECTION_ROOT"
set +e
npx -y "@usebruno/cli@${BRUNO_VERSION}" "${BRU_ARGS[@]}" "$TARGET"
BRU_EXIT=$?
set -e

TOTAL_REQUESTS=$(node -e '
  const report = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
  console.log(report.reduce((n, iteration) => n + iteration.summary.totalRequests, 0));
' "$REPORT_REL" 2>/dev/null || echo 0)
echo "Bruno: exit ${BRU_EXIT}, ${TOTAL_REQUESTS} request(s) run (report: tests/api-smoke/reports/bruno-report.json)"
if [[ "$BRU_EXIT" -eq 0 && "$TOTAL_REQUESTS" -eq 0 ]]; then
  echo "Error: Bruno ran 0 requests against bruno-collection/${TARGET#./}: nothing was tested" >&2
  exit 1
fi
exit "$BRU_EXIT"

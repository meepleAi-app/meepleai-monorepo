#!/usr/bin/env bash
# Secrets validation — ensure every .secret / .secret.example file is safe
# to `source` under `bash -e` (issue #1574).
#
# Failure mode this prevents: a single unquoted value with shell metacharacters
# (parentheses, pipes, ampersands, spaces, `<placeholder>`) aborts the deploy
# script silently when `set -e` is active. Pre-2026-05-26 this caused
# admin.secret + monitoring.secret to break the manual recovery deploy of #1498
# (see audits/2026-05-26-disk-cleanup-1575.md and issue #1574).
#
# Usage:
#   bash infra/secrets/validate.sh              # validate all .secret + .example
#   bash infra/secrets/validate.sh --examples   # only validate committed *.example
#   bash infra/secrets/validate.sh --secrets    # only validate local *.secret
#
# Exit codes:
#   0 — all clean
#   1 — at least one file failed parse or source
#   64 — bad CLI usage

set -uo pipefail

MODE="all"
case "${1:-all}" in
  all|"") MODE="all" ;;
  --examples) MODE="examples" ;;
  --secrets) MODE="secrets" ;;
  -h|--help)
    sed -n '1,/^set -uo pipefail/p' "$0" | head -20
    exit 0
    ;;
  *)
    echo "Unknown arg: $1" >&2
    echo "Usage: $0 [--examples|--secrets]" >&2
    exit 64
    ;;
esac

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

problems=0
checked=0

check_file() {
  local f="$1"
  [ -f "$f" ] || return 0
  checked=$((checked + 1))
  # 1. Syntax check
  local err
  err=$(bash -n "$f" 2>&1)
  if [ -n "$err" ]; then
    echo "FAIL  $f (syntax)"
    echo "$err" | head -3 | sed 's/^/      /'
    problems=$((problems + 1))
    return 1
  fi
  # 2. Source under `set -e` in a subshell — catches `cmd not found` and similar
  err=$( ( set -e; source "$f" ) 2>&1 )
  if [ -n "$err" ]; then
    echo "FAIL  $f (source under set -e)"
    echo "$err" | head -3 | sed 's/^/      /'
    problems=$((problems + 1))
    return 1
  fi
  echo "PASS  $f"
}

echo "=== Secrets validation (mode=$MODE) ==="

if [ "$MODE" = "all" ] || [ "$MODE" = "examples" ]; then
  for f in *.secret.example; do
    [ -e "$f" ] || continue
    check_file "$f"
  done
fi

if [ "$MODE" = "all" ] || [ "$MODE" = "secrets" ]; then
  for f in *.secret; do
    [ -e "$f" ] || continue
    check_file "$f"
  done
fi

echo ""
echo "=== Result: checked=$checked, problems=$problems ==="

if [ "$problems" -gt 0 ]; then
  echo ""
  echo "How to fix:"
  echo "  Quote values containing shell metacharacters (space, parentheses, pipes,"
  echo "  redirections like <foo>). Prefer single quotes — they disable expansion:"
  echo "    GOOD: GRAFANA_PASSWORD='2)09kdW|x'"
  echo "    BAD:  GRAFANA_PASSWORD=2)09kdW|x"
  echo "    GOOD: DISPLAY_NAME='System Administrator'"
  echo "    BAD:  DISPLAY_NAME=System Administrator"
  echo "    GOOD: TOKEN='<placeholder>'"
  echo "    BAD:  TOKEN=<placeholder>"
  echo ""
  echo "Re-run with --examples or --secrets to scope the check."
  exit 1
fi

exit 0

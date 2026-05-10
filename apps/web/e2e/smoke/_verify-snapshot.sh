#!/usr/bin/env bash
# Pre-flight check: verifica che snapshot DB contenga setup richiesto da smoke G1-G5.
# Usato dal CI workflow prima di lanciare Playwright.
#
# Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-design.md §4.1
# Plan: docs/superpowers/plans/2026-05-10-e2e-smoke-game-night.md Task 1.3
set -euo pipefail

PSQL_CMD="docker exec meepleai-postgres psql -U postgres -d meepleai -tAc"
SMOKE_EMAIL="smoke-aaron@meepleai.test"

# 1. Smoke user exists
USER_COUNT=$($PSQL_CMD "SELECT COUNT(*) FROM users WHERE \"Email\" = '$SMOKE_EMAIL';")
if [ "$USER_COUNT" -lt 1 ]; then
  echo "FAIL: smoke-aaron user not in DB. Run: psql -f tests/fixtures/smoke-test-users.sql"
  exit 1
fi

# 2. At least 1 game with ready KB
KB_COUNT=$($PSQL_CMD "SELECT COUNT(*) FROM pdf_documents WHERE processing_status = 'ready';")
if [ "$KB_COUNT" -lt 1 ]; then
  echo "FAIL: no PDF documents with status='ready'. Snapshot needs games+KB indexed."
  exit 1
fi

# 3. smoke-aaron has at least 1 game in library (warning only — bootstrap may handle)
LIB_COUNT=$($PSQL_CMD "SELECT COUNT(*) FROM user_library WHERE user_id = (SELECT \"Id\" FROM users WHERE \"Email\" = '$SMOKE_EMAIL');")
if [ "$LIB_COUNT" -lt 1 ]; then
  echo "WARNING: smoke-aaron has no games in library. Bootstrap may be needed."
  echo "Will attempt graceful continue (tests may FAIL if SEED_GAME_ID points to game not in user library)."
fi

echo "PASS: snapshot verified for smoke E2E"

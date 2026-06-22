#!/usr/bin/env bash
# rollback-trigger.sh — Thin local wrapper around .github/workflows/rollback.yml
#
# Validates inputs locally (env name, tag format, gh CLI auth) and asks for
# explicit operator confirmation before invoking the workflow.
#
# Usage:
#   bash infra/scripts/rollback-trigger.sh <staging|production> <image_tag> [--restore-db]
#
# Examples:
#   bash infra/scripts/rollback-trigger.sh staging staging-20260511-deadbee
#   bash infra/scripts/rollback-trigger.sh production v2026.05.11-deadbee --restore-db
#
# See docs/for-developers/operations/rollback-runbook.md §5 for full procedure.

set -euo pipefail

# ─────────────────────────────────────────────
# Argument parsing
# ─────────────────────────────────────────────
usage() {
  cat <<'EOF'
Usage: rollback-trigger.sh <staging|production> <image_tag> [--restore-db]

Arguments:
  environment   Target environment: 'staging' or 'production'
  image_tag     Image tag to rollback to (immutable SHA-suffixed tag, NOT 'latest')

Options:
  --restore-db  Also restore DB from latest pre-migration backup (dangerous!)
                Default: false. Only use for Scenario B (bad migration) — see runbook §6.2.

Examples:
  rollback-trigger.sh staging staging-20260511-deadbee
  rollback-trigger.sh production v2026.05.11-deadbee --restore-db

Reference:
  docs/for-developers/operations/rollback-runbook.md
EOF
  exit 1
}

[[ $# -lt 2 ]] && usage

ENVIRONMENT="$1"
IMAGE_TAG="$2"
RESTORE_DB="false"

if [[ $# -ge 3 ]]; then
  case "$3" in
    --restore-db) RESTORE_DB="true" ;;
    -h|--help) usage ;;
    *) echo "ERROR: unknown option '$3'" >&2; usage ;;
  esac
fi

# ─────────────────────────────────────────────
# Validate environment
# ─────────────────────────────────────────────
case "$ENVIRONMENT" in
  staging|production) ;;
  *)
    echo "ERROR: environment must be 'staging' or 'production' (got: '$ENVIRONMENT')" >&2
    exit 2
    ;;
esac

# ─────────────────────────────────────────────
# Validate tag format (immutable, SHA-suffixed)
# ─────────────────────────────────────────────
# Accepted patterns:
#   staging-YYYYMMDD-<7+ hex chars>
#   vYYYY.MM.DD-<7+ hex chars>
TAG_PATTERN_STAGING='^staging-[0-9]{8}-[0-9a-f]{7,}$'
TAG_PATTERN_PROD='^v[0-9]{4}\.[0-9]{2}\.[0-9]{2}-[0-9a-f]{7,}$'

if [[ "$IMAGE_TAG" == "latest" || "$IMAGE_TAG" == "staging" ]]; then
  echo "ERROR: refusing to rollback to mutable tag '$IMAGE_TAG'" >&2
  echo "       Use an immutable SHA-suffixed tag — see runbook §3.3" >&2
  exit 3
fi

if ! [[ "$IMAGE_TAG" =~ $TAG_PATTERN_STAGING || "$IMAGE_TAG" =~ $TAG_PATTERN_PROD ]]; then
  echo "WARNING: tag '$IMAGE_TAG' does not match expected patterns:" >&2
  echo "         staging-YYYYMMDD-<sha7>" >&2
  echo "         vYYYY.MM.DD-<sha7>" >&2
  echo "         Proceed only if you have manually verified the tag exists in GHCR." >&2
  read -r -p "Continue anyway? (yes/NO): " confirm
  if [[ "$confirm" != "yes" ]]; then
    echo "Aborted." >&2
    exit 4
  fi
fi

# ─────────────────────────────────────────────
# Validate gh CLI is available and authenticated
# ─────────────────────────────────────────────
if ! command -v gh >/dev/null 2>&1; then
  echo "ERROR: gh CLI not found in PATH. Install: https://cli.github.com/" >&2
  exit 5
fi

if ! gh auth status >/dev/null 2>&1; then
  echo "ERROR: gh CLI not authenticated. Run: gh auth login" >&2
  exit 6
fi

# ─────────────────────────────────────────────
# Verify tag exists in GHCR (non-fatal check)
# ─────────────────────────────────────────────
REPO="meepleAi-app/meepleai-monorepo"
echo "→ Verifying tag '$IMAGE_TAG' exists in GHCR (ghcr.io/$REPO/api:$IMAGE_TAG)..."

if gh api "/orgs/meepleAi-app/packages/container/meepleai-monorepo%2Fapi/versions" \
     --jq ".[].metadata.container.tags | select(. != null) | .[]" 2>/dev/null \
   | grep -qFx "$IMAGE_TAG"; then
  echo "  ✓ Tag found in GHCR"
else
  echo "  ⚠ Tag NOT found in GHCR — workflow will fail at 'docker pull'" >&2
  echo "    Available recent tags:" >&2
  gh api "/orgs/meepleAi-app/packages/container/meepleai-monorepo%2Fapi/versions" \
       --jq ".[0:5] | .[].metadata.container.tags | select(. != null) | .[]" 2>/dev/null \
    | head -10 | sed 's/^/      /' >&2 || true
  read -r -p "Continue anyway? (yes/NO): " confirm
  if [[ "$confirm" != "yes" ]]; then
    echo "Aborted." >&2
    exit 7
  fi
fi

# ─────────────────────────────────────────────
# Pre-flight summary + operator confirmation
# ─────────────────────────────────────────────
cat <<EOF

═══════════════════════════════════════════════════════════════════
                    ROLLBACK PRE-FLIGHT SUMMARY
═══════════════════════════════════════════════════════════════════
  Environment       : $ENVIRONMENT
  Rollback to tag   : $IMAGE_TAG
  Restore database  : $RESTORE_DB
  Target host       : $([ "$ENVIRONMENT" = "staging" ] && echo "meepleai.app (Hetzner CAX21)" || echo "meepleai.com (TBD — prod not provisioned)")
═══════════════════════════════════════════════════════════════════

Pre-flight reminders (see runbook §4):
  - Did you identify the correct previous-good tag?               (runbook §4.1)
  - Did you drain Hangfire if recent migration ran?               (runbook §4.2)
  - Did you capture baseline metrics for post-rollback compare?   (runbook §4.3)
EOF

if [[ "$RESTORE_DB" == "true" ]]; then
  cat <<EOF
  - DB RESTORE ENABLED — did you verify backup integrity?         (runbook §4.4)
  - DB RESTORE ENABLED — is this Scenario B (bad migration)?      (runbook §6.2)
EOF
fi

cat <<EOF

This will trigger '.github/workflows/rollback.yml' via workflow_dispatch.

EOF

read -r -p "Type 'rollback $ENVIRONMENT' to confirm (anything else aborts): " confirm
if [[ "$confirm" != "rollback $ENVIRONMENT" ]]; then
  echo "Aborted by operator." >&2
  exit 0
fi

# ─────────────────────────────────────────────
# Trigger workflow
# ─────────────────────────────────────────────
echo "→ Triggering rollback workflow..."

gh workflow run rollback.yml \
  -f "environment=$ENVIRONMENT" \
  -f "image_tag=$IMAGE_TAG" \
  -f "restore_db=$RESTORE_DB"

echo "  ✓ Workflow dispatched"
echo ""

# Give GitHub a moment to register the run, then surface the URL
sleep 3

RUN_ID=$(gh run list --workflow=rollback.yml --limit=1 --json databaseId --jq '.[0].databaseId' 2>/dev/null || echo "")

if [[ -n "$RUN_ID" ]]; then
  RUN_URL=$(gh run view "$RUN_ID" --json url --jq '.url')
  echo "→ Workflow run: $RUN_URL"
  echo ""
  echo "To watch progress in this terminal:"
  echo "  gh run watch $RUN_ID"
  echo ""
  echo "Next steps (runbook §9):"
  echo "  1. Wait for workflow to complete (target: ≤ 10 min)"
  echo "  2. Run smoke validation script (runbook §9.2)"
  echo "  3. Verify Grafana metrics within budget (runbook §9.1)"
  echo "  4. Post completion to Slack #critical (runbook §12.2)"
  echo "  5. Schedule post-mortem within 48 h (runbook §14)"
else
  echo "  ⚠ Could not retrieve run ID — check Actions tab on GitHub manually"
fi

#!/usr/bin/env bash
# Post-deploy Phase 1 resume — run after deploy-staging.yml completes for the
# fix #1357 tracking commit. Flips MigrationEnabled=true, force-recreates the
# API container with the new image, and monitors drain progress for the
# 216-row migration `2f50ce96-ac0c-4b0b-be22-4afa172085ca`.

set -euo pipefail

MIGRATION_ID="2f50ce96-ac0c-4b0b-be22-4afa172085ca"
API_TAG="${API_TAG:?usage: API_TAG=ghcr.io/.../api:staging-20260520-XXXXX $0}"
WEB_TAG="${WEB_TAG:-ghcr.io/meepleai-app/meepleai-monorepo/web:staging-20260520-890abe3}"

echo "=== Step 1: flip flag to true ==="
ssh meepleai-staging "sudo sed -i 's/^StorageLayout__MigrationEnabled=false/StorageLayout__MigrationEnabled=true/' /opt/meepleai/repo/infra/secrets/storage.secret && echo OK flipped"

echo ""
echo "=== Step 2: force-recreate API ==="
ssh meepleai-staging "docker stop meepleai-api && docker rm meepleai-api && cd /opt/meepleai/repo/infra && \
  API_IMAGE='${API_TAG}' WEB_IMAGE='${WEB_TAG}' \
  docker compose -f docker-compose.yml -f compose.staging.yml --profile ai-essential --profile monitoring-essential up -d --no-deps api 2>&1 | tail -5"

echo ""
echo "=== Step 3: wait API healthy + verify flag ==="
ssh meepleai-staging "until docker exec meepleai-api curl -s -o /dev/null -w '%{http_code}' http://localhost:8080/api/v1/health 2>/dev/null | grep -q 200; do sleep 2; done; echo API healthy; docker exec meepleai-api printenv | grep StorageLayout; docker logs meepleai-api --since=20s 2>&1 | grep StorageOperationOutbox | head -3"

echo ""
echo "=== Step 4: monitor drain ${MIGRATION_ID} ==="
for i in $(seq 1 40); do
  STATE=$(ssh meepleai-staging "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -t -A -F'|' -c \"SELECT \\\"Status\\\", COUNT(*) FROM storage_operation_outbox WHERE \\\"MigrationId\\\" = '${MIGRATION_ID}' GROUP BY \\\"Status\\\" ORDER BY 1;\"" 2>&1 | tr -d '\r' | paste -sd, -)
  echo "--- Cycle $i ($(date -u +%H:%M:%S)Z) --- $STATE"
  PENDING=$(echo "$STATE" | grep -oE 'Pending\|[0-9]+' | grep -oE '[0-9]+' | head -1)
  if [ -z "$PENDING" ] || [ "$PENDING" = "0" ]; then
    echo ""
    echo "🎯 Pending = 0 → migration complete"
    break
  fi
  if [ $i -lt 40 ]; then
    sleep 30
  fi
done

echo ""
echo "=== Final state ==="
ssh meepleai-staging "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -c \"SELECT \\\"Status\\\", COUNT(*) FROM storage_operation_outbox WHERE \\\"MigrationId\\\" = '${MIGRATION_ID}' GROUP BY \\\"Status\\\";\""

echo ""
echo "=== Audit pdf_uploads/ (should be 0 if all moved) ==="
ssh meepleai-staging 'docker exec meepleai-api bash -c "
NEW_MIGRATION_ID=\$(cat /proc/sys/kernel/random/uuid)
curl -sS -o /tmp/login_body.json -w \"login=%{http_code}\\n\" -X POST http://localhost:8080/api/v1/auth/login -c /tmp/cj.txt -H Content-Type:application/json -d \"{\\\"email\\\":\\\"badsworm@gmail.com\\\",\\\"password\\\":\\\"Meeple1280!!\\\"}\"
curl -sS -X POST http://localhost:8080/api/v1/admin/storage/migration/enqueue -b /tmp/cj.txt -H Content-Type:application/json -d \"{\\\"migrationId\\\":\\\"\$NEW_MIGRATION_ID\\\",\\\"legacyPrefix\\\":\\\"pdf_uploads/\\\",\\\"category\\\":\\\"Pdf\\\",\\\"dryRun\\\":true}\"
rm -f /tmp/cj.txt /tmp/login_body.json
"'

#!/bin/bash
set -euo pipefail

# =============================================================================
# MeepleAI Staging Reset - Beta0
# Wipes all data volumes, preserves ML models and secrets
# =============================================================================

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log()  { echo -e "${GREEN}[$(date '+%H:%M:%S')]${NC} $1"; }
warn() { echo -e "${YELLOW}[$(date '+%H:%M:%S')] WARNING:${NC} $1"; }
err()  { echo -e "${RED}[$(date '+%H:%M:%S')] ERROR:${NC} $1"; }

INFRA_DIR="/opt/meepleai/repo/infra"
COMPOSE_FILES="-f docker-compose.yml -f compose.staging.yml -f compose.staging-traefik.yml"
BACKUP_DIR="/backups"

# Volumes to DELETE (user data)
DATA_VOLUMES=(
  "meepleai-pgdata-staging"
  "meepleai-qdrantdata-staging"
  "meepleai-redisdata-staging"
  "meepleai-grafana-staging"
  "meepleai-prometheus-staging"
  "meepleai_alertmanager_data"
  "meepleai_minio_data"
  "meepleai_pdf_uploads"
  "meepleai_mailpit_data"
  "meepleai_unstructured_temp"
  "meepleai_smoldocling_temp"
)

# Volumes to KEEP (ML models - expensive to re-download)
KEEP_VOLUMES=(
  "meepleai_ollama_data"
  "meepleai_reranker_models"
  "meepleai_smoldocling_models"
)

echo ""
echo "================================================================="
echo "  MeepleAI Staging Reset - Beta0"
echo "  $(date '+%Y-%m-%d %H:%M:%S UTC')"
echo "================================================================="
echo ""
echo "  DELETE: ${#DATA_VOLUMES[@]} data volumes"
echo "  KEEP:   ${#KEEP_VOLUMES[@]} ML model volumes"
echo "  KEEP:   secrets/*.secret files"
echo ""
echo "================================================================="
echo ""

# --- Phase 1: Backup ---
log "Phase 1: Creating safety backup..."
mkdir -p "$BACKUP_DIR"
BACKUP_FILE="$BACKUP_DIR/pre-beta0-reset-$(date +%Y%m%d%H%M%S).sql"

cd "$INFRA_DIR"

# Source secrets for DB credentials
set -a
for f in secrets/*.secret; do source "$f" 2>/dev/null || true; done
set +a

if docker exec meepleai-postgres pg_isready -U "${POSTGRES_USER:-meeple}" > /dev/null 2>&1; then
  docker exec meepleai-postgres pg_dump -U "${POSTGRES_USER:-meeple}" "${POSTGRES_DB:-meepleai_staging}" > "$BACKUP_FILE" 2>/dev/null && \
    log "Backup saved: $BACKUP_FILE ($(du -h "$BACKUP_FILE" | cut -f1))" || \
    warn "Backup failed (non-blocking, continuing)"
else
  warn "PostgreSQL not reachable, skipping backup"
fi

# --- Phase 2: Stop all containers ---
log "Phase 2: Stopping all containers..."
docker compose $COMPOSE_FILES --profile full down --timeout 30 2>/dev/null || \
  docker stop $(docker ps -aq) 2>/dev/null || true

# Ensure everything is stopped
RUNNING=$(docker ps -q | wc -l)
if [ "$RUNNING" -gt 0 ]; then
  warn "$RUNNING containers still running, force stopping..."
  docker kill $(docker ps -q) 2>/dev/null || true
fi
log "All containers stopped"

# --- Phase 3: Remove data volumes ---
log "Phase 3: Removing data volumes..."
REMOVED=0
SKIPPED=0
for vol in "${DATA_VOLUMES[@]}"; do
  if docker volume inspect "$vol" > /dev/null 2>&1; then
    docker volume rm "$vol" && \
      log "  Removed: $vol" && ((REMOVED++)) || \
      { err "  Failed to remove: $vol"; }
  else
    ((SKIPPED++))
  fi
done
log "Volumes removed: $REMOVED, skipped (not found): $SKIPPED"

echo ""
log "Preserved ML model volumes:"
for vol in "${KEEP_VOLUMES[@]}"; do
  if docker volume inspect "$vol" > /dev/null 2>&1; then
    SIZE=$(docker system df -v 2>/dev/null | grep "$vol" | awk '{print $3}' || echo "?")
    log "  KEPT: $vol ($SIZE)"
  fi
done

# --- Phase 4: Prune old images ---
log "Phase 4: Pruning unused Docker images..."
BEFORE=$(docker system df --format '{{.Size}}' 2>/dev/null | head -1)
docker image prune -af --filter "until=24h" 2>/dev/null || docker image prune -f 2>/dev/null || true
AFTER=$(docker system df --format '{{.Size}}' 2>/dev/null | head -1)
log "Image cleanup done (before: $BEFORE, after: $AFTER)"

# --- Phase 5: Restart services ---
log "Phase 5: Pulling latest images and starting services..."

# Pull latest staging images
docker pull ghcr.io/degrassiaaron/meepleai-monorepo/api:staging-latest 2>/dev/null && \
  log "  Pulled: API staging-latest" || warn "  API image pull failed (will use cached)"
docker pull ghcr.io/degrassiaaron/meepleai-monorepo/web:staging-latest 2>/dev/null && \
  log "  Pulled: Web staging-latest" || warn "  Web image pull failed (will use cached)"

# Start core services first (DB, cache, vector)
log "Starting core services (postgres, redis, qdrant)..."
docker compose $COMPOSE_FILES up -d postgres redis qdrant
log "Waiting 15s for databases to initialize..."
sleep 15

# Verify core services
for svc in postgres redis qdrant; do
  if docker ps --filter "name=meepleai-$svc" --filter "status=running" -q | grep -q .; then
    log "  $svc: running"
  else
    err "  $svc: NOT running!"
  fi
done

# Start all remaining services
log "Starting all services..."
docker compose $COMPOSE_FILES --profile full up -d

# Wait for API to be healthy
log "Waiting for API health (migrations applying on first boot)..."
for attempt in $(seq 1 40); do
  if curl -sf http://localhost:8080/health > /dev/null 2>&1; then
    log "API healthy after ~$((attempt*5))s"
    break
  fi
  if [ "$attempt" -eq 40 ]; then
    err "API did not become healthy within 200s"
    echo "--- API LOGS (last 50 lines) ---"
    docker logs meepleai-api --tail=50 2>&1
    exit 1
  fi
  sleep 5
done

# --- Phase 6: Verification ---
echo ""
log "Phase 6: Verification..."
echo ""

# Health check
HEALTH=$(curl -sf http://localhost:8080/health 2>/dev/null || echo '{}')
echo "$HEALTH" | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin)
    print(f\"  API Status: {d.get('status', 'unknown')}\")
    for c in d.get('checks', []):
        icon = '  OK' if c['status'] == 'Healthy' else '  FAIL'
        print(f\"  {icon}  {c['name']}: {c['status']}\")
except:
    print('  Could not parse health response')
" 2>/dev/null || echo "  Health endpoint returned: $HEALTH"

# Container status
echo ""
log "Container status:"
docker ps --format 'table {{.Names}}\t{{.Status}}' | head -25

# Volume status
echo ""
log "Volume status:"
docker volume ls --format '{{.Name}}' | sort

# Disk usage
echo ""
log "Disk usage:"
df -h / | tail -1
docker system df 2>/dev/null

echo ""
echo "================================================================="
echo "  Beta0 Reset Complete!"
echo "  $(date '+%Y-%m-%d %H:%M:%S UTC')"
echo ""
echo "  - All data volumes wiped"
echo "  - ML models preserved"
echo "  - Secrets untouched"
echo "  - Fresh DB with EF Core migrations applied"
echo "  - All services running"
echo ""
echo "  Backup: $BACKUP_FILE"
echo "  URL: https://meepleai.app"
echo "================================================================="

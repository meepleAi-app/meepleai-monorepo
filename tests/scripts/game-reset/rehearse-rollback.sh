#!/usr/bin/env bash
# End-to-end rollback rehearsal:
# 1. Spin up disposable postgres container
# 2. Apply synthetic schema + seed
# 3. Backup → mutate → rollback
# 4. Verify state matches original
# 5. Teardown

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
FIXTURES="$SCRIPT_DIR/fixtures"

CONTAINER_NAME="meepleai-rollback-rehearse"
DB_USER="rehearse"
DB_PASS="rehearse"
DB_NAME="rehearse_db"
DB_PORT="55432"
DB_HOST="localhost"

cleanup() {
  docker rm -f "$CONTAINER_NAME" > /dev/null 2>&1 || true
  rm -rf "$REPO_ROOT/infra/backups/rehearse-"*.dump 2>/dev/null || true
}
trap cleanup EXIT

echo "[1/7] Spinning up disposable postgres..."
docker run -d --rm \
  --name "$CONTAINER_NAME" \
  -e POSTGRES_USER="$DB_USER" \
  -e POSTGRES_PASSWORD="$DB_PASS" \
  -e POSTGRES_DB="$DB_NAME" \
  -p "$DB_PORT:5432" \
  pgvector/pgvector:pg16 > /dev/null

echo "[2/7] Waiting for postgres to be ready..."
for i in {1..30}; do
  if docker exec "$CONTAINER_NAME" pg_isready -U "$DB_USER" > /dev/null 2>&1; then
    break
  fi
  sleep 1
done

DB_URL="postgresql://$DB_USER:$DB_PASS@$DB_HOST:$DB_PORT/$DB_NAME"
DB_URL_ADMIN="postgresql://$DB_USER:$DB_PASS@$DB_HOST:$DB_PORT/postgres"

echo "[3/7] Loading synthetic schema and seed data..."
PGPASSWORD="$DB_PASS" psql "$DB_URL" -f "$FIXTURES/synthetic-dump.sql" > /dev/null

original_count=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;")
original_games=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM games;")
echo "    Original state: $original_games games, $original_count vectors"

echo "[4/7] Creating temp env file and running 01-backup.sh..."
TMP_ENV="$(mktemp)"
cat > "$TMP_ENV" <<EOF
ENV_NAME="rehearse"
DATABASE_URL="$DB_URL"
DATABASE_URL_ADMIN="$DB_URL_ADMIN"
DATABASE_NAME="$DB_NAME"
BACKUP_DIR="$REPO_ROOT/infra/backups"
EOF

PGPASSWORD="$DB_PASS" "$REPO_ROOT/infra/scripts/game-reset/01-backup.sh" "$TMP_ENV"
dump_file=$(ls -t "$REPO_ROOT/infra/backups/"*-rehearse-pre-game-reset.dump | head -1)
echo "    Dump: $dump_file"

echo "[5/7] Mutating DB (delete half the vectors, all the games)..."
PGPASSWORD="$DB_PASS" psql "$DB_URL" -c "DELETE FROM pgvector_embeddings WHERE ctid IN (SELECT ctid FROM pgvector_embeddings LIMIT 3);" > /dev/null
PGPASSWORD="$DB_PASS" psql "$DB_URL" -c "DELETE FROM games;" > /dev/null
mutated_count=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;")
mutated_games=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM games;")
echo "    Mutated state: $mutated_games games, $mutated_count vectors"

if [[ "$mutated_count" == "$original_count" ]] || [[ "$mutated_games" == "$original_games" ]]; then
  echo "[FAIL] Mutation didn't take effect — rehearsal is invalid."
  exit 1
fi

echo "[6/7] Running 99-rollback.sh..."
PGPASSWORD="$DB_PASS" "$REPO_ROOT/infra/scripts/game-reset/99-rollback.sh" "$TMP_ENV" "$dump_file" --i-mean-it

echo "[7/7] Verifying restored state matches original..."
restored_count=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;")
restored_games=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM games;")
echo "    Restored state: $restored_games games, $restored_count vectors"

rm -f "$TMP_ENV"

if [[ "$restored_count" != "$original_count" ]]; then
  echo "[FAIL] Vector count mismatch: expected $original_count, got $restored_count"
  exit 1
fi
if [[ "$restored_games" != "$original_games" ]]; then
  echo "[FAIL] Game count mismatch: expected $original_games, got $restored_games"
  exit 1
fi

echo ""
echo "[PASS] Rollback rehearsal succeeded:"
echo "  - Backup created from clean state"
echo "  - DB mutated then rolled back"
echo "  - Final state matches original ($restored_games games, $restored_count vectors)"

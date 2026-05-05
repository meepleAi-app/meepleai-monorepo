#!/usr/bin/env bash
set -euo pipefail

command -v age >/dev/null 2>&1 || { echo "ERROR: age not found. Install age for encryption."; exit 1; }
[ -f /etc/age.pub ] || { echo "ERROR: /etc/age.pub missing. Cannot encrypt backups."; exit 1; }

BACKUP_DIR="/mnt/storagebox/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
RETENTION_DAYS=30

mkdir -p "$BACKUP_DIR/postgres" "$BACKUP_DIR/redis" "$BACKUP_DIR/blob"

# PostgreSQL backup (compressed)
echo "==> Backing up PostgreSQL"
docker exec meepleai-postgres pg_dump -U meepleai meepleai_db | \
  gzip > "$BACKUP_DIR/postgres/postgres_${TIMESTAMP}.sql.gz"

# Redis backup (RDB snapshot)
echo "==> Backing up Redis"
docker exec meepleai-redis redis-cli --rdb /tmp/dump.rdb
docker cp meepleai-redis:/tmp/dump.rdb "$BACKUP_DIR/redis/redis_${TIMESTAMP}.rdb"

# Blob storage backup (rsync to box) — actual path depends on STORAGE_PROVIDER config
echo "==> Backing up blob storage"
if [ -d "/var/lib/meepleai/blob" ]; then
  rsync -av --delete /var/lib/meepleai/blob/ "$BACKUP_DIR/blob/"
else
  echo "WARNING: blob path /var/lib/meepleai/blob/ not found — skipping (check STORAGE_PROVIDER config; if S3/R2, blob is already offsite)"
fi

# Encrypt with age (recipient public key in /etc/age.pub)
echo "==> Encrypting backups"
for f in "$BACKUP_DIR/postgres/postgres_${TIMESTAMP}.sql.gz" "$BACKUP_DIR/redis/redis_${TIMESTAMP}.rdb"; do
  age -R /etc/age.pub -o "${f}.age" "$f" && rm "$f"
done

# Retention: delete backups older than RETENTION_DAYS
find "$BACKUP_DIR/postgres" -name "*.age" -mtime +$RETENTION_DAYS -delete
find "$BACKUP_DIR/redis" -name "*.age" -mtime +$RETENTION_DAYS -delete

echo "==> Backup complete: $TIMESTAMP"

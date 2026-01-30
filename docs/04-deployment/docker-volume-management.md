# Docker Volume Management Guide

> **Scope**: Complete guide to Docker volume management, backup strategies, and data persistence for MeepleAI
> **Last Updated**: 2026-01-30

---

## Table of Contents

1. [Overview](#overview)
2. [Volume Types](#volume-types)
3. [MeepleAI Volume Architecture](#meepleai-volume-architecture)
4. [Named vs Anonymous Volumes](#named-vs-anonymous-volumes)
5. [Volume Operations](#volume-operations)
6. [Backup Strategies](#backup-strategies)
7. [Restore Procedures](#restore-procedures)
8. [Volume Migration](#volume-migration)
9. [Best Practices](#best-practices)
10. [Troubleshooting](#troubleshooting)

---

## Overview

### Why Volume Management Matters

Docker volumes provide **persistent storage** for containers:

- **Data Persistence**: Survives container restarts and rebuilds
- **Performance**: Better than bind mounts for I/O-intensive operations
- **Portability**: Easy to backup, restore, and migrate
- **Isolation**: Separates data from application code
- **Security**: Managed by Docker, not exposed to host filesystem

### MeepleAI Data Criticality

| Volume | Criticality | Backup Frequency | Retention | Size |
|--------|-------------|------------------|-----------|------|
| `postgres_data` | **CRITICAL** | Hourly (prod) / Daily (dev) | 30 days | 5-50 GB |
| `qdrant_data` | **CRITICAL** | Daily | 14 days | 10-100 GB |
| `pdf_uploads` | **HIGH** | Daily | 30 days | 20-200 GB |
| `prometheus_data` | **MEDIUM** | Weekly | 7 days | 5-20 GB |
| `grafana_data` | **MEDIUM** | Weekly | 7 days | 1-5 GB |
| `redis_data` | **LOW** | No backup (cache) | N/A | 1-5 GB |
| `ollama_data` | **LOW** | No backup (models) | N/A | 10-50 GB |
| Temp volumes | **NONE** | No backup | N/A | 1-10 GB |

---

## Volume Types

### 1. Named Volumes (✅ Recommended)

**Explicitly defined volumes with human-readable names**:

```yaml
# docker-compose.yml
services:
  postgres:
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:  # Named volume declaration
```

**Naming Convention**: `{project}_{service}_{purpose}`

```bash
# Docker generates: {compose-project}_{volume-name}
# Example: meepleai_postgres_data
```

**Advantages**:
- ✅ Easy to identify: `meepleai_postgres_data` vs `a7f3d8c9b1e2...`
- ✅ Persistent across `docker compose down`
- ✅ Easy to backup/restore
- ✅ Shared across multiple containers
- ✅ Managed lifecycle (explicit removal)

### 2. Anonymous Volumes (❌ Avoid)

**Auto-generated volumes without explicit names**:

```yaml
# BAD: Anonymous volume (no name)
services:
  postgres:
    volumes:
      - /var/lib/postgresql/data  # Docker generates random hash
```

**Result**: `docker volume ls` shows `7a3d8c9b1e2f48a5c3d1e9f0b4a6c8d2`

**Disadvantages**:
- ❌ Hard to identify
- ❌ Orphaned volumes accumulate
- ❌ Difficult to backup
- ❌ Removed with `docker compose down -v`
- ❌ Cannot share across containers

### 3. Bind Mounts (⚠️ Use Sparingly)

**Direct host directory mapping**:

```yaml
# Use only for development/config files
services:
  api:
    volumes:
      - ./config:/app/config:ro  # Read-only config
      - ./logs:/app/logs         # Development logs
```

**When to Use**:
- ✅ Development: Source code hot-reload
- ✅ Configuration: Read-only config files
- ✅ Logs: Easy access from host
- ❌ **Never** for production data (postgres, qdrant, etc.)

**Disadvantages**:
- Performance overhead on Windows/Mac
- Host-dependent paths
- Permission issues
- Security concerns (expose host filesystem)

---

## MeepleAI Volume Architecture

### Complete Volume Map

```yaml
# infra/docker-compose.yml
name: meepleai  # Project name ensures consistent naming

services:
  # DATABASE & CACHE (CRITICAL DATA)
  postgres:
    volumes:
      - postgres_data:/var/lib/postgresql/data      # Database files
      - ./init/postgres-init.sql:/docker-entrypoint-initdb.d/init.sql:ro

  qdrant:
    volumes:
      - qdrant_data:/qdrant/storage                  # Vector database

  redis:
    # No volume = ephemeral cache (acceptable for caching)

  # APPLICATION DATA
  api:
    volumes:
      - pdf_uploads:/app/pdf_uploads                 # User-uploaded PDFs
      - ./secrets:/app/infra/secrets:ro              # Read-only secrets

  # ML SERVICES (MODEL CACHES)
  ollama:
    volumes:
      - ollama_data:/root/.ollama                    # Downloaded models

  smoldocling-service:
    volumes:
      - smoldocling_models:/root/.cache/huggingface  # Hugging Face models
      - smoldocling_temp:/tmp/pdf-processing         # Temporary processing

  reranker-service:
    volumes:
      - reranker_models:/home/reranker/.cache/huggingface

  # TEMPORARY PROCESSING
  unstructured-service:
    volumes:
      - unstructured_temp:/tmp/pdf-extraction        # Temporary files

  # OBSERVABILITY
  prometheus:
    volumes:
      - prometheus_data:/prometheus                  # Metrics data

  grafana:
    volumes:
      - grafana_data:/var/lib/grafana               # Dashboards, users

  alertmanager:
    volumes:
      - alertmanager_data:/alertmanager             # Alert state

  # DEVELOPMENT TOOLS
  mailpit:
    volumes:
      - mailpit_data:/data                          # Email test data

volumes:
  # CRITICAL (backup required)
  postgres_data:
  qdrant_data:
  pdf_uploads:

  # ML SERVICE CACHES (rebuildable, no backup)
  ollama_data:
  smoldocling_models:
  reranker_models:

  # TEMPORARY (ephemeral, no backup)
  unstructured_temp:
  smoldocling_temp:

  # OBSERVABILITY (medium priority backup)
  prometheus_data:
  grafana_data:
  alertmanager_data:

  # DEVELOPMENT (no backup in production)
  mailpit_data:
```

### Volume Naming

**Project Name Control**:

```yaml
# docker-compose.yml
name: meepleai  # Ensures all volumes start with "meepleai_"
```

**Result**:
```bash
docker volume ls
# DRIVER    VOLUME NAME
# local     meepleai_postgres_data
# local     meepleai_qdrant_data
# local     meepleai_pdf_uploads
```

**Without `name:`** (directory-dependent):
```bash
# If run from /opt/meepleai/infra:
# local     infra_postgres_data

# If run from /home/user/project/infra:
# local     project_postgres_data
```

---

## Named vs Anonymous Volumes

### Comparison Table

| Feature | Named Volume | Anonymous Volume |
|---------|--------------|------------------|
| **Identification** | `meepleai_postgres_data` | `a7f3d8c9b1e2...` (64-char hash) |
| **Creation** | Explicit in `volumes:` section | Auto-created from container `VOLUME` |
| **Persistence** | Survives `docker compose down` | Survives `docker compose down` |
| **Removal** | Explicit `docker volume rm` | Removed with `docker compose down -v` |
| **Sharing** | Can share across services | Tied to single container |
| **Backup** | Easy to identify and backup | Hard to identify |
| **Migration** | Simple with volume name | Requires finding correct hash |
| **Debugging** | Clear purpose from name | Must inspect container to find |

### How Anonymous Volumes Are Created

**Dockerfile** (creates anonymous volume):
```dockerfile
FROM postgres:16
VOLUME /var/lib/postgresql/data  # Anonymous if not overridden
```

**docker-compose.yml** (overrides with named):
```yaml
services:
  postgres:
    image: postgres:16
    volumes:
      - postgres_data:/var/lib/postgresql/data  # Named (overrides VOLUME)

volumes:
  postgres_data:  # Explicit named volume
```

### Finding and Removing Anonymous Volumes

```bash
# List all volumes
docker volume ls

# Filter anonymous volumes (long hash names)
docker volume ls --filter "dangling=true"

# Remove all anonymous volumes
docker volume prune

# WARNING: Only run if sure no important data!
# MeepleAI uses named volumes, so this is safe:
docker volume prune -f
```

---

## Volume Operations

### Create Volume

```bash
# Create explicitly (usually done by docker compose)
docker volume create meepleai_postgres_data

# Create with driver options
docker volume create \
  --driver local \
  --opt type=none \
  --opt device=/mnt/data/postgres \
  --opt o=bind \
  meepleai_postgres_data
```

### Inspect Volume

```bash
# Get volume details
docker volume inspect meepleai_postgres_data

# Output:
[
  {
    "CreatedAt": "2026-01-30T10:00:00Z",
    "Driver": "local",
    "Labels": {
      "com.docker.compose.project": "meepleai",
      "com.docker.compose.volume": "postgres_data"
    },
    "Mountpoint": "/var/lib/docker/volumes/meepleai_postgres_data/_data",
    "Name": "meepleai_postgres_data",
    "Scope": "local"
  }
]

# Get just the mountpoint
docker volume inspect -f '{{ .Mountpoint }}' meepleai_postgres_data
```

### List Volumes

```bash
# All volumes
docker volume ls

# Filter by project
docker volume ls --filter label=com.docker.compose.project=meepleai

# Filter by service
docker volume ls --filter label=com.docker.compose.volume=postgres_data

# Show size (requires privileges)
docker system df -v
```

### Remove Volume

```bash
# Remove specific volume (must be unused)
docker volume rm meepleai_postgres_data

# Force remove (stops using containers first)
docker compose -f docker-compose.yml down
docker volume rm meepleai_postgres_data

# Remove all unused volumes
docker volume prune

# Remove all project volumes (DESTRUCTIVE!)
docker volume ls --filter label=com.docker.compose.project=meepleai -q | xargs docker volume rm
```

### Copy Data Between Volumes

```bash
# Create new volume
docker volume create meepleai_postgres_data_backup

# Copy data using temporary container
docker run --rm \
  -v meepleai_postgres_data:/source:ro \
  -v meepleai_postgres_data_backup:/target \
  alpine \
  sh -c "cp -a /source/. /target/"

# Verify
docker run --rm -v meepleai_postgres_data_backup:/data alpine ls -la /data
```

---

## Backup Strategies

### 1. Database Dump (PostgreSQL)

**Recommended for PostgreSQL**:

```bash
# Manual backup (while running)
docker compose exec postgres pg_dump -U postgres meepleai > backup.sql

# With compression
docker compose exec postgres pg_dump -U postgres meepleai | gzip > backup-$(date +%Y%m%d).sql.gz

# Backup all databases
docker compose exec postgres pg_dumpall -U postgres > backup-all-$(date +%Y%m%d).sql

# Restore
docker compose exec -T postgres psql -U postgres meepleai < backup.sql
```

**Automated Backup Script**:

```bash
#!/bin/bash
# /opt/meepleai/scripts/backup-postgres.sh

BACKUP_DIR="/backups/postgres"
RETENTION_DAYS=30

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Generate backup
BACKUP_FILE="$BACKUP_DIR/meepleai-$(date +%Y%m%d-%H%M%S).sql.gz"
docker compose -f /opt/meepleai/infra/docker-compose.yml \
  exec -T postgres \
  pg_dump -U postgres meepleai | gzip > "$BACKUP_FILE"

# Verify backup
if [ -f "$BACKUP_FILE" ] && [ -s "$BACKUP_FILE" ]; then
  echo "✅ Backup successful: $BACKUP_FILE"
else
  echo "❌ Backup failed!"
  exit 1
fi

# Cleanup old backups
find "$BACKUP_DIR" -name "meepleai-*.sql.gz" -mtime +$RETENTION_DAYS -delete

# Upload to cloud storage (optional)
# aws s3 cp "$BACKUP_FILE" s3://meepleai-backups/postgres/
```

**Cron Schedule**:

```bash
# Edit crontab
crontab -e

# Hourly backups (production)
0 * * * * /opt/meepleai/scripts/backup-postgres.sh >> /var/log/meepleai-backup.log 2>&1

# Daily backups (staging)
0 3 * * * /opt/meepleai/scripts/backup-postgres.sh >> /var/log/meepleai-backup.log 2>&1
```

### 2. Volume Tar Backup

**For non-database volumes** (pdf_uploads, qdrant_data, etc.):

```bash
# Backup volume to tar.gz
docker run --rm \
  -v meepleai_pdf_uploads:/source:ro \
  -v /backups:/backup \
  alpine \
  tar czf /backup/pdf-uploads-$(date +%Y%m%d).tar.gz -C /source .

# Verify backup
tar tzf /backups/pdf-uploads-$(date +%Y%m%d).tar.gz | head

# Restore volume from tar.gz
docker run --rm \
  -v meepleai_pdf_uploads:/target \
  -v /backups:/backup \
  alpine \
  tar xzf /backup/pdf-uploads-20260130.tar.gz -C /target
```

### 3. Qdrant Backup

**Vector database backup**:

```bash
# Create Qdrant snapshot
curl -X POST "http://localhost:6333/collections/{collection_name}/snapshots"

# Download snapshot
curl "http://localhost:6333/collections/{collection_name}/snapshots/{snapshot_name}" \
  -o qdrant-snapshot-$(date +%Y%m%d).snapshot

# OR: Volume backup (simpler but less optimal)
docker run --rm \
  -v meepleai_qdrant_data:/source:ro \
  -v /backups:/backup \
  alpine \
  tar czf /backup/qdrant-data-$(date +%Y%m%d).tar.gz -C /source .
```

### 4. Complete Backup Script

**Comprehensive MeepleAI backup**:

```bash
#!/bin/bash
# /opt/meepleai/scripts/backup-all.sh

set -e

BACKUP_ROOT="/backups"
DATE=$(date +%Y%m%d-%H%M%S)
BACKUP_DIR="$BACKUP_ROOT/$DATE"

mkdir -p "$BACKUP_DIR"

echo "🔄 Starting full backup: $DATE"

# 1. PostgreSQL Database
echo "📦 Backing up PostgreSQL..."
docker compose -f /opt/meepleai/infra/docker-compose.yml \
  exec -T postgres \
  pg_dump -U postgres meepleai | gzip > "$BACKUP_DIR/postgres.sql.gz"

# 2. Qdrant Vector Database
echo "📦 Backing up Qdrant..."
docker run --rm \
  -v meepleai_qdrant_data:/source:ro \
  -v "$BACKUP_DIR":/backup \
  alpine \
  tar czf /backup/qdrant.tar.gz -C /source .

# 3. PDF Uploads
echo "📦 Backing up PDF uploads..."
docker run --rm \
  -v meepleai_pdf_uploads:/source:ro \
  -v "$BACKUP_DIR":/backup \
  alpine \
  tar czf /backup/pdf-uploads.tar.gz -C /source .

# 4. Prometheus Data
echo "📦 Backing up Prometheus..."
docker run --rm \
  -v meepleai_prometheus_data:/source:ro \
  -v "$BACKUP_DIR":/backup \
  alpine \
  tar czf /backup/prometheus.tar.gz -C /source .

# 5. Grafana Dashboards
echo "📦 Backing up Grafana..."
docker run --rm \
  -v meepleai_grafana_data:/source:ro \
  -v "$BACKUP_DIR":/backup \
  alpine \
  tar czf /backup/grafana.tar.gz -C /source .

# 6. Verify all backups
echo "✅ Verifying backups..."
for file in postgres.sql.gz qdrant.tar.gz pdf-uploads.tar.gz prometheus.tar.gz grafana.tar.gz; do
  if [ ! -s "$BACKUP_DIR/$file" ]; then
    echo "❌ Backup failed: $file"
    exit 1
  fi
done

# 7. Create manifest
cat > "$BACKUP_DIR/manifest.txt" << EOF
MeepleAI Backup Manifest
Date: $DATE
Host: $(hostname)
Services:
$(docker compose -f /opt/meepleai/infra/docker-compose.yml ps --format json)

Files:
$(ls -lh "$BACKUP_DIR")

Checksums:
$(cd "$BACKUP_DIR" && sha256sum *.gz *.tar.gz)
EOF

echo "✅ Backup complete: $BACKUP_DIR"
echo "📊 Total size: $(du -sh "$BACKUP_DIR" | cut -f1)"

# 8. Cleanup old backups (keep 30 days)
find "$BACKUP_ROOT" -maxdepth 1 -type d -mtime +30 -exec rm -rf {} \;

# 9. Upload to cloud (optional)
# aws s3 sync "$BACKUP_DIR" s3://meepleai-backups/$DATE/
```

**Cron Schedule**:

```bash
# Daily at 3 AM
0 3 * * * /opt/meepleai/scripts/backup-all.sh >> /var/log/meepleai-backup-all.log 2>&1
```

---

## Restore Procedures

### 1. PostgreSQL Restore

```bash
# Stop API (prevent writes during restore)
docker compose stop api

# Restore from SQL dump
gunzip -c /backups/postgres.sql.gz | \
  docker compose exec -T postgres psql -U postgres meepleai

# OR: Restore from specific backup
docker compose exec -T postgres psql -U postgres meepleai < /backups/backup-20260130.sql

# Restart API
docker compose start api

# Verify
docker compose exec postgres psql -U postgres meepleai -c "SELECT COUNT(*) FROM games;"
```

### 2. Volume Restore

```bash
# Stop service using volume
docker compose stop api

# Remove existing volume (DESTRUCTIVE!)
docker volume rm meepleai_pdf_uploads

# Create new empty volume
docker volume create meepleai_pdf_uploads

# Restore from tar backup
docker run --rm \
  -v meepleai_pdf_uploads:/target \
  -v /backups:/backup \
  alpine \
  tar xzf /backup/pdf-uploads-20260130.tar.gz -C /target

# Restart service
docker compose start api

# Verify
docker run --rm -v meepleai_pdf_uploads:/data alpine ls -la /data
```

### 3. Complete Disaster Recovery

**Scenario**: Complete server failure, restore from backups

```bash
# 1. Fresh MeepleAI installation
cd /opt/meepleai
git clone <repo>

# 2. Setup secrets
cd infra/secrets
./setup-prod-secrets.sh

# 3. Download backups
mkdir -p /backups/restore
cd /backups/restore
# Download from cloud storage
# aws s3 sync s3://meepleai-backups/20260130-030000/ .

# 4. Create volumes
docker volume create meepleai_postgres_data
docker volume create meepleai_qdrant_data
docker volume create meepleai_pdf_uploads

# 5. Restore PostgreSQL
docker compose up -d postgres
sleep 10
gunzip -c /backups/restore/postgres.sql.gz | \
  docker compose exec -T postgres psql -U postgres meepleai

# 6. Restore Qdrant
docker run --rm \
  -v meepleai_qdrant_data:/target \
  -v /backups/restore:/backup \
  alpine \
  tar xzf /backup/qdrant.tar.gz -C /target

# 7. Restore PDF uploads
docker run --rm \
  -v meepleai_pdf_uploads:/target \
  -v /backups/restore:/backup \
  alpine \
  tar xzf /backup/pdf-uploads.tar.gz -C /target

# 8. Start all services
docker compose up -d

# 9. Verify
curl -sf https://www.meepleai.io/health
```

---

## Volume Migration

### Scenario 1: Migrate to New Server

```bash
# OLD SERVER
# 1. Create backups (see Backup Strategies)
/opt/meepleai/scripts/backup-all.sh

# 2. Transfer backups to new server
rsync -avz /backups/ user@new-server:/backups/

# NEW SERVER
# 3. Follow disaster recovery procedure (see above)
```

### Scenario 2: Migrate Volume to Different Storage

```bash
# Example: Move postgres_data to SSD mount

# 1. Stop service
docker compose stop postgres

# 2. Create backup
docker run --rm \
  -v meepleai_postgres_data:/source:ro \
  -v /tmp:/backup \
  alpine \
  tar czf /backup/postgres-migration.tar.gz -C /source .

# 3. Remove old volume
docker volume rm meepleai_postgres_data

# 4. Create volume on new storage
docker volume create \
  --driver local \
  --opt type=none \
  --opt device=/mnt/ssd/postgres \
  --opt o=bind \
  meepleai_postgres_data

# 5. Restore data
docker run --rm \
  -v meepleai_postgres_data:/target \
  -v /tmp:/backup \
  alpine \
  tar xzf /backup/postgres-migration.tar.gz -C /target

# 6. Restart service
docker compose start postgres
```

---

## Best Practices

### 1. Always Use Named Volumes

**✅ DO**:
```yaml
volumes:
  postgres_data:
  qdrant_data:
  pdf_uploads:
```

**❌ DON'T**:
```yaml
# No volume declaration = anonymous
services:
  postgres:
    volumes:
      - /var/lib/postgresql/data
```

### 2. Consistent Naming Convention

**Pattern**: `{project}_{service}_{purpose}`

```yaml
name: meepleai

volumes:
  postgres_data:        # meepleai_postgres_data
  qdrant_data:          # meepleai_qdrant_data
  pdf_uploads:          # meepleai_pdf_uploads
  prometheus_data:      # meepleai_prometheus_data
```

### 3. Regular Backups

```bash
# Production: Hourly database, daily volumes
0 * * * * /opt/meepleai/scripts/backup-postgres.sh
0 3 * * * /opt/meepleai/scripts/backup-all.sh

# Staging: Daily database
0 3 * * * /opt/meepleai/scripts/backup-postgres.sh
```

### 4. Test Restores

```bash
# Monthly restore test
# 1. Restore to test environment
# 2. Verify data integrity
# 3. Document restore time
# 4. Update disaster recovery plan
```

### 5. Monitor Volume Usage

```bash
# Check disk space
docker system df -v

# Alert if volumes > 80% disk capacity
du -sh /var/lib/docker/volumes/* | sort -hr | head
```

### 6. Cleanup Strategy

```bash
# Regular cleanup of temporary volumes
docker volume prune -f

# Keep named volumes
docker volume ls --filter label=com.docker.compose.project=meepleai
```

---

## Troubleshooting

### Issue: Volume Not Found

```bash
# Error: volume "meepleai_postgres_data" not found

# Solution 1: Create volume
docker volume create meepleai_postgres_data

# Solution 2: Let docker compose create it
docker compose up -d postgres
```

### Issue: Permission Denied

```bash
# Error: Permission denied accessing volume

# Solution: Fix ownership
docker run --rm \
  -v meepleai_postgres_data:/data \
  alpine \
  chown -R 999:999 /data  # PostgreSQL user
```

### Issue: Volume Full

```bash
# Check disk usage
df -h /var/lib/docker/volumes

# Clean up old data
docker system prune -a --volumes

# Expand disk or migrate to larger storage
```

### Issue: Data Corruption

```bash
# PostgreSQL corruption
docker compose stop postgres

# Attempt repair
docker compose exec postgres pg_resetwal /var/lib/postgresql/data

# If repair fails, restore from backup
gunzip -c /backups/postgres.sql.gz | \
  docker compose exec -T postgres psql -U postgres meepleai
```

---

## Quick Reference

### Common Commands

```bash
# List volumes
docker volume ls

# Inspect volume
docker volume inspect meepleai_postgres_data

# Backup volume
docker run --rm -v meepleai_postgres_data:/source:ro -v /backups:/backup alpine tar czf /backup/data.tar.gz -C /source .

# Restore volume
docker run --rm -v meepleai_postgres_data:/target -v /backups:/backup alpine tar xzf /backup/data.tar.gz -C /target

# Remove volume
docker volume rm meepleai_postgres_data

# Prune unused
docker volume prune -f
```

---

## Additional Resources

- [Docker Volumes Documentation](https://docs.docker.com/storage/volumes/)
- [PostgreSQL Backup Best Practices](https://www.postgresql.org/docs/current/backup.html)
- [Disaster Recovery Runbook](./runbooks/disaster-recovery.md)
- [Backup & Restore Runbook](./runbooks/backup-restore.md)

---

**Next**: [Deployment Secrets for Production](./deployment-secrets-production.md)

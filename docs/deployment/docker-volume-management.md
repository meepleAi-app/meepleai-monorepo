# Docker Volume Management Guide

> **Scope**: Volume management, backup strategies, and data persistence for MeepleAI
> **Last Updated**: 2026-01-30

## Volume Types Comparison

| Feature | Named Volume ✅ | Anonymous Volume ❌ | Bind Mount ⚠️ |
|---------|----------------|---------------------|---------------|
| **Name** | `meepleai_postgres_data` | `a7f3d8c9b1e2...` | Host path |
| **Identification** | Easy | Hash (hard) | Path-dependent |
| **Persistence** | Until explicit `rm` | Until `down -v` | Host file |
| **Sharing** | Multi-container | Single container | Multi-container |
| **Backup** | Simple | Complex | Simple |
| **Performance** | Best | Best | Overhead (Win/Mac) |
| **Use Case** | Production data ✅ | Never use ❌ | Dev only ⚠️ |

## MeepleAI Volume Map

| Volume | Criticality | Backup | Retention | Size |
|--------|-------------|--------|-----------|------|
| `postgres_data` | 🔴 CRITICAL | Hourly (prod) / Daily (dev) | 30d | 5-50GB |
| `qdrant_data` | 🔴 CRITICAL | Daily | 14d | 10-100GB |
| `pdf_uploads` | 🟡 HIGH | Daily | 30d | 20-200GB |
| `prometheus_data` | 🟢 MEDIUM | Weekly | 7d | 5-20GB |
| `grafana_data` | 🟢 MEDIUM | Weekly | 7d | 1-5GB |
| `redis_data` | ⚪ LOW | No (cache) | N/A | 1-5GB |
| `ollama_data` | ⚪ LOW | No (models) | N/A | 10-50GB |

### docker-compose.yml Configuration
```yaml
name: meepleai  # Ensures meepleai_* prefix

services:
  postgres:
    volumes:
      - postgres_data:/var/lib/postgresql/data  # Named override

  redis:
    # No volume = ephemeral (acceptable for cache)

volumes:
  postgres_data:  # Explicit named volume
  qdrant_data:
  pdf_uploads:
```

---

## Volume Operations

### Basic Commands
```bash
# List
docker volume ls
docker volume ls --filter label=com.docker.compose.project=meepleai

# Inspect
docker volume inspect meepleai_postgres_data
docker volume inspect -f '{{.Mountpoint}}' meepleai_postgres_data

# Remove (must be unused)
docker volume rm meepleai_postgres_data

# Prune dangling
docker volume prune -f
```

### Copy Between Volumes
```bash
# Create backup volume
docker volume create meepleai_postgres_backup

# Copy data
docker run --rm \
  -v meepleai_postgres_data:/source:ro \
  -v meepleai_postgres_backup:/target \
  alpine sh -c "cp -a /source/. /target/"
```

---

## Backup Strategies

### 1. PostgreSQL (Recommended)
```bash
# Manual backup
docker compose exec postgres pg_dump -U postgres meepleai | \
  gzip > backup-$(date +%Y%m%d).sql.gz

# Restore
gunzip -c backup-20260130.sql.gz | \
  docker compose exec -T postgres psql -U postgres meepleai
```

### 2. Volume Tar Backup
```bash
# Backup (for pdf_uploads, qdrant_data)
docker run --rm \
  -v meepleai_pdf_uploads:/source:ro \
  -v /backups:/backup \
  alpine tar czf /backup/pdf-$(date +%Y%m%d).tar.gz -C /source .

# Restore
docker run --rm \
  -v meepleai_pdf_uploads:/target \
  -v /backups:/backup \
  alpine tar xzf /backup/pdf-20260130.tar.gz -C /target
```

### 3. Automated Backup Script
```bash
#!/bin/bash
# backup-all.sh

DATE=$(date +%Y%m%d-%H%M%S)
BACKUP_DIR="/backups/$DATE"
mkdir -p "$BACKUP_DIR"

# PostgreSQL
docker compose exec -T postgres pg_dump -U postgres meepleai | \
  gzip > "$BACKUP_DIR/postgres.sql.gz"

# Qdrant + PDF + Prometheus + Grafana
for vol in qdrant_data pdf_uploads prometheus_data grafana_data; do
  docker run --rm \
    -v meepleai_$vol:/source:ro \
    -v "$BACKUP_DIR":/backup \
    alpine tar czf /backup/$vol.tar.gz -C /source .
done

# Verify
for f in postgres.sql.gz qdrant_data.tar.gz pdf_uploads.tar.gz; do
  [ ! -s "$BACKUP_DIR/$f" ] && echo "❌ $f failed" && exit 1
done

# Cleanup old (>30d)
find /backups -maxdepth 1 -type d -mtime +30 -exec rm -rf {} \;
```

**Cron**: `0 3 * * * /opt/meepleai/scripts/backup-all.sh`

---

## Restore Procedures

### PostgreSQL
```bash
docker compose stop api                           # Prevent writes
gunzip -c backup.sql.gz | \
  docker compose exec -T postgres psql -U postgres meepleai
docker compose start api                          # Resume
docker compose exec postgres psql -U postgres meepleai -c "SELECT COUNT(*) FROM games;"
```

### Volumes
```bash
docker compose stop api                           # Stop service
docker volume rm meepleai_pdf_uploads             # Remove old
docker volume create meepleai_pdf_uploads         # Create empty
docker run --rm \
  -v meepleai_pdf_uploads:/target \
  -v /backups:/backup \
  alpine tar xzf /backup/pdf-20260130.tar.gz -C /target
docker compose start api                          # Restart
```

### Disaster Recovery
```
1. Install MeepleAI → Setup secrets
2. Create volumes → Restore PostgreSQL
3. Restore other volumes (Qdrant, PDF, etc.)
4. Start services → Verify health
```

---

## Best Practices

### ✅ DO
- Always use named volumes in production
- Set project `name:` for consistent prefixes
- Document volume purpose with comments/labels
- Regular backups (hourly DB prod, daily staging)
- Test restores monthly
- Monitor disk usage (`docker system df -v`)

### ❌ DON'T
- Never use anonymous volumes for persistent data
- Never `docker volume prune` on production without verification
- Never `docker compose down -v` without backup
- Never bind mounts for production databases

### Volume Naming Convention
```yaml
name: meepleai  # Project prefix

volumes:
  postgres_data:      # → meepleai_postgres_data
  qdrant_data:        # → meepleai_qdrant_data
  pdf_uploads:        # → meepleai_pdf_uploads
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **Volume not found** | `docker volume create meepleai_*` |
| **Permission denied** | `docker run --rm -v vol:/data alpine chown -R 999:999 /data` |
| **Volume full** | `df -h` → Cleanup or expand disk |
| **Data corruption** | Restore from backup |

### Quick Reference
```bash
# List MeepleAI volumes
docker volume ls --filter label=com.docker.compose.project=meepleai

# Backup volume
docker run --rm -v vol:/source:ro -v /backups:/backup alpine \
  tar czf /backup/vol.tar.gz -C /source .

# Restore volume
docker run --rm -v vol:/target -v /backups:/backup alpine \
  tar xzf /backup/vol.tar.gz -C /target

# Prune dangling
docker volume prune -f
```

---

**See Also**: [Anonymous Volumes Investigation](./anonymous-volumes-investigation.md) | [Disaster Recovery](./runbooks/disaster-recovery.md)

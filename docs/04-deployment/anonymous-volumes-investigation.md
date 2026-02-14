# Anonymous Volumes Investigation

> **Scope**: Detection, prevention, and cleanup of anonymous Docker volumes
> **Last Updated**: 2026-01-30

## What Are Anonymous Volumes

**Definition**: Volumes with 64-char hex hash names instead of human-readable names

```
✅ Named:     meepleai_postgres_data
❌ Anonymous: a7f3d8c9b1e2f48a5c3d1e9f0b4a6c8d2e1f0a9b8c7d6e5f4a3b2c1d0
```

### Comparison Table

| Feature | Named ✅ | Anonymous ❌ |
|---------|---------|-------------|
| **Identification** | Clear purpose | Hash (unknown) |
| **Lifecycle** | Explicit removal | Removed with `down -v` |
| **Sharing** | Multi-container | Single container |
| **Backup** | Easy to automate | Hard to identify |
| **Migration** | Simple | Complex |

---

## How They Are Created

### 1. Dockerfile VOLUME (No Override)
```dockerfile
# Base image
VOLUME /var/lib/postgresql/data  # Creates anonymous if not overridden
```

```yaml
# ❌ BAD: No volumes section
services:
  postgres:
    image: postgres:16
    # Anonymous volume created!

# ✅ GOOD: Override with named
services:
  postgres:
    image: postgres:16
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:
```

### 2. Unnamed Volume Mount
```yaml
# ❌ BAD: No name
volumes:
  - /app/data  # Anonymous!

# ✅ GOOD: Named
volumes:
  - app_data:/app/data

volumes:
  app_data:
```

---

## Problems

| Issue | Impact |
|-------|--------|
| **Hard to identify** | Which service owns hash `a7f3d8c9...`? |
| **Orphaned accumulation** | 47 volumes (should be 10) after rebuilds |
| **Backup nightmare** | Which hash to backup? |
| **Accidental loss** | `docker volume prune` deletes important data |
| **Migration hell** | Must inspect each hash to find correct one |

---

## Detection

### Find Anonymous Volumes
```bash
# List all
docker volume ls

# Filter by hash pattern
docker volume ls --format "{{.Name}}" | grep -E "^[a-f0-9]{64}$"

# Count
docker volume ls -q | grep -E "^[a-f0-9]{64}$" | wc -l

# Find dangling
docker volume ls -q --filter "dangling=true"
```

### Identify Owner
```bash
# Method 1: Inspect labels
docker volume inspect <hash> | jq '.[0].Labels'

# Method 2: Find container
for c in $(docker ps -a -q); do
  docker inspect $c | jq -r '.[] | .Name + " uses: " + (.Mounts[].Name // "none")'
done | grep "<hash>"
```

---

## Prevention

### 1. Always Use Named Volumes
```yaml
name: meepleai  # Consistent prefix

services:
  postgres:
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:  # Explicit declaration
```

### 2. Check Base Image VOLUME
```bash
# Inspect what volumes base image creates
docker inspect postgres:16 | jq '.[0].Config.Volumes'
# → {"/var/lib/postgresql/data": {}}

# Override in docker-compose.yml
```

### 3. Use Labels
```yaml
volumes:
  postgres_data:
    labels:
      com.meepleai.backup: "critical"
      com.meepleai.service: "postgres"
      com.meepleai.purpose: "database-storage"
```

---

## Cleanup

### Safe Cleanup (Recommended)
```bash
# 1. List anonymous
docker volume ls --format "{{.Name}}" | grep -E "^[a-f0-9]{64}$"

# 2. Check dangling
docker volume ls -q --filter "dangling=true"

# 3. Remove dangling only
docker volume prune -f
```

### Automated Cleanup Script
```bash
#!/bin/bash
# cleanup-anonymous-volumes.sh

BEFORE=$(docker volume ls -q | wc -l)
docker volume prune -f
AFTER=$(docker volume ls -q | wc -l)

echo "✅ Removed $((BEFORE - AFTER)) volumes"
```

**Cron**: `0 2 * * 0 /path/to/cleanup-anonymous-volumes.sh` (Weekly Sunday 2AM)

---

## MeepleAI Audit

### Current Status
**Result**: ✅ **ZERO ANONYMOUS VOLUMES**

```bash
# Verify all named
docker volume ls --filter label=com.docker.compose.project=meepleai

# Expected output:
meepleai_postgres_data
meepleai_qdrant_data
meepleai_pdf_uploads
meepleai_prometheus_data
... (all with meepleai_ prefix)
```

### Verification Command
```bash
docker volume ls --filter label=com.docker.compose.project=meepleai \
  --format "{{.Name}}" | while read vol; do
    [[ $vol =~ ^[a-f0-9]{64}$ ]] && echo "❌ ANONYMOUS: $vol" || echo "✅ NAMED: $vol"
  done
```

---

## Best Practices

### ✅ DO
- Always declare named volumes in `docker-compose.yml`
- Set project `name:` for consistent prefixes
- Label volumes with metadata (backup, service, criticality)
- Weekly cleanup of dangling volumes (dev)
- Backup before major operations
- Document volume purpose

### ❌ DON'T
- Never rely on anonymous volumes for data
- Never `docker volume prune` on production without verification
- Never `docker compose down -v` without backup
- Never assume hash is safe to delete

---

## Quick Reference

```bash
# Detect anonymous
docker volume ls -q | grep -E "^[a-f0-9]{64}$"

# Find dangling
docker volume ls -q --filter "dangling=true"

# Safe cleanup
docker volume prune -f

# Identify owner
docker volume inspect <hash> | jq '.[0].Labels'

# List by project
docker volume ls --filter label=com.docker.compose.project=meepleai
```

---

**See Also**: [Volume Management](./docker-volume-management.md) | [Backup Guide](./runbooks/backup-restore.md)

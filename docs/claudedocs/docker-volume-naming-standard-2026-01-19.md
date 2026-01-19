# Docker Volume Naming Standardization

**Date**: 2026-01-19
**Purpose**: Standardize Docker volume naming for easy identification and consistency
**Impact**: All volumes renamed from `infra_*` to `meepleai_*` with underscore convention

---

## Problem Identified

### Before Standardization

**Inconsistent Prefixes:**
```
infra_pgdata              ← Directory-based prefix
infra_qdrantdata          ← Not project-based
infra_mailpit-data        ← Mix of hyphen/no separator
```

**Multiple Naming Conventions:**
- No separator: `pgdata`, `qdrantdata`, `ollamadata`
- Hyphens: `mailpit-data`, `pdf-uploads`, `unstructured-temp`
- Underscores: None (in main compose)
- Template uses underscores: `postgres_data`, `redis_data`

**Problems:**
1. ❌ Not immediately identifiable as MeepleAI volumes
2. ❌ Prefix depends on directory name (`infra`)
3. ❌ Inconsistent separator usage (none vs hyphen)
4. ❌ Different conventions across compose files
5. ❌ Difficult to find volumes: `docker volume ls | grep meepleai` returns nothing

---

## Solution Applied

### 1. Added Project Name

**File:** `infra/docker-compose.yml`

```yaml
# Added at top of file
name: meepleai

services:
  # ... rest of config
```

**Impact:** All volumes now prefixed with `meepleai_` instead of `infra_`

### 2. Standardized to Underscore Convention

**Before → After:**
```
pgdata           → postgres_data
qdrantdata       → qdrant_data
ollamadata       → ollama_data
mailpit-data     → mailpit_data
prometheusdata   → prometheus_data
grafanadata      → grafana_data
alertmanagerdata → alertmanager_data
unstructured-temp → unstructured_temp
smoldocling-temp  → smoldocling_temp
smoldocling-models → smoldocling_models
reranker-models   → reranker_models
pdf-uploads       → pdf_uploads
```

**Final Volume Names** (with project prefix):
```
meepleai_postgres_data       ✅ Clear, project-prefixed
meepleai_qdrant_data         ✅
meepleai_ollama_data         ✅
meepleai_mailpit_data        ✅
meepleai_prometheus_data     ✅
meepleai_grafana_data        ✅
meepleai_alertmanager_data   ✅
meepleai_unstructured_temp   ✅
meepleai_smoldocling_temp    ✅
meepleai_smoldocling_models  ✅
meepleai_reranker_models     ✅
meepleai_pdf_uploads         ✅
```

### 3. Updated All Compose Files

**Files Modified:**
- ✅ `infra/docker-compose.yml` - Main configuration
- ✅ `infra/docker-compose.hyperdx.yml` - Observability stack
- ✅ `infra/docker-compose.test.yml` - Test environment (inherits project name)

**Files NOT Modified** (templates only):
- ⏭️ `docker-compose.resource-limits-template.yml` - Already uses underscore convention
- ⏭️ `docker-compose.traefik*.yml` - No volumes defined

---

## Benefits

### 1. Easy Identification

```bash
# Find all MeepleAI volumes
docker volume ls | findstr meepleai

# Before: Had to search for "infra" (ambiguous)
# After: Search for "meepleai" (specific to project)
```

### 2. Directory Independent

```bash
# Volumes maintain same name regardless of directory
# Before: Moving infra/ to infrastructure/ would create infrastructure_*
# After: Always meepleai_* regardless of directory name
```

### 3. Consistent Convention

```
All volumes use:
- Project prefix: meepleai_
- Separator: underscore (_)
- Format: meepleai_service_purpose

Examples:
- meepleai_postgres_data (database data)
- meepleai_qdrant_data (vector database)
- meepleai_pdf_uploads (application uploads)
```

### 4. Better Organization

```yaml
volumes:
  # Grouped by purpose
  # Clear comments
  # Alphabetical within groups
  postgres_data:
  qdrant_data:
  # ... ML caches
  # ... Temp directories
  # ... Observability
```

---

## Migration Guide

### For Existing Installations (with data)

**IMPORTANT:** Migrating volumes preserves data but requires downtime.

#### Option 1: Automated Migration Script (Recommended)

```powershell
# Stop all services
cd infra
docker compose down

# Run migration script (dry run first)
.\scripts\migrate-volume-names.ps1 -DryRun

# Review output, then run actual migration
.\scripts\migrate-volume-names.ps1

# Start services with new configuration
docker compose --profile dev up -d

# Verify data integrity
docker compose ps
docker logs meepleai-postgres

# (Optional) Remove old volumes after verification
docker volume rm infra_pgdata infra_qdrantdata ...
```

#### Option 2: Manual Migration

For each volume:

```powershell
# Example: Migrate PostgreSQL data
$old = "infra_pgdata"
$new = "meepleai_postgres_data"

# Create new volume
docker volume create $new

# Copy data
docker run --rm `
    -v "${old}:/source:ro" `
    -v "${new}:/destination" `
    alpine sh -c "cd /source && cp -av . /destination"

# Verify
docker run --rm -v "${new}:/data" alpine ls -lah /data
```

#### Option 3: Start Fresh (for development only)

**WARNING:** This deletes all data!

```bash
# Stop services
cd infra && docker compose down -v

# Remove old volumes
docker volume prune -f

# Start with new configuration
docker compose --profile dev up -d
```

### For New Installations

Simply use the new docker-compose.yml - volumes will be created with correct names automatically.

---

## Verification

### Check Volume Names

```powershell
# List all MeepleAI volumes
docker volume ls --format "table {{.Name}}\t{{.Driver}}\t{{.Scope}}" | findstr meepleai

# Expected output:
# meepleai_postgres_data       local    local
# meepleai_qdrant_data         local    local
# meepleai_ollama_data         local    local
# ... (12 volumes total)
```

### Verify Service Mounts

```bash
cd infra
docker compose config | grep -A 2 "volumes:"

# Should show new volume names in service definitions
```

### Check Data Integrity

```powershell
# Start services
docker compose --profile dev up -d

# Check PostgreSQL data
docker exec meepleai-postgres psql -U admin -d meepleai -c "\dt"

# Check Qdrant collections
curl http://localhost:6333/collections

# Check Prometheus metrics
curl http://localhost:9090/api/v1/status/config
```

---

## Breaking Changes

### What Changed

**Volume Names:**
- Old format: `infra_servicename` or `infra_service-name`
- New format: `meepleai_service_purpose`

**Container Names:**
- No change (already used `meepleai-service` format)

**Network Names:**
- No change (already `meepleai`)

### Impact on CI/CD

**GitHub Actions** (`.github/workflows/ci.yml`):
- Uses services in CI, not named volumes
- No impact on CI/CD pipelines

**Local Development:**
- Requires migration script or fresh start
- One-time operation per developer machine

**Production:**
- Plan migration during maintenance window
- Test migration script in staging first
- Have rollback plan ready

---

## Naming Convention Reference

### Standard Format

```
meepleai_{service}_{purpose}

Where:
- meepleai: Project prefix (always)
- service: Service name (postgres, qdrant, ollama, etc.)
- purpose: data, models, temp, logs, etc.
- Separator: underscore (_)
```

### Examples by Category

**Databases:**
```
meepleai_postgres_data     - PostgreSQL database files
meepleai_qdrant_data       - Qdrant vector storage
```

**ML Models:**
```
meepleai_ollama_data       - Ollama model storage
meepleai_smoldocling_models - Hugging Face models
meepleai_reranker_models    - Reranker models
```

**Temporary:**
```
meepleai_unstructured_temp - PDF extraction temp
meepleai_smoldocling_temp  - OCR processing temp
```

**Application:**
```
meepleai_pdf_uploads       - User uploaded PDFs
```

**Observability:**
```
meepleai_prometheus_data   - Prometheus metrics
meepleai_grafana_data      - Grafana dashboards
meepleai_alertmanager_data - Alert configuration
meepleai_hyperdx_data      - HyperDX ClickHouse
meepleai_hyperdx_logs      - HyperDX logs
```

**Development Tools:**
```
meepleai_mailpit_data      - Email testing storage
```

---

## Troubleshooting

### Migration Script Fails

**Error:** "Volume already exists"
```powershell
# Use -Force to overwrite
.\scripts\migrate-volume-names.ps1 -Force
```

**Error:** "Docker not running"
```powershell
# Start Docker Desktop first
# Wait for it to fully start
# Then run script
```

**Error:** "Permission denied copying data"
```powershell
# Run PowerShell as Administrator
# Or use docker with elevated permissions
```

### Services Don't Start After Migration

**Check volume mounts:**
```bash
docker compose config | grep volumes -A 5
```

**Check volume exists:**
```powershell
docker volume ls | findstr meepleai_postgres_data
```

**Check logs:**
```bash
docker compose logs postgres
```

### Data Missing After Migration

**Rollback to old volumes:**
```yaml
# Temporarily edit docker-compose.yml
volumes:
  - infra_pgdata:/var/lib/postgresql/data  # Use old name

# Restart
docker compose down && docker compose up -d
```

**Re-run migration:**
```powershell
# Ensure old volume still exists
docker volume ls | findstr infra_pgdata

# Re-run migration
.\scripts\migrate-volume-names.ps1 -Force
```

---

## Related Changes

**Modified Files:**
- `infra/docker-compose.yml` - Project name + standardized volume names
- `infra/docker-compose.hyperdx.yml` - Updated to use underscore convention
- `infra/docker-compose.test.yml` - Inherits project name (no volume changes)
- `infra/scripts/migrate-volume-names.ps1` - NEW: Migration automation

**Documentation:**
- This file: Volume naming standard and migration guide

---

## Future Guidelines

### When Adding New Services

**Always use:**
```yaml
services:
  my-service:
    volumes:
      - my_service_data:/data/path  # Use underscore, descriptive suffix

volumes:
  my_service_data:  # Define at bottom, same name
```

**Don't use:**
```yaml
volumes:
  - mydata:/data           # Too generic
  - my-service-data:/data  # Hyphen (inconsistent)
  - service_volume:/data   # Volume suffix too generic
```

### Volume Naming Checklist

- [ ] Uses project prefix (automatic with `name: meepleai`)
- [ ] Service name is clear and matches container name
- [ ] Purpose suffix is descriptive (data, models, temp, logs)
- [ ] Uses underscore separator, not hyphen
- [ ] Defined in `volumes:` section at end of compose file
- [ ] Has comment explaining purpose
- [ ] Grouped by category (database, ML, observability, etc.)

---

## References

- Docker Compose name specification: https://docs.docker.com/compose/compose-file/04-version-and-name/
- Volume naming best practices: https://docs.docker.com/storage/volumes/
- Migration script: `infra/scripts/migrate-volume-names.ps1`

---

**Status**: Standardization complete
**Migration Required**: Yes (for existing installations with data)
**Downtime Required**: Yes (brief, during migration)
**Data Safety**: High (migration script preserves data)

---

**Last Updated**: 2026-01-19
**Next Review**: After first production deployment

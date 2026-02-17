# Clean Build Strategies

**Last Updated**: 2026-02-02

Different clean build strategies for Docker services with varying levels of data preservation.

---

## Build Strategy Matrix

| Strategy | Data Preserved | Use Case | Time | Risk |
|----------|----------------|----------|------|------|
| **Restart** | ✅ All | Config changes, minor issues | ~10s | 🟢 None |
| **Rebuild** | ✅ All | Code changes, Dockerfile updates | ~2-5min | 🟢 None |
| **Medium Clean** | ✅ Volumes | Service reset, fresh containers | ~1-2min | 🟡 Low |
| **Full Clean** | ❌ Everything | Complete reset, corruption fix | ~5-10min | 🔴 High |

---

## 1. Restart (Fastest - No Data Loss)

**Preserves**: All data, volumes, images, networks
**Time**: ~10 seconds
**Use When**: Service is stuck, config changes don't require rebuild

### Commands

```bash
cd infra

# Restart all services
docker compose restart

# Restart specific service
docker compose restart api
docker compose restart web

# Restart multiple services
docker compose restart api web postgres
```

### What Happens

1. Stops containers gracefully (SIGTERM → 10s timeout → SIGKILL)
2. Starts containers with existing configuration
3. **No rebuild**, **no volume loss**, **no image pull**

### Example Use Cases

- API not responding but database is fine
- Frontend needs reload after .env change
- Service crash without corruption
- Apply simple environment variable changes

---

## 2. Rebuild (Code Changes - No Data Loss)

**Preserves**: Volumes (data), networks
**Rebuilds**: Images from Dockerfile
**Time**: ~2-5 minutes (depending on cache)
**Use When**: Dockerfile changed, dependencies updated, code changes

### Commands

⚠️ **CRITICAL**: Must specify profile for services with `build:` configuration!

```bash
cd infra

# ❌ WRONG - Shows "No services to build"
docker compose build --no-cache

# ✅ CORRECT - Rebuild with profile
docker compose --profile minimal build --no-cache  # api + web
docker compose --profile ai build --no-cache       # api + web + AI services
docker compose --profile full build --no-cache     # everything

# Rebuild specific service (works without profile)
docker compose build --no-cache api
docker compose up -d api

# Rebuild multiple services
docker compose build --no-cache api web
docker compose up -d api web

# Rebuild and restart all (with profile)
docker compose --profile full build --no-cache
docker compose --profile full up -d --force-recreate
```

### What Happens

1. Rebuilds Docker images from Dockerfile
2. Pulls base images if needed
3. Recreates containers with new images
4. **Preserves volumes** (database data, uploads, etc.)
5. **Preserves networks**

### Example Use Cases

- Updated .NET packages (`dotnet restore` needed)
- Changed npm dependencies (`pnpm install` needed)
- Modified Dockerfile (COPY, RUN, ENV commands)
- Updated Python requirements.txt
- Changed base image version

### Optimization Tips

```bash
# Use BuildKit for faster builds (parallel stages)
DOCKER_BUILDKIT=1 docker compose build

# Rebuild with cache (faster)
docker compose build api  # Uses layer cache

# Rebuild without cache (slow but clean)
docker compose build --no-cache api

# Pull latest base images first
docker compose build --pull api
```

---

## 3. Medium Clean (Fresh Start - Volumes Preserved)

**Preserves**: Named volumes (database data, uploads, etc.)
**Removes**: Containers, networks
**Time**: ~1-2 minutes
**Risk**: 🟡 Low - Data safe, but active sessions lost

### Commands

```bash
cd infra

# Stop and remove containers + networks
docker compose down

# Then start fresh
docker compose --profile minimal up -d
# Or
docker compose --profile full up -d
```

### What Happens

1. **Stops all containers** gracefully
2. **Removes containers** (not volumes)
3. **Removes networks** (recreated on up)
4. **Preserves named volumes**: `postgres_data`, `qdrant_data`, `redis_data`, etc.
5. **Keeps Docker images** (faster restart)

### What Gets Reset

✅ **Preserved**:
- Database data (PostgreSQL)
- Vector data (Qdrant)
- Cache data (Redis) - if persistence enabled
- Uploaded PDFs
- Grafana dashboards
- Prometheus data
- n8n workflows

❌ **Lost**:
- Active API sessions
- Redis cache (if not persisted to disk)
- Container logs (use `docker compose logs > backup.log` first)
- Temporary in-memory data

### Example Use Cases

- Service won't start after config change
- Need clean slate for testing
- Networking issues between services
- Multiple services behaving erratically
- After major docker-compose.yml changes

### Before Medium Clean

```bash
# Backup logs if needed
docker compose logs > logs-backup-$(date +%Y%m%d-%H%M%S).log

# Backup specific service logs
docker compose logs api > api-logs.log
docker compose logs web > web-logs.log

# Check what will be preserved
docker volume ls | grep meepleai
```

### After Medium Clean

```bash
# Verify volumes still exist
docker volume ls | grep meepleai

# Expected volumes:
# meepleai_postgres_data
# meepleai_qdrant_data
# meepleai_redis_data (if persistence enabled)
# meepleai_ollama_data
# meepleai_grafana_data
# meepleai_prometheus_data
# ... etc

# Restart with same data
docker compose --profile full up -d

# Verify data integrity
docker compose exec postgres psql -U postgres -d meepleai -c "SELECT COUNT(*) FROM games;"
curl http://localhost:6333/collections  # Check Qdrant collections
```

---

## 4. Full Clean (Nuclear Option - Total Reset)

⚠️ **WARNING**: This destroys ALL data including databases, uploads, and configurations

**Removes**: Containers, volumes, networks, (optionally) images
**Preserves**: Nothing (except source code and secrets on host)
**Time**: ~5-10 minutes (rebuild + data re-initialization)
**Risk**: 🔴 High - Complete data loss

### Commands

```bash
cd infra

# Full clean (volumes + containers + networks)
docker compose down -v

# Full clean + remove images (even slower next start)
docker compose down -v --rmi all

# Nuclear option (remove everything Docker-related)
docker compose down -v --rmi all --remove-orphans
```

### What Happens

1. **Stops all containers**
2. **Removes all containers**
3. **Removes all volumes** (⚠️ DATA LOSS!)
4. **Removes all networks**
5. **Optionally removes images** (if --rmi flag used)

### What Gets Permanently Deleted

❌ **All Application Data**:
- PostgreSQL database (games, users, sessions, everything)
- Qdrant vector collections
- Redis cache
- All uploaded PDFs
- Grafana dashboards (unless in git)
- Prometheus metrics history
- n8n workflows
- Ollama pulled models
- SmolDocling/Reranker cached models
- All application state

✅ **What Survives** (on host):
- Source code (apps/*)
- Secrets (infra/secrets/*.secret)
- Docker Compose configs
- Documentation
- Git history

### When to Use Full Clean

1. **Database Corruption**:
   ```bash
   # PostgreSQL corruption
   docker compose logs postgres | grep "corruption"
   # Solution: Full clean + restore from backup
   ```

2. **Volume Permission Issues**:
   ```bash
   # Windows/Linux permission conflicts
   docker compose logs api | grep "permission denied"
   # Solution: Full clean to reset volume ownership
   ```

3. **Complete Environment Reset**:
   - Switching between development branches with schema changes
   - Testing fresh installation
   - After major version upgrades
   - Debugging volume-related issues

4. **Disk Space Recovery**:
   ```bash
   # Check disk usage
   docker system df
   # Full clean to reclaim space
   ```

### Before Full Clean - Data Backup

```bash
# 1. Backup PostgreSQL
docker compose exec postgres pg_dump -U postgres meepleai > backup-$(date +%Y%m%d).sql

# 2. Backup Qdrant collections
curl http://localhost:6333/collections | jq > qdrant-collections-backup.json

# 3. Backup PDFs (if needed)
docker compose cp api:/app/pdf_uploads ./pdf_uploads_backup

# 4. Backup Grafana dashboards
curl -u admin:admin http://localhost:3001/api/dashboards/home > grafana-backup.json

# 5. Backup n8n workflows (if using n8n)
docker compose exec n8n n8n export:workflow --all --output=/tmp/workflows.json
docker compose cp n8n:/tmp/workflows.json ./n8n-workflows-backup.json

# 6. Export secrets (already in infra/secrets/*.secret - verify)
ls -la infra/secrets/*.secret
```

### Execute Full Clean

```bash
cd infra

# Stop services first (graceful shutdown)
docker compose down

# POINT OF NO RETURN - Confirm before proceeding
# Double-check backups exist:
ls -la backup-*.sql qdrant-collections-backup.json

# Execute full clean
docker compose down -v

# Optional: Remove images too (slower next start)
docker compose down -v --rmi all

# Verify volumes are gone
docker volume ls | grep meepleai
# Should show nothing or only non-project volumes
```

### After Full Clean - Rebuild

```bash
cd infra

# Regenerate secrets (if needed)
cd secrets
pwsh setup-secrets.ps1 -SaveGenerated
cd ..

# Pull fresh images (optional, saves time)
docker compose pull

# Start services
docker compose --profile full up -d

# Wait for services to be healthy
docker compose ps
# Check STATUS column for "Up (healthy)"

# Restore PostgreSQL backup (if needed)
cat backup-20260202.sql | docker compose exec -T postgres psql -U postgres -d meepleai

# Verify restoration
docker compose exec postgres psql -U postgres -d meepleai -c "SELECT COUNT(*) FROM games;"

# Restore Qdrant (manual via API - refer to Qdrant docs)
# Restore PDFs
docker compose cp ./pdf_uploads_backup api:/app/pdf_uploads

# Run migrations (if schema changed)
cd ../apps/api/src/Api
dotnet ef database update
```

---

## Selective Clean Strategies

### Clean Specific Service

```bash
# Stop and remove only API
docker compose stop api
docker compose rm -f api

# Remove API volume (if it has one)
docker volume rm meepleai_api_data  # Only if API has named volume

# Rebuild and restart
docker compose build --no-cache api
docker compose up -d api
```

### Clean Database Only

⚠️ **Destroys all database data**

```bash
# Stop database
docker compose stop postgres

# Remove PostgreSQL volume
docker volume rm meepleai_postgres_data

# Restart (will initialize fresh database)
docker compose up -d postgres

# Run migrations
cd ../apps/api/src/Api
dotnet ef database update
```

### Clean Vector DB Only

⚠️ **Destroys all vector embeddings**

```bash
# Stop Qdrant
docker compose stop qdrant

# Remove Qdrant volume
docker volume rm meepleai_qdrant_data

# Restart
docker compose up -d qdrant

# Re-index documents (via API or admin UI)
```

### Clean Cache Only

```bash
# Flush Redis (data loss if not persisted)
docker compose exec redis redis-cli FLUSHALL

# Or restart Redis (clears memory cache)
docker compose restart redis

# Or remove Redis volume (if persistence enabled)
docker compose stop redis
docker volume rm meepleai_redis_data
docker compose up -d redis
```

### Clean AI Models Only

```bash
# Remove model caches (will re-download on next start)
docker volume rm meepleai_ollama_data
docker volume rm meepleai_smoldocling_models
docker volume rm meepleai_reranker_models

# Restart services (will download models again)
docker compose up -d ollama smoldocling-service reranker-service
```

---

## Clean Build Workflow Examples

### Example 1: Update API Dependencies

```bash
# 1. Update packages
cd apps/api/src/Api
dotnet add package NewPackage

# 2. Rebuild API image
cd ../../../../infra
docker compose build --no-cache api

# 3. Restart API only
docker compose up -d api

# 4. Verify
docker compose logs -f api
curl http://localhost:8080/health
```

### Example 2: Fix Stuck Service

```bash
# 1. Try restart first
docker compose restart api

# 2. If restart doesn't work, medium clean
docker compose down
docker compose up -d --profile minimal

# 3. If still stuck, check logs
docker compose logs api --tail=100

# 4. Last resort: full clean (after backup!)
docker compose down -v
docker compose up -d --profile minimal
```

### Example 3: Switch Git Branches (with schema changes)

```bash
# 1. Backup current database
docker compose exec postgres pg_dump -U postgres meepleai > backup-current.sql

# 2. Switch branch
git checkout feature/new-schema

# 3. Medium clean (to reset services)
docker compose down

# 4. Rebuild API (new migrations)
docker compose build api

# 5. Start services
docker compose up -d --profile minimal

# 6. Apply new migrations
cd ../apps/api/src/Api
dotnet ef database update

# 7. Verify schema
dotnet ef migrations list
```

### Example 4: Complete Environment Reset (Testing)

```bash
# 1. Backup if needed
docker compose exec postgres pg_dump -U postgres meepleai > backup.sql

# 2. Full clean
cd infra
docker compose down -v

# 3. Fresh start
docker compose --profile minimal up -d

# 4. Wait for health
watch docker compose ps  # Ctrl+C when all healthy

# 5. Seed test data
curl -X POST http://localhost:8080/api/v1/admin/seed-test-data

# 6. Verify
curl http://localhost:8080/api/v1/shared-games?pageSize=5
```

---

## Troubleshooting Clean Builds

### Issue: Volume Won't Delete

```bash
# Error: volume is in use
docker compose down
docker volume rm meepleai_postgres_data
# Error: volume is in use

# Solution: Force remove
docker compose down -v  # Removes all volumes
# Or
docker volume rm -f meepleai_postgres_data  # Force single volume
```

### Issue: Containers Won't Stop

```bash
# Timeout waiting for containers
docker compose down
# Waiting for postgres... (hangs)

# Solution: Force kill
docker compose kill
docker compose down -v
```

### Issue: Network Conflicts

```bash
# Error: network meepleai has active endpoints

# Solution: Force remove network
docker network rm meepleai

# Then recreate
docker compose up -d
```

### Issue: Disk Space After Clean

```bash
# Full clean but disk still full

# Remove dangling images
docker image prune -a

# Remove unused volumes (⚠️ be careful!)
docker volume prune

# Remove everything unused
docker system prune -a --volumes

# Check reclaimed space
docker system df
```

---

## Best Practices

1. **Always Backup Before Full Clean**: `pg_dump` for database, export for Qdrant
2. **Use Medium Clean First**: Try less destructive options before full clean
3. **Document Reasons**: Comment in git why full clean was needed
4. **Verify Volumes**: Check `docker volume ls` after clean to confirm
5. **Test After Clean**: Run health checks and smoke tests
6. **Monitor Logs**: Use `docker compose logs -f` to catch startup issues
7. **Save Logs**: `docker compose logs > backup.log` before clean
8. **Check Disk Space**: Ensure enough space for rebuilds (`df -h`)

---

## Quick Command Reference

| Task | Command | Data Loss |
|------|---------|-----------|
| Restart service | `docker compose restart <service>` | None |
| Rebuild service | `docker compose build --no-cache <service>` | None |
| Medium clean | `docker compose down` | None (volumes preserved) |
| Full clean | `docker compose down -v` | ⚠️ Everything |
| Clean + images | `docker compose down -v --rmi all` | ⚠️ Everything + images |
| Backup DB | `pg_dump -U postgres meepleai > backup.sql` | None (creates backup) |
| Restore DB | `psql -U postgres meepleai < backup.sql` | None (restores data) |

---

## Additional Resources

- **Quick Start**: [quick-start.md](./quick-start.md)
- **Service Endpoints**: [service-endpoints.md](./service-endpoints.md)
- **Common Commands**: [common-commands.md](./common-commands.md)
- **Troubleshooting**: [troubleshooting.md](./troubleshooting.md)

---

**Last Updated**: 2026-02-02
**Maintainer**: MeepleAI Development Team

# Dependency Down Runbook

**Alerts**: `DatabaseDown` (critical), `RedisDown` (critical), `QdrantDown` (critical)
**Dashboard**: [Infrastructure](http://localhost:3001/d/infrastructure)

## Symptom

One or more core dependencies are unreachable:

| Alert | Service | Port | Impact |
|-------|---------|------|--------|
| `DatabaseDown` | PostgreSQL | 5432 | **Total outage** — all reads/writes fail |
| `RedisDown` | Redis | 6379 | **Degraded** — caching, sessions, rate limiting fail |
| `QdrantDown` | Qdrant | 6333 | **RAG outage** — vector search and chat unavailable |

## Impact

- **DatabaseDown**: All API operations fail. This is the most critical dependency.
- **RedisDown**: API functions but slower (no cache), sessions may drop, rate limiting disabled.
- **QdrantDown**: RAG chat and semantic search unavailable. Other API features work normally.

## Diagnosis Steps

### 1. Verify which dependency is down

```bash
# Quick health check all dependencies
echo "=== PostgreSQL ===" && pwsh -c "docker exec meepleai-postgres pg_isready" 2>&1
echo "=== Redis ===" && pwsh -c "docker exec meepleai-redis redis-cli ping" 2>&1
echo "=== Qdrant ===" && curl -s http://localhost:6333/healthz 2>&1
```

### 2. Check container status

```bash
# Container state and uptime
pwsh -c "docker ps -a --filter 'name=meepleai-postgres' --filter 'name=meepleai-redis' --filter 'name=meepleai-qdrant' --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'"
```

### 3. Check container logs

```bash
# PostgreSQL logs
pwsh -c "docker logs meepleai-postgres --tail=50 2>&1"

# Redis logs
pwsh -c "docker logs meepleai-redis --tail=50 2>&1"

# Qdrant logs
pwsh -c "docker logs meepleai-qdrant --tail=50 2>&1"
```

### 4. Check resource usage

```bash
# Container resource consumption
pwsh -c "docker stats --no-stream --format 'table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.MemPerc}}' meepleai-postgres meepleai-redis meepleai-qdrant"
```

### 5. Check disk space (common cause)

```bash
# Host disk space
df -h /var/lib/docker

# Docker disk usage
pwsh -c "docker system df"
```

## Remediation

### PostgreSQL Down

```bash
# 1. Try restart
pwsh -c "docker compose restart postgres"

# 2. If OOM killed — check memory limits
pwsh -c "docker inspect meepleai-postgres --format '{{.HostConfig.Memory}}'"

# 3. If data corruption — check WAL
pwsh -c "docker logs meepleai-postgres 2>&1" | grep -i "wal\|corrupt\|panic"

# 4. If disk full — clean old WAL files
pwsh -c "docker exec meepleai-postgres psql -U meepleai -c 'SELECT pg_database_size(current_database()) / 1024 / 1024 AS size_mb;'"
```

### Redis Down

```bash
# 1. Try restart
pwsh -c "docker compose restart redis"

# 2. If memory exhaustion — check memory usage
pwsh -c "docker exec meepleai-redis redis-cli info memory" | grep used_memory_human

# 3. If persistence issue
pwsh -c "docker exec meepleai-redis redis-cli config get save"
```

### Qdrant Down

```bash
# 1. Try restart
pwsh -c "docker compose restart qdrant"

# 2. Check collection health
curl -s http://localhost:6333/collections | jq '.result.collections[] | {name, status}'

# 3. If index corruption — check storage
pwsh -c "docker exec meepleai-qdrant du -sh /qdrant/storage/"
```

### Root Cause Fix

1. **OOM Kill**: Increase container memory limits in `docker-compose.yml`
2. **Disk full**: Clean old data, expand disk, add log rotation
3. **Network issue**: Check Docker network connectivity between containers
4. **Data corruption**: Restore from backup, investigate corruption cause

## Escalation

- **Single dependency**: Attempt restart. If fails after 2 attempts → escalate
- **Multiple dependencies**: Likely infrastructure issue (disk, network, host) → escalate immediately
- **DatabaseDown sustained >5 min**: Escalate — total service outage

## Prevention

- Monitor disk space with `HostLowDiskSpace` alert (threshold: 10%)
- Set appropriate memory limits in Docker Compose
- Regular backups for PostgreSQL (pg_dump)
- Redis persistence configured (RDB snapshots)
- Qdrant snapshots for vector data recovery

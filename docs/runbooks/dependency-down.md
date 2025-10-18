# Runbook: Dependency Down

**Alerts**: `DatabaseDown`, `RedisDown`, `QdrantDown`
**Severity**: CRITICAL
**Threshold**: Service unreachable for 1-2 minutes
**Expected Response Time**: Immediate (< 5 minutes)

## Overview

MeepleAI depends on three core services:
1. **PostgreSQL** - Primary database (all data)
2. **Redis** - Cache & session storage
3. **Qdrant** - Vector database (RAG/semantic search)

When any goes down, API functionality is severely impacted.

## Symptoms

- Alert firing: `DatabaseDown`, `RedisDown`, or `QdrantDown`
- Error dashboard shows dependency status as RED
- API health check fails: `/health/ready`
- Specific features unavailable depending on service

## Impact by Dependency

### PostgreSQL Down

**Impact**: CRITICAL - Complete API failure
- ❌ All database operations fail
- ❌ Authentication fails (sessions, users)
- ❌ All game/rules data unavailable
- ❌ Chat history unavailable
- ✅ Health check endpoint still works

**User Experience**:
- Cannot login
- Cannot view games
- Cannot ask questions
- All operations return 500 errors

### Redis Down

**Impact**: HIGH - Degraded functionality
- ❌ Caching unavailable (slower responses)
- ❌ Session management degraded
- ❌ Rate limiting unavailable
- ❌ AI response cache unavailable (slower Q&A)
- ✅ Core functionality works (slower)

**User Experience**:
- Slower page loads (no cache)
- May get logged out (session loss)
- Slower AI responses (no cache)
- Core features still work

### Qdrant Down

**Impact**: MEDIUM - Feature-specific failure
- ❌ RAG/semantic search unavailable
- ❌ Q&A functionality fails
- ❌ Document similarity unavailable
- ✅ Non-RAG endpoints work fine

**User Experience**:
- Cannot ask questions about rules
- Cannot search documents
- Game browsing still works
- Authentication still works

## Investigation Steps

### 1. Identify Which Dependency (30 seconds)

**Dashboard**:
```json
http://localhost:3001/d/meepleai-error-monitoring
Scroll to: Dependency Health section
```

Look for RED status:
- PostgreSQL: 🔴
- Redis: 🔴
- Qdrant: 🔴

**Or check Alertmanager**:
```json
http://localhost:9093
```

Look for which alert is firing.

### 2. Verify Service Status (30 seconds)

**Check Docker containers**:
```bash
docker compose ps
```

Look for:
- `postgres` - Should show "Up"
- `redis` - Should show "Up"
- `qdrant` - Should show "Up"

**If "Restarting" or "Exit X"**:
- Service is crashing/down
- Check logs immediately

### 3. Check Service Logs (1 minute)

**PostgreSQL**:
```bash
docker compose logs postgres --tail 100
```

**Redis**:
```bash
docker compose logs redis --tail 100
```

**Qdrant**:
```bash
docker compose logs qdrant --tail 100
```

**Look for**:
- Error messages
- Crash stack traces
- "out of memory" errors
- "disk full" errors
- "permission denied" errors

### 4. Check API Logs (1 minute)

```bash
docker compose logs api --tail 100 | grep -i "postgres\|redis\|qdrant"
```

**Look for**:
- Connection timeout errors
- "Connection refused" errors
- "No route to host" errors

### 5. Test Service Manually (1 minute)

**PostgreSQL**:
```bash
# Test connection
docker compose exec postgres psql -U meeple -d meepleai -c "SELECT 1;"
# Should return: 1

# Check database exists
docker compose exec postgres psql -U meeple -l
```

**Redis**:
```bash
# Test connection
docker compose exec redis redis-cli ping
# Should return: PONG

# Check memory usage
docker compose exec redis redis-cli info memory | grep used_memory_human
```

**Qdrant**:
```bash
# Test HTTP endpoint
curl http://localhost:6333/healthz
# Should return: {"status":"ok"}

# Check collections
curl http://localhost:6333/collections
```

## Common Root Causes & Fixes

### Cause 1: Container Crashed

**Symptoms**:
- `docker compose ps` shows "Exit X" or "Restarting"
- Service logs show crash/panic
- Service keeps restarting in loop

**Fix**:
```bash
# Option A: Restart container
docker compose restart <service>
# Example: docker compose restart postgres

# Wait 30 seconds for health check
docker compose ps

# Option B: If restart loop, check resources
docker stats --no-stream
# Look for memory/CPU limits

# Option C: Full restart
docker compose down
docker compose up -d
```

**Verification**:
```bash
# Service should be "Up"
docker compose ps <service>

# Health check should pass
curl http://localhost:8080/health/ready
```json
**Resolution time**: 1-2 minutes

### Cause 2: Out of Disk Space

**Symptoms**:
- Error in logs: "No space left on device"
- `docker compose ps` shows service exited
- Cannot write to database/logs

**Investigation**:
```bash
# Check disk usage
df -h

# Check Docker volumes
docker system df -v

# Check specific volume
docker volume inspect <volume_name>
```

**Fix**:
```bash
# Option A: Clean up Docker
docker system prune -a --volumes
# WARNING: This deletes unused containers, images, volumes

# Option B: Clean up logs
docker compose logs > /tmp/logs.txt  # Backup
docker compose down
docker compose up -d  # Recreates with empty logs

# Option C: Increase disk space
# Expand VM disk (if Docker Desktop)
# Or add more disk to server
```json
**Prevention**:
- Monitor disk usage (add Prometheus alert)
- Configure log rotation
- Set volume size limits

**Resolution time**: 5-15 minutes

### Cause 3: Out of Memory

**Symptoms**:
- Error in logs: "Out of memory" or "OOMKilled"
- Container exits with code 137
- `docker stats` shows memory usage at limit

**Investigation**:
```bash
# Check memory usage
docker stats --no-stream

# Check container memory limit
docker inspect <container> | grep -i memory

# Check system memory
free -h
```

**Fix**:
```bash
# Option A: Increase container memory limit
# Edit docker-compose.yml:
services:
  postgres:
    deploy:
      resources:
        limits:
          memory: 2G  # Increase from 1G

docker compose down
docker compose up -d

# Option B: Reduce memory usage
# PostgreSQL: Reduce shared_buffers
# Redis: Reduce maxmemory
# Qdrant: Reduce cache size

# Option C: Add more system RAM
# Increase VM memory (if Docker Desktop)
```sql
**Prevention**:
- Monitor memory usage (Grafana dashboard)
- Set appropriate limits based on workload
- Use memory profiling to find leaks

**Resolution time**: 5-15 minutes

### Cause 4: Network Issue

**Symptoms**:
- Error: "Connection refused" or "No route to host"
- Service is up but API can't reach it
- Works when testing from host, fails from container

**Investigation**:
```bash
# Check Docker network
docker network ls
docker network inspect meepleai

# Test connectivity from API container
docker compose exec api ping postgres
docker compose exec api nc -zv redis 6379
docker compose exec api nc -zv qdrant 6333
```

**Fix**:
```bash
# Option A: Restart Docker network
docker compose down
docker compose up -d

# Option B: Recreate network
docker network rm meepleai
docker compose up -d
# docker-compose will recreate network

# Option C: Check firewall/iptables
# Ensure Docker has correct firewall rules
sudo iptables -L -n | grep DOCKER
```json
**Resolution time**: 2-5 minutes

### Cause 5: Data Corruption

**Symptoms**:
- Service won't start
- Error in logs: "Corrupted data" or "Invalid format"
- Database recovery mode
- Redis: "Bad file format"

**Fix - PostgreSQL**:
```bash
# Option A: Try auto-recovery
docker compose restart postgres
# PostgreSQL has built-in recovery

# Option B: Restore from backup
docker compose down
# Restore pg_data volume from backup
docker volume rm meepleai_pgdata
docker volume create meepleai_pgdata
# Copy backup data to volume
docker compose up -d postgres

# Option C: Rebuild database
# DESTRUCTIVE: Lose all data
docker compose down -v  # Deletes volumes
docker compose up -d postgres
# Run migrations
cd apps/api/src/Api
dotnet ef database update
```

**Fix - Redis**:
```bash
# Option A: Delete corrupted dump.rdb
docker compose down
docker volume inspect meepleai_redisdata
# Find mountpoint, delete dump.rdb
docker compose up -d redis

# Option B: Recreate volume
docker compose down
docker volume rm meepleai_redisdata
docker compose up -d redis
# Cache will rebuild automatically
```

**Fix - Qdrant**:
```bash
# Option A: Restart and let Qdrant recover
docker compose restart qdrant

# Option B: Recreate collection
# Log into Qdrant container
docker compose exec qdrant sh
# Delete corrupted collection
curl -X DELETE http://localhost:6333/collections/<collection_name>
# Re-index documents from API
```json
**Resolution time**: 5-30 minutes (depending on data size)

### Cause 6: Port Conflict

**Symptoms**:
- Error: "Port already in use" or "Address already in use"
- Service fails to start
- Another process using same port

**Investigation**:
```bash
# Check what's using the port
# PostgreSQL: 5432
# Redis: 6379
# Qdrant: 6333

# Windows:
netstat -ano | findstr "5432"

# Linux/Mac:
lsof -i :5432
sudo ss -lptn 'sport = :5432'
```

**Fix**:
```bash
# Option A: Kill conflicting process
# Windows:
taskkill /PID <PID> /F

# Linux/Mac:
kill -9 <PID>

# Then restart service
docker compose up -d <service>

# Option B: Change port in docker-compose.yml
services:
  postgres:
    ports:
      - "5433:5432"  # Changed from 5432:5432

# Update connection string in API config
docker compose down
docker compose up -d
```sql
**Resolution time**: 2-5 minutes

## Mitigation Steps

### Immediate (< 2 minutes)

1. **Restart service**:
   ```bash
   docker compose restart <service>
   ```

2. **Silence alert** (if working on it):
   ```bash
   # http://localhost:9093
   # Silence for 30 minutes
   ```

3. **Notify team**:
   ```
   #incidents: "PostgreSQL down, restarting container"
   ```

### Short-term (< 10 minutes)

1. **Identify root cause**:
   - Check logs
   - Check resources (disk, memory)
   - Test connectivity

2. **Apply fix**:
   - Restart service
   - Free up resources
   - Fix configuration

3. **Verify fix**:
   ```bash
   # Service is up
   docker compose ps

   # API health check passes
   curl http://localhost:8080/health/ready

   # Test affected feature
   curl http://localhost:8080/api/v1/games
   ```

### Medium-term (< 1 hour)

1. **Monitor for recurrence**:
   - Watch dashboard for 15-30 minutes
   - Check service doesn't crash again

2. **Root cause analysis**:
   - Why did service crash?
   - Resource limits too low?
   - Data corruption?
   - Bug in service?

3. **Create follow-up**:
   - GitHub issue for permanent fix
   - Update monitoring/alerting
   - Improve service configuration

## Recovery Time Objectives

| Dependency | RTO (Recovery Time Objective) | RPO (Recovery Point Objective) |
|------------|-------------------------------|--------------------------------|
| PostgreSQL | < 5 minutes | Last backup (ideally < 1 hour) |
| Redis | < 2 minutes | Acceptable (cache, rebuild) |
| Qdrant | < 5 minutes | Re-index from Postgres |

**RTO**: How long to restore service
**RPO**: How much data loss is acceptable

## Escalation

### When to Escalate

Escalate if:
- ✅ Service won't restart after 3 attempts
- ✅ Data corruption suspected (PostgreSQL)
- ✅ Issue is infrastructure-related (host down, network down)
- ✅ Recovery requires restoring from backup

### Escalation Contacts

**DevOps/Infrastructure team**:
- #ops Slack channel
- On-call DevOps engineer (check schedule)

**Database expertise**:
- #engineering Slack channel
- Database admin (if PostgreSQL corruption)

## Prevention

### Monitoring

1. **Resource monitoring** (add if missing):
   ```promql
   # Disk usage
   node_filesystem_avail_bytes / node_filesystem_size_bytes

   # Memory usage
   container_memory_usage_bytes / container_spec_memory_limit_bytes

   # Container restarts
   rate(kube_pod_container_status_restarts_total[5m])
   ```

2. **Health check frequency**:
   - Increase from 10s to 5s for critical services
   - Reduce timeout to 3s for faster detection

3. **Alert tuning**:
   - Add "Disk Space Low" alert at 80%
   - Add "Memory High" alert at 80%

### Configuration

1. **Resource limits** (`docker-compose.yml`):
   ```yaml
   services:
     postgres:
       deploy:
         resources:
           limits:
             memory: 2G
             cpus: '2'
           reservations:
             memory: 1G
             cpus: '1'
   ```

2. **Restart policy**:
   ```yaml
   restart: unless-stopped  # Already configured
   ```

3. **Health checks**:
   - Ensure all services have health checks
   - Already configured for all dependencies

### Backup & Disaster Recovery

1. **PostgreSQL backups**:
   ```bash
   # Daily backup script
   docker compose exec postgres pg_dump -U meeple meepleai > backup_$(date +%Y%m%d).sql

   # Or use continuous archiving (WAL archiving)
   ```

2. **Volume backups**:
   ```bash
   # Backup Docker volume
   docker run --rm -v meepleai_pgdata:/data -v $(pwd):/backup \
     alpine tar czf /backup/pgdata_backup.tar.gz /data
   ```

3. **Test restore procedure**:
   - Regularly test restoring from backup
   - Document restore steps
   - Measure actual recovery time

## Testing This Runbook

### Simulate PostgreSQL Down

```bash
# Stop service
docker compose stop postgres

# Wait for alert (should fire within 1-2 minutes)
# Follow runbook steps to investigate

# Verify alert fired
curl http://localhost:9093

# Restart service
docker compose start postgres

# Verify recovery
curl http://localhost:8080/health/ready
```

### Simulate Redis Down

```bash
docker compose stop redis
# Follow investigation steps
docker compose start redis
```

### Simulate Qdrant Down

```bash
docker compose stop qdrant
# Follow investigation steps
docker compose start qdrant
```

## Post-Incident

### Required

1. **Document incident**:
   - Which dependency failed?
   - Root cause?
   - How long was it down?
   - Data loss?

2. **Improve resilience**:
   - Add redundancy if possible
   - Improve health checks
   - Add resource monitoring

3. **Update runbook**:
   - Add new scenario if encountered
   - Improve troubleshooting steps

### Optional (if major outage)

1. **Post-mortem**:
   - Discuss with team
   - Identify systemic issues
   - Create action items

2. **Architecture review**:
   - Is single point of failure acceptable?
   - Should we add Redis cluster?
   - Should we add Postgres replica?

## Changelog

- **2025-10-16**: Initial version

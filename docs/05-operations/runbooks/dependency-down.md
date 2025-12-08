# Runbook: Dependency Down

**Alerts**: `DatabaseDown`, `RedisDown`, `QdrantDown`
**Severity**: CRITICAL
**Threshold**: Service unreachable for 1-2 minutes
**Expected Response Time**: Immediate (< 5 minutes)

## Symptoms

**Observable indicators when this alert fires:**
- Alert: "DatabaseDown", "RedisDown", or "QdrantDown" firing in Alertmanager
- Error dashboard shows dependency status as RED (unhealthy)
- API health check fails: `/health/ready` returns 503 Service Unavailable
- Specific features unavailable depending on which service is down
- Logs showing connection errors to affected dependency

## Impact

**Effect on system and users (by dependency):**

### PostgreSQL Down
- **Impact**: CRITICAL - Complete API failure
- **User Experience**: Cannot login, cannot view games, cannot ask questions, all operations return 500 errors
- **Data Integrity**: All database operations fail, no writes possible
- **Business Impact**: Complete service outage, revenue loss, user churn risk
- **System Health**: API cannot function without database

### Redis Down
- **Impact**: HIGH - Degraded functionality
- **User Experience**: Slower page loads (no cache), may get logged out (session loss), slower AI responses (no cache)
- **Data Integrity**: No caching, all operations hit database (increased load)
- **Business Impact**: Performance degradation, poor user experience, database overload risk
- **System Health**: Core functionality works but slower, rate limiting unavailable

### Qdrant Down
- **Impact**: MEDIUM - Feature-specific failure
- **User Experience**: Cannot ask questions about rules, cannot search documents, game browsing still works, authentication still works
- **Data Integrity**: No vector operations, RAG/semantic search unavailable
- **Business Impact**: Q&A functionality unavailable, core differentiator feature down
- **System Health**: Non-RAG endpoints work fine, database and cache operational

## Investigation Steps

### 1. Identify Which Dependency (30 seconds)

**Dashboard Check**:
```
http://localhost:3001/d/meepleai-error-monitoring
Scroll to: Dependency Health section
```

**Look for RED status**:
- PostgreSQL: 🔴 (critical)
- Redis: 🔴 (high priority)
- Qdrant: 🔴 (medium priority)

**Or check Alertmanager**:
```
http://localhost:9093
```

**Look for which alert is firing**: DatabaseDown, RedisDown, or QdrantDown

**Verification checklist**:
- ✅ Which dependency is down? (Postgres, Redis, Qdrant)
- ✅ Is it completely down or intermittent?
- ✅ When did it go down? (note timestamp)
- ✅ Are other dependencies healthy?

### 2. Verify Service Status (30 seconds)

**Check Docker containers**:
```bash
docker compose ps
```

**Look for**:
- `postgres` - Should show "Up" (healthy)
- `redis` - Should show "Up" (healthy)
- `qdrant` - Should show "Up" (healthy)

**Status interpretation**:
- "Up" - Container running (but may still be unhealthy internally)
- "Restarting" - Container crashing in loop (check logs immediately)
- "Exit X" - Container exited with error code X (critical)
- "Created" - Container not started yet (startup issue)

**If "Restarting" or "Exit X"**:
- Service is crashing or failed to start
- Check logs immediately (step 3)
- Likely resource issue or configuration problem

### 3. Check Service Logs (1 minute)

**PostgreSQL**:
```bash
docker compose logs postgres --tail 100

# Look for:
# - "database system is ready to accept connections" (healthy startup)
# - "FATAL: password authentication failed" (config issue)
# - "out of memory" (resource issue)
# - "could not open file" (permission/disk issue)
```

**Redis**:
```bash
docker compose logs redis --tail 100

# Look for:
# - "Ready to accept connections" (healthy startup)
# - "Can't save in background: fork: Cannot allocate memory" (OOM)
# - "Background saving error" (disk issue)
# - "Fatal error loading the DB" (corruption)
```

**Qdrant**:
```bash
docker compose logs qdrant --tail 100

# Look for:
# - "Qdrant started" (healthy startup)
# - "Collection not found" (data issue)
# - "Out of memory" (resource issue)
# - "Failed to load collection" (corruption)
```

### 4. Check API Logs (1 minute)

**API connection errors**:
```bash
docker compose logs api --tail 100 | grep -i "postgres\|redis\|qdrant"

# Look for:
# - "Connection refused" (service not listening)
# - "Timeout" (service slow or network issue)
# - "No route to host" (network issue)
# - "Connection pool exhausted" (too many connections)
```

**HyperDX logs**:
```
http://localhost:8180
Filter: level:error AND (postgres OR redis OR qdrant) AND @timestamp:[now-10m TO now]
```

### 5. Test Service Manually (1 minute)

**PostgreSQL**:
```bash
# Test connection
docker compose exec postgres psql -U meeple -d meepleai -c "SELECT 1;"
# Should return: 1 (if healthy)

# Check database exists
docker compose exec postgres psql -U meeple -l
# Should list meepleai database

# Check database size
docker compose exec postgres psql -U meeple -d meepleai -c "\l+"
```

**Redis**:
```bash
# Set password from Docker secret (run once per session)
export REDIS_PASS=$(cat infra/secrets/redis-password.txt)

# Test connection
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning ping
# Should return: PONG (if healthy)

# Check memory usage
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning info memory | grep used_memory_human

# Check keyspace (number of keys)
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning info keyspace
```

**Qdrant**:
```bash
# Test HTTP endpoint
curl http://localhost:6333/healthz
# Should return: {"status":"ok"}

# Check collections
curl http://localhost:6333/collections
# Should list available collections

# Check cluster info
curl http://localhost:6333/cluster
```

### 6. Check System Resources (1 minute)

**Docker stats** (check for resource exhaustion):
```bash
docker stats --no-stream postgres redis qdrant
```

**Look for**:
- Memory usage > 90% (OOM risk)
- CPU usage > 80% (performance issue)
- No disk space (df -h)

**System-level checks**:
```bash
# Check disk space (should have >10% free)
df -h

# Check system memory (should have >20% free)
free -h

# Check Docker volumes
docker volume ls
docker system df -v
```

### 7. Check Network Connectivity (1 minute)

**Test connectivity from API container**:
```bash
# Ping PostgreSQL
docker compose exec api ping -c 3 postgres

# Test Redis port
docker compose exec api nc -zv redis 6379

# Test Qdrant port
docker compose exec api nc -zv qdrant 6333
```

**Check Docker network**:
```bash
# List networks
docker network ls

# Inspect meepleai network
docker network inspect meepleai_default
# Verify all services are attached
```

## Common Root Causes & Fixes

### Cause 1: Container Crashed

**Symptoms**:
- `docker compose ps` shows "Exit X" or "Restarting"
- Service logs show crash/panic/fatal error
- Service keeps restarting in loop (restart count increasing)

**Investigation**:
```bash
# Check container exit code
docker compose ps <service>
# Exit 0 = clean shutdown
# Exit 1 = general error
# Exit 137 = OOMKilled (out of memory)
# Exit 139 = SIGSEGV (segmentation fault)

# Check restart count
docker inspect <container-id> | grep RestartCount
```

**Fix**:
```bash
# Option A: Simple restart (if first occurrence)
docker compose restart <service>
# Example: docker compose restart postgres

# Wait 30-60 seconds for health check
docker compose ps <service>

# Option B: If restart loop (check resources first)
docker stats --no-stream <service>
# If memory at limit, increase memory allocation

# Option C: Full restart (nuclear option)
docker compose down
docker compose up -d
# Wait for all services to start (1-2 minutes)
```

**Verification**:
```bash
# Service should be "Up" and healthy
docker compose ps <service>

# Health check should pass
curl http://localhost:8080/health | jq '.checks.<service>'

# Service responds to commands
# (see "Test Service Manually" section above)
```

**Prevention**:
- Configure restart policy: `restart: unless-stopped` (already configured)
- Set resource limits in docker-compose.yml
- Monitor resource usage with Prometheus/Grafana
- Add health checks for early detection

**Resolution time**: 1-2 minutes

### Cause 2: Out of Disk Space

**Symptoms**:
- Error in logs: "No space left on device"
- `docker compose ps` shows service exited or unhealthy
- Cannot write to database/logs/volumes
- df -h shows 100% usage on filesystem

**Investigation**:
```bash
# Check disk usage
df -h
# Look for 100% or >95% usage

# Check Docker volumes size
docker system df -v
# Look for large volumes

# Check specific volume
docker volume inspect meepleai_pgdata
docker volume inspect meepleai_redisdata
docker volume inspect meepleai_qdrantdata

# Find large files
du -sh /var/lib/docker/volumes/* | sort -h
```

**Fix**:
```bash
# Option A: Clean up Docker resources (CAUTION: removes unused data)
docker system prune -a --volumes
# WARNING: This deletes unused containers, images, and volumes
# Back up important data first!

# Option B: Clean up logs
docker compose logs > /tmp/logs_backup.txt  # Backup logs
docker compose down
docker compose up -d  # Recreates containers with empty logs

# Option C: Increase disk space (permanent solution)
# - Expand VM disk (if Docker Desktop)
# - Add more disk to server (if dedicated host)
# - Mount additional volume for Docker data

# Option D: Clean specific volume (if safe to delete data)
docker compose down
docker volume rm meepleai_<service>data
docker compose up -d
# WARNING: This deletes all data in that volume!
```

**Verification**:
```bash
# Disk space available (>10%)
df -h

# Service started successfully
docker compose ps <service>

# Service responds to commands
curl http://localhost:8080/health
```

**Prevention**:
- Monitor disk usage (add Prometheus alert for >80% disk usage)
- Configure log rotation (limit log file sizes)
- Set volume size limits in docker-compose.yml
- Implement automated cleanup scripts (weekly cron job)

**Resolution time**: 5-15 minutes

### Cause 3: Out of Memory (OOMKilled)

**Symptoms**:
- Error in logs: "Out of memory" or "OOMKilled"
- Container exits with code 137 (OOMKilled signal)
- `docker stats` shows memory usage at 100% before crash
- System memory exhausted (free -h shows 0 available)

**Investigation**:
```bash
# Check memory usage
docker stats --no-stream postgres redis qdrant api

# Check container memory limit
docker inspect <container-id> | grep -i memory

# Check system memory
free -h

# Check OOMKill events (Linux)
dmesg | grep -i "killed process"
journalctl -u docker | grep -i oom
```

**Fix**:
```bash
# Option A: Increase container memory limit
# Edit docker-compose.yml or docker-compose.resource-limits.yml:
services:
  postgres:
    deploy:
      resources:
        limits:
          memory: 2G  # Increase from 1G
        reservations:
          memory: 1G

docker compose down
docker compose up -d

# Option B: Reduce memory usage (configuration tuning)
# PostgreSQL: Reduce shared_buffers in postgresql.conf
# Redis: Set maxmemory in redis.conf
# Qdrant: Reduce cache size in config.yaml

# Option C: Add more system RAM (permanent solution)
# - Increase VM memory (if Docker Desktop)
# - Add RAM to server (if dedicated host)

# Option D: Restart service (temporary fix)
docker compose restart <service>
# This frees memory but doesn't fix root cause
```

**Verification**:
```bash
# Memory usage normalized (<80%)
docker stats --no-stream <service>

# Service running and healthy
docker compose ps <service>

# No OOMKill events in last 10 minutes
docker compose logs <service> --tail 100 | grep -i oom
```

**Prevention**:
- Monitor memory usage (Grafana dashboard + Prometheus alerts)
- Set appropriate memory limits based on workload profiling
- Use memory profiling tools to identify leaks
- Load test to understand memory requirements under peak load

**Resolution time**: 5-15 minutes

### Cause 4: Network Issue

**Symptoms**:
- Error: "Connection refused" or "No route to host"
- Service is up (`docker compose ps` shows "Up") but API can't reach it
- Works when testing from host, fails from container
- Network timeouts or DNS resolution failures

**Investigation**:
```bash
# Check Docker network
docker network ls
docker network inspect meepleai_default

# Test connectivity from API container
docker compose exec api ping postgres
docker compose exec api ping redis
docker compose exec api ping qdrant

# Test port connectivity
docker compose exec api nc -zv postgres 5432
docker compose exec api nc -zv redis 6379
docker compose exec api nc -zv qdrant 6333

# Check DNS resolution
docker compose exec api nslookup postgres
```

**Fix**:
```bash
# Option A: Restart Docker network
docker compose down
docker compose up -d
# This recreates network with fresh configuration

# Option B: Recreate network manually
docker network rm meepleai_default
docker compose up -d
# docker-compose will recreate network automatically

# Option C: Check firewall/iptables (Linux)
# Ensure Docker has correct firewall rules
sudo iptables -L -n | grep DOCKER
# If rules missing, restart Docker daemon:
sudo systemctl restart docker

# Option D: Restart Docker daemon (if network completely broken)
# Windows/Mac: Restart Docker Desktop
# Linux: sudo systemctl restart docker
```

**Verification**:
```bash
# Network exists and all services attached
docker network inspect meepleai_default | grep -A 10 Containers

# Connectivity works
docker compose exec api ping -c 3 postgres

# API can reach services
curl http://localhost:8080/health | jq '.checks'
```

**Prevention**:
- Use Docker Compose networking (already configured)
- Avoid manual network configuration
- Monitor network errors in logs
- Test network connectivity in health checks

**Resolution time**: 2-5 minutes

### Cause 5: Data Corruption

**Symptoms**:
- Service won't start or crashes immediately after start
- Error in logs: "Corrupted data", "Invalid format", "Checksum mismatch"
- Database recovery mode (PostgreSQL)
- Redis: "Bad file format reading the append only file"
- Qdrant: "Failed to load collection"

**Fix - PostgreSQL**:
```bash
# Option A: Try auto-recovery (PostgreSQL has built-in recovery)
docker compose restart postgres
# Wait 1-2 minutes for recovery process
docker compose logs postgres --tail 50

# Option B: Restore from backup (if recovery fails)
docker compose down
# 1. Backup corrupted data (for investigation)
docker run --rm -v meepleai_pgdata:/data -v $(pwd):/backup \
  alpine tar czf /backup/corrupted_pgdata.tar.gz /data
# 2. Remove corrupted volume
docker volume rm meepleai_pgdata
# 3. Create new volume
docker volume create meepleai_pgdata
# 4. Restore from backup
# Extract backup data to new volume
# 5. Start PostgreSQL
docker compose up -d postgres

# Option C: Rebuild database (DESTRUCTIVE - last resort)
docker compose down -v  # Deletes ALL volumes
docker compose up -d postgres
# Run migrations to recreate schema
cd apps/api/src/Api
dotnet ef database update
```

**Fix - Redis**:
```bash
# Option A: Delete corrupted dump.rdb (cache can rebuild)
docker compose down
# Find Redis data volume mountpoint
docker volume inspect meepleai_redisdata
# Delete corrupted dump.rdb and appendonly.aof
# (Navigate to mountpoint and delete files)
docker compose up -d redis

# Option B: Recreate volume (DESTRUCTIVE but safe for cache)
docker compose down
docker volume rm meepleai_redisdata
docker compose up -d redis
# Cache will rebuild automatically over time
```

**Fix - Qdrant**:
```bash
# Option A: Let Qdrant attempt recovery
docker compose restart qdrant
docker compose logs qdrant --tail 100

# Option B: Delete corrupted collection and re-index
docker compose exec qdrant sh
# Delete corrupted collection
curl -X DELETE http://localhost:6333/collections/<collection_name>
# Exit container
# Re-index documents from API (requires API endpoint or script)

# Option C: Recreate volume and re-index (DESTRUCTIVE)
docker compose down
docker volume rm meepleai_qdrantdata
docker compose up -d qdrant
# Re-index all documents from PostgreSQL
```

**Verification**:
```bash
# Service started successfully
docker compose ps <service>

# No corruption errors in logs
docker compose logs <service> --tail 50 | grep -i "corrupt\|invalid\|error"

# Service responds correctly
# (see "Test Service Manually" section)
```

**Prevention**:
- Implement regular backups (automated daily/weekly)
- Use volume snapshots (if supported by infrastructure)
- Graceful shutdown (avoid docker kill, use docker stop)
- Monitor disk health (SMART monitoring)

**Resolution time**: 5-30 minutes (depending on data size and backup availability)

### Cause 6: Port Conflict

**Symptoms**:
- Error: "Port already in use" or "Address already in use"
- Service fails to start
- Another process using same port (5432, 6379, 6333)
- `docker compose up` shows bind error

**Investigation**:
```bash
# Check what's using the port
# PostgreSQL: 5432
# Redis: 6379
# Qdrant: 6333

# Windows:
netstat -ano | findstr "5432"
netstat -ano | findstr "6379"
netstat -ano | findstr "6333"

# Linux/Mac:
lsof -i :5432
lsof -i :6379
lsof -i :6333
# Or:
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
# Edit docker-compose.yml:
services:
  postgres:
    ports:
      - "5433:5432"  # Changed from 5432:5432

# Update connection string in API config
# infra/env/api.env.dev:
# ConnectionStrings__Postgres="Host=localhost;Port=5433;..."

docker compose down
docker compose up -d

# Option C: Stop all Docker containers using port
docker compose down
# Kill any remaining processes on port
# Then start services
docker compose up -d
```

**Verification**:
```bash
# Port is now free or bound to correct service
netstat -ano | findstr "5432"
# Or: lsof -i :5432

# Service started successfully
docker compose ps <service>

# API can connect
curl http://localhost:8080/health | jq '.checks.<service>'
```

**Prevention**:
- Use standard ports and document them
- Check for port conflicts before starting services
- Use Docker Compose port mapping consistently
- Avoid running multiple instances of same service

**Resolution time**: 2-5 minutes

## Mitigation Steps

### Immediate (< 2 minutes)

1. **Restart service**:
   ```bash
   docker compose restart <service>
   # Example: docker compose restart postgres
   ```

2. **Silence alert** (if working on it):
   ```bash
   # http://localhost:9093
   # Silence DatabaseDown/RedisDown/QdrantDown for 30 minutes
   # Comment: "Restarting service, investigating root cause"
   ```

3. **Notify team**:
   ```
   #incidents: "🚨 PostgreSQL down - restarting container - investigating"
   ```

### Short-term (< 10 minutes)

1. **Identify root cause**:
   - Check logs (see step 3 above)
   - Check resources (disk, memory - see step 6 above)
   - Test connectivity (see step 7 above)

2. **Apply fix**:
   - Restart service (if simple crash)
   - Free up resources (if disk/memory issue)
   - Fix configuration (if config problem)
   - Restore from backup (if corruption)

3. **Verify fix**:
   ```bash
   # Service is up and healthy
   docker compose ps

   # API health check passes
   curl http://localhost:8080/health/ready | jq '.'

   # Test affected feature
   # PostgreSQL: curl -f http://localhost:8080/api/v1/games
   # Redis: Check fast response times (cached)
   # Qdrant: curl -f http://localhost:8080/api/v1/chat (RAG endpoint)
   ```

### Medium-term (< 1 hour)

1. **Monitor for recurrence**:
   - Watch dashboard Dependency Health section for 15-30 minutes
   - Check service doesn't crash again
   - Monitor resource usage trends

2. **Root cause analysis**:
   - Why did service crash? (OOM, disk full, corruption, bug)
   - Resource limits too low? (increase if needed)
   - Data corruption? (restore from backup, add integrity checks)
   - Bug in service? (upgrade version, report bug)

3. **Create follow-up**:
   - GitHub issue for permanent fix (if workaround was applied)
   - Update monitoring/alerting (add resource usage alerts)
   - Improve service configuration (memory limits, health checks)

## Recovery Time Objectives (RTO)

| Dependency | RTO Target | RPO (Data Loss Acceptable) | Priority |
|------------|-----------|---------------------------|----------|
| PostgreSQL | < 5 minutes | Last backup (< 1 hour ideal) | CRITICAL |
| Redis | < 2 minutes | Acceptable (cache rebuilds) | HIGH |
| Qdrant | < 5 minutes | Re-index from Postgres (< 1 hour) | MEDIUM |

**RTO (Recovery Time Objective)**: How long to restore service functionality
**RPO (Recovery Point Objective)**: How much data loss is acceptable

## Escalation

### When to Escalate

Escalate if:
- ✅ Service won't restart after 3 attempts (underlying systemic issue)
- ✅ Data corruption suspected in PostgreSQL (DBA expertise needed)
- ✅ Issue is infrastructure-related (host down, network down, cloud provider issue)
- ✅ Recovery requires restoring from backup (backup/restore expertise needed)
- ✅ Cannot identify root cause after 10 minutes (need senior help)

### Escalation Contacts

**DevOps/Infrastructure team**:
- #ops Slack channel
- On-call DevOps engineer (check on-call schedule)

**Database expertise** (PostgreSQL corruption/recovery):
- #engineering Slack channel
- Database admin or senior backend engineer

**Emergency contacts**:
- Team Lead: [name] - [phone]
- DevOps Lead: [name] - [phone]
- CTO: [name] - [phone] (critical only)

## Prevention

### Monitoring

1. **Resource monitoring** (add if missing):
   ```promql
   # Disk usage (should have >20% free)
   node_filesystem_avail_bytes / node_filesystem_size_bytes < 0.2

   # Memory usage (should be <80%)
   container_memory_usage_bytes / container_spec_memory_limit_bytes > 0.8

   # Container restarts (should be 0)
   rate(kube_pod_container_status_restarts_total[5m]) > 0
   ```

2. **Health check frequency**:
   - Current: 10s interval (already configured)
   - Consider: 5s interval for critical services (faster detection)
   - Timeout: 3s (already configured)

3. **Alert tuning**:
   - Add "Disk Space Low" alert at 80% usage
   - Add "Memory High" alert at 80% usage
   - Add "Container Restarting" alert (more than 2 restarts in 10 min)

### Configuration

1. **Resource limits** (docker-compose.yml):
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
     redis:
       deploy:
         resources:
           limits:
             memory: 512M
             cpus: '1'
     qdrant:
       deploy:
         resources:
           limits:
             memory: 1G
             cpus: '2'
   ```

2. **Restart policy** (already configured):
   ```yaml
   restart: unless-stopped
   ```

3. **Health checks** (already configured for all dependencies):
   - PostgreSQL: `pg_isready -U meeple`
   - Redis: `redis-cli ping`
   - Qdrant: `curl http://localhost:6333/healthz`

### Backup & Disaster Recovery

1. **PostgreSQL backups** (automated):
   ```bash
   # Daily backup script (add to cron)
   #!/bin/bash
   BACKUP_DIR=/backups/postgres
   DATE=$(date +%Y%m%d_%H%M%S)
   docker compose exec -T postgres pg_dump -U meeple meepleai > $BACKUP_DIR/backup_$DATE.sql
   # Keep last 7 days
   find $BACKUP_DIR -name "backup_*.sql" -mtime +7 -delete
   ```

2. **Volume backups** (weekly):
   ```bash
   # Backup Docker volume
   docker run --rm \
     -v meepleai_pgdata:/data \
     -v $(pwd):/backup \
     alpine tar czf /backup/pgdata_backup_$(date +%Y%m%d).tar.gz /data
   ```

3. **Test restore procedure** (monthly):
   - Regularly test restoring from backup (dry run)
   - Document restore steps in runbook
   - Measure actual recovery time (verify RTO)

## Testing This Runbook

### Simulate PostgreSQL Down

```bash
# Stop service
docker compose stop postgres

# Wait for alert (should fire within 1-2 minutes)
# Follow runbook investigation steps 1-7

# Verify alert fired
curl http://localhost:9093 | jq '.data.alerts[] | select(.labels.alertname=="DatabaseDown")'

# Verify API health check fails
curl http://localhost:8080/health/ready
# Should return 503 Service Unavailable

# Restart service
docker compose start postgres

# Verify recovery
curl http://localhost:8080/health/ready | jq '.checks.postgres'
# Should return healthy status
```

### Simulate Redis Down

```bash
docker compose stop redis
# Follow investigation steps
# Verify cache miss (slower responses)
docker compose start redis
```

### Simulate Qdrant Down

```bash
docker compose stop qdrant
# Follow investigation steps
# Verify RAG endpoint fails
docker compose start qdrant
```

## Post-Incident

### Required

1. **Document incident**:
   - Which dependency failed? (Postgres, Redis, Qdrant)
   - Root cause? (OOM, disk full, corruption, crash)
   - How long was it down? (calculate downtime)
   - Data loss? (if any, quantify)

2. **Improve resilience**:
   - Add redundancy if possible (replicas, clustering)
   - Improve health checks (more frequent, better detection)
   - Add resource monitoring (disk, memory alerts)

3. **Update runbook**:
   - Add new scenario if encountered (new failure mode)
   - Improve troubleshooting steps (add helpful commands)
   - Update resolution times (based on actual incident)

### Optional (if major outage >30 min)

1. **Post-mortem**:
   - Discuss with team (blameless, focus on systems)
   - Identify systemic issues (resource limits, monitoring gaps)
   - Create action items (improvements, preventive measures)

2. **Architecture review**:
   - Is single point of failure acceptable? (consider HA)
   - Should we add Redis cluster? (high availability)
   - Should we add Postgres replica? (read replica, failover)

## Related Runbooks

- [High Error Rate](./high-error-rate.md): Often caused by dependency failures
- [Error Spike](./error-spike.md): May indicate dependency intermittent failures
- [High Memory Usage](./high-memory-usage.md): OOM can cause dependency crashes

## Related Dashboards

- [Error Monitoring](http://localhost:3001/d/meepleai-error-monitoring): Dependency Health section
- [Infrastructure](http://localhost:3001/d/infrastructure): Resource usage metrics
- [API Performance](http://localhost:3001/d/api-performance): Impact of dependency outage on API

## Changelog

- **2025-12-08**: Rewritten for uniform template compliance (Issue #706)
- **2025-10-16**: Initial version
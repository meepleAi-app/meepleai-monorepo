# Infrastructure Monitoring Runbook

**Alerts**: `ContainerHighMemory`, `ContainerHighCPU`, `ContainerRestarted`, `HostHighCPU`, `HostLowDiskSpace`, `HostLowDiskSpaceWarning`, `HostHighMemory`, `HostHighLoadAverage`
**Dashboard**: [Infrastructure Monitoring](http://localhost:3001/d/infrastructure-monitoring)

## Symptom

Infrastructure resource usage exceeds safe thresholds:

| Alert | Threshold | Duration | Severity |
|-------|-----------|----------|----------|
| `ContainerHighMemory` | > 90% of limit | 5 min | Warning |
| `ContainerHighCPU` | > 80% | 5 min | Warning |
| `ContainerRestarted` | Restart detected | 1 min | Warning |
| `HostHighCPU` | > 80% | 5 min | Warning |
| `HostLowDiskSpace` | < 10% free | 5 min | Critical |
| `HostLowDiskSpaceWarning` | < 20% free | 10 min | Warning |
| `HostHighMemory` | > 85% | 5 min | Warning |
| `HostHighLoadAverage` | > 2x CPU cores | 10 min | Warning |

## Impact

- **Container resources**: Individual service degradation or OOM kills
- **Host resources**: All services affected, potential cascading failures
- **Disk space critical**: Database writes fail, logs stop, services crash

## Diagnosis Steps

### 1. Overview — all containers

```bash
# Container resource usage
pwsh -c "docker stats --no-stream --format 'table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.MemPerc}}\t{{.NetIO}}\t{{.BlockIO}}'"
```

### 2. Container-specific diagnosis

```bash
# Identify which container is problematic (from alert labels)
CONTAINER="meepleai-api"  # Replace with alert's container name

# Container details
pwsh -c "docker inspect $CONTAINER --format '{{.State.Status}} | Started: {{.State.StartedAt}} | Restarts: {{.RestartCount}}'"

# Container logs (last 50 lines)
pwsh -c "docker logs $CONTAINER --tail=50 2>&1"

# Container processes
pwsh -c "docker top $CONTAINER"
```

### 3. Host resource diagnosis

```bash
# CPU usage
top -bn1 | head -5

# Memory usage
free -h

# Disk usage
df -h

# Top disk consumers
du -sh /var/lib/docker/* 2>/dev/null | sort -rh | head -10

# Docker disk usage breakdown
pwsh -c "docker system df -v"
```

### 4. Identify resource hogs

```bash
# Top CPU processes
ps aux --sort=-%cpu | head -10

# Top memory processes
ps aux --sort=-%mem | head -10

# Docker volume sizes
pwsh -c "docker volume ls -q" | while read vol; do echo "$vol: $(pwsh -c "docker volume inspect $vol --format '{{.Mountpoint}}'" | xargs du -sh 2>/dev/null)"; done
```

## Remediation

### Container High Memory

```bash
# 1. Restart the container (frees leaked memory)
pwsh -c "docker compose restart <service-name>"

# 2. If recurring — increase memory limit in docker-compose.yml
# deploy.resources.limits.memory: "2g" → "3g"

# 3. If API container — check for memory leaks
pwsh -c "docker logs meepleai-api --tail=200 2>&1" | grep -i "memory\|oom\|gc"
```

### Container High CPU

```bash
# 1. Check what's consuming CPU
pwsh -c "docker top <container-name> aux"

# 2. If embedding/reranker service — model inference is CPU-intensive, this may be expected under load
# 3. If API — check for infinite loops or expensive queries
pwsh -c "docker exec meepleai-postgres psql -U meepleai -c \"SELECT pid, now()-query_start AS duration, query FROM pg_stat_activity WHERE state='active' ORDER BY duration DESC LIMIT 5;\""
```

### Container Restarted

```bash
# 1. Check why it restarted
pwsh -c "docker inspect <container-name> --format '{{.State.ExitCode}} {{.State.Error}}'"

# 2. Check logs before restart
pwsh -c "docker logs <container-name> --tail=100 2>&1" | tail -30

# 3. If OOM killed
pwsh -c "docker inspect <container-name> --format '{{.State.OOMKilled}}'"
# If true → increase memory limit
```

### Host Low Disk Space (Critical)

```bash
# 1. Clean Docker resources (safe)
pwsh -c "docker system prune -f"

# 2. Clean old Docker images
pwsh -c "docker image prune -a -f --filter 'until=168h'"

# 3. Clean old logs
find /var/lib/docker/containers -name "*-json.log" -size +100M -exec truncate -s 10M {} \;

# 4. Check PostgreSQL WAL size
pwsh -c "docker exec meepleai-postgres psql -U meepleai -c \"SELECT pg_size_pretty(pg_wal_lsn_diff(pg_current_wal_lsn(), '0/0')) AS wal_size;\""

# 5. If still critical — identify and remove old data
du -sh /var/lib/docker/volumes/* | sort -rh | head -10
```

### Host High Memory

```bash
# 1. Check for memory leaks in containers
pwsh -c "docker stats --no-stream" | sort -k4 -rh | head -5

# 2. Clear system caches (safe, non-destructive)
sync && echo 3 > /proc/sys/vm/drop_caches

# 3. If sustained — consider adding swap or RAM
```

### Host High Load Average

```bash
# 1. Identify load source
iostat -x 1 3  # Check I/O wait

# 2. If I/O bound — check disk performance
iotop -obn 3

# 3. If CPU bound — check which containers/processes
pwsh -c "docker stats --no-stream" | sort -k3 -rh | head -5
```

## Escalation

- **Container warning**: Monitor, restart if needed, no immediate escalation
- **Container OOM killed repeatedly**: Investigate memory leak, escalate if can't resolve
- **Host disk < 10%**: Immediate — clean up NOW, services will fail
- **Host CPU/memory sustained**: Consider infrastructure scaling

## Prevention

- Set appropriate container resource limits in `docker-compose.yml`
- Docker log rotation configured (max-size, max-file in daemon.json)
- Regular `docker system prune` in maintenance windows
- Monitor disk usage trend — alert at 20% gives time to act before 10% critical
- PostgreSQL vacuum and WAL management configured
- Loki log retention set to 30 days (prevents unbounded growth)

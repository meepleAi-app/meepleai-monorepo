# Runbook: Infrastructure Monitoring (cAdvisor + node-exporter)

**Issue**: [P3-705] Infrastructure monitoring with cAdvisor and node-exporter
**Severity**: Variable (depends on specific alert)
**Services**: cAdvisor (container metrics), node-exporter (host metrics)

---

## Overview

This runbook covers troubleshooting for infrastructure-level issues detected by cAdvisor and node-exporter monitoring.

**Services**:
- **cAdvisor** (http://localhost:8082): Container resource usage, network I/O, disk I/O
- **node-exporter** (http://localhost:9100): Host CPU, memory, disk, network, load average

**Dashboard**: http://localhost:3001/d/infrastructure-monitoring

---

## Common Alerts

### Container Alerts

#### 1. ContainerHighMemory

**Alert**: `ContainerHighMemory`
**Severity**: WARNING
**Threshold**: > 90% of memory limit for 5 minutes

**Symptoms**:
- Container using > 90% of configured memory limit
- Risk of OOM (Out Of Memory) kill
- Possible performance degradation

**Investigation**:
```bash
# Check container memory usage
docker stats --no-stream

# Check container logs for memory issues
docker logs <container-name> --tail 100 | grep -i "memory\|oom"

# Check cAdvisor metrics
curl "http://localhost:8082/metrics" | grep container_memory_usage_bytes
```

**Resolution**:
1. **Identify memory-hungry container**:
   ```bash
   docker stats --no-stream --format "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}"
   ```

2. **Temporary fix - Restart container**:
   ```bash
   docker compose restart <service-name>
   ```

3. **Permanent fix - Increase memory limit**:
   ```yaml
   # docker-compose.yml
   services:
     <service-name>:
       deploy:
         resources:
           limits:
             memory: 2G  # Increase from 1G
   ```

4. **Optimize application memory usage**:
   - Check for memory leaks
   - Optimize database queries (use AsNoTracking)
   - Reduce cache size
   - Review concurrent operations

**Prevention**:
- Set appropriate memory limits based on actual usage
- Monitor trends and plan capacity
- Implement memory leak detection
- Use memory profiling tools (dotnet-dump, Node.js heap snapshots)

---

#### 2. ContainerHighCPU

**Alert**: `ContainerHighCPU`
**Severity**: WARNING
**Threshold**: > 80% CPU usage for 5 minutes

**Symptoms**:
- Container consuming > 80% CPU
- Possible performance impact on other containers
- Slow response times

**Investigation**:
```bash
# Check container CPU usage
docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}"

# Check what's consuming CPU inside container
docker top <container-name>

# Check container logs
docker logs <container-name> --tail 100
```

**Resolution**:
1. **Identify CPU-intensive operations**:
   ```bash
   # API container - check endpoint latency
   curl http://localhost:3001/d/api-performance

   # Check for long-running requests in HyperDX
   http://localhost:8180
   ```

2. **Temporary fix - Scale horizontally** (if using Kubernetes):
   ```bash
   kubectl scale deployment/<service> --replicas=3
   ```

3. **Optimize CPU usage**:
   - Optimize algorithms (O(n²) → O(n log n))
   - Add caching for expensive operations
   - Offload heavy processing to background jobs
   - Use async/await properly

4. **Increase CPU limit** (if justified):
   ```yaml
   # docker-compose.yml
   services:
     <service-name>:
       deploy:
         resources:
           limits:
             cpus: '2.0'  # Increase from 1.0
   ```

**Prevention**:
- Profile CPU-intensive code paths
- Set CPU limits based on actual needs
- Implement rate limiting for expensive operations
- Monitor CPU trends

---

#### 3. ContainerRestarted

**Alert**: `ContainerRestarted`
**Severity**: WARNING
**Threshold**: Container restarted in last 5 minutes

**Symptoms**:
- Container has restarted unexpectedly
- Possible OOM kill, crash, or health check failure
- Brief service interruption

**Investigation**:
```bash
# Check container status
docker compose ps

# Check container restart count
docker inspect <container-name> | jq '.[0].RestartCount'

# Check last exit code
docker inspect <container-name> | jq '.[0].State.ExitCode'

# Check logs around restart time
docker logs <container-name> --since 10m
```

**Exit Codes**:
- `0`: Clean exit (normal)
- `1`: Application error
- `137`: OOM kill (128 + 9 SIGKILL)
- `139`: Segmentation fault (128 + 11 SIGSEGV)
- `143`: SIGTERM (graceful shutdown)

**Resolution**:
1. **OOM Kill (137)**:
   - Increase memory limit
   - Fix memory leak
   - Optimize memory usage

2. **Application Crash (1, 139)**:
   - Check logs for exceptions
   - Fix application bug
   - Add error handling

3. **Health Check Failure**:
   ```bash
   # Test health check endpoint
   curl http://localhost:8080/health

   # Adjust health check settings if needed
   healthcheck:
     interval: 10s
     timeout: 5s
     retries: 5
   ```

**Prevention**:
- Proper resource limits
- Comprehensive error handling
- Health check tuning
- Pre-production load testing

---

### Host Alerts

#### 4. HostHighCPU

**Alert**: `HostHighCPU`
**Severity**: WARNING
**Threshold**: > 80% CPU usage for 5 minutes

**Symptoms**:
- Host CPU usage > 80%
- All containers affected
- System-wide performance degradation

**Investigation**:
```bash
# Check overall CPU usage
top -b -n 1 | head -20

# Check CPU usage by process
ps aux --sort=-%cpu | head -10

# Check which containers are using CPU
docker stats --no-stream

# Check node-exporter metrics
curl "http://localhost:9100/metrics" | grep node_cpu_seconds_total
```

**Resolution**:
1. **Identify CPU hog**:
   ```bash
   # Find top CPU consumers
   docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}" | sort -k2 -rn
   ```

2. **Scale infrastructure** (if using cloud):
   - Vertical scaling: Increase VM/instance CPU cores
   - Horizontal scaling: Add more nodes

3. **Optimize workload**:
   - Reduce container CPU limits if over-allocated
   - Distribute load across multiple hosts
   - Schedule CPU-intensive tasks during off-peak hours

**Prevention**:
- Monitor CPU trends
- Capacity planning (keep < 70% average)
- Auto-scaling policies
- Load balancing

---

#### 5. HostLowDiskSpace

**Alert**: `HostLowDiskSpace` / `HostLowDiskSpaceWarning`
**Severity**: CRITICAL (< 10%) / WARNING (< 20%)
**Threshold**: < 10% free disk space for 5 minutes

**Symptoms**:
- Low disk space on host filesystem
- Risk of service failures (database writes, log writes)
- Potential data loss

**Investigation**:
```bash
# Check disk usage
df -h

# Find large directories
du -h / | sort -rh | head -20

# Check Docker volumes
docker system df

# Check node-exporter metrics
curl "http://localhost:9100/metrics" | grep node_filesystem_avail_bytes
```

**Resolution**:
1. **Clean up Docker resources**:
   ```bash
   # Remove unused containers
   docker container prune -f

   # Remove unused images
   docker image prune -a -f

   # Remove unused volumes (⚠️ CAUTION: data loss)
   docker volume prune -f

   # Remove build cache
   docker builder prune -a -f
   ```

2. **Clean up logs**:
   ```bash
   # Find large log files
   find /var/log -type f -size +100M

   # Rotate logs (if not using logrotate)
   journalctl --vacuum-time=7d
   ```

3. **Clean up old backups**:
   ```bash
   # Remove backups older than 30 days
   find /backups -name "*.sql" -mtime +30 -delete
   ```

4. **Expand disk** (permanent solution):
   - Cloud: Resize volume
   - Bare metal: Add disk, extend LVM

**Prevention**:
- Automated log rotation (logrotate)
- Automated backup cleanup
- Disk usage alerts at 20% (warning) and 10% (critical)
- Regular cleanup cron jobs
- Monitor trends, plan capacity

---

#### 6. HostHighMemory

**Alert**: `HostHighMemory`
**Severity**: WARNING
**Threshold**: > 85% memory usage for 5 minutes

**Symptoms**:
- Host memory usage > 85%
- Risk of swapping (performance degradation)
- Possible OOM kills

**Investigation**:
```bash
# Check memory usage
free -h

# Check memory by process
ps aux --sort=-%mem | head -10

# Check which containers are using memory
docker stats --no-stream

# Check node-exporter metrics
curl "http://localhost:9100/metrics" | grep node_memory
```

**Resolution**:
1. **Identify memory consumers**:
   ```bash
   docker stats --no-stream --format "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}" | sort -k3 -rn
   ```

2. **Free up memory**:
   ```bash
   # Drop caches (safe, temporary)
   sync && echo 3 | sudo tee /proc/sys/vm/drop_caches

   # Restart memory-hungry containers
   docker compose restart <service-name>
   ```

3. **Optimize memory allocation**:
   - Reduce container memory limits if over-allocated
   - Optimize application memory usage
   - Use swap wisely (avoid if possible)

4. **Add more RAM** (permanent solution):
   - Cloud: Resize instance
   - Bare metal: Add RAM

**Prevention**:
- Monitor memory trends
- Capacity planning (keep < 70% average)
- Proper container memory limits
- Memory leak detection

---

#### 7. HostHighLoadAverage

**Alert**: `HostHighLoadAverage`
**Severity**: WARNING
**Threshold**: load15 > 2x CPU cores for 10 minutes

**Symptoms**:
- System load average is high
- Possible resource saturation (CPU, I/O, network)
- Performance degradation

**Investigation**:
```bash
# Check load averages (1m, 5m, 15m)
uptime

# Check CPU cores
nproc

# Check what's causing load
top -b -n 1 | head -20

# Check I/O wait
iostat -x 1 5

# Check node-exporter metrics
curl "http://localhost:9100/metrics" | grep node_load
```

**Load Average Interpretation**:
- **< 1.0 per core**: Normal
- **1.0 - 1.5 per core**: Busy but manageable
- **> 2.0 per core**: Overloaded

Example: 4-core system
- Load 4.0: OK (100% utilized)
- Load 6.0: High (50% overloaded)
- Load 8.0: Critical (100% overloaded)

**Resolution**:
1. **Identify bottleneck**:
   ```bash
   # CPU-bound?
   top -b -n 1 | grep "Cpu(s)" # Check %wa (iowait)

   # I/O-bound?
   iostat -x 1 5 # Check %util column

   # Check disk I/O
   docker stats --no-stream --format "table {{.Name}}\t{{.BlockIO}}"
   ```

2. **CPU-bound**:
   - Optimize CPU-intensive operations
   - Scale horizontally
   - Add more CPU cores

3. **I/O-bound**:
   - Optimize database queries
   - Add indexes
   - Use faster disks (SSD, NVMe)
   - Enable database query caching

**Prevention**:
- Monitor load trends
- Capacity planning
- Performance testing
- Optimize I/O patterns

---

## Prometheus Queries

### Container Metrics (cAdvisor)

```promql
# Container CPU usage (percentage)
rate(container_cpu_usage_seconds_total{name!=""}[5m])

# Container memory usage (bytes)
container_memory_usage_bytes{name!=""}

# Container memory usage (percentage of limit)
(container_memory_usage_bytes / container_spec_memory_limit_bytes) * 100

# Container network receive (bytes/sec)
rate(container_network_receive_bytes_total{name!=""}[5m])

# Container network transmit (bytes/sec)
rate(container_network_transmit_bytes_total{name!=""}[5m])

# Container disk reads (bytes/sec)
rate(container_fs_reads_bytes_total{name!=""}[5m])

# Container disk writes (bytes/sec)
rate(container_fs_writes_bytes_total{name!=""}[5m])

# Container restart count (last hour)
changes(container_start_time_seconds{name!=""}[1h])
```

### Host Metrics (node-exporter)

```promql
# Host CPU usage (percentage)
100 - (avg by(instance) (irate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)

# Host memory used (bytes)
node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes

# Host memory used (percentage)
((node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) / node_memory_MemTotal_bytes) * 100

# Host disk usage (percentage)
((node_filesystem_size_bytes - node_filesystem_avail_bytes) / node_filesystem_size_bytes) * 100

# Host network receive (bytes/sec)
rate(node_network_receive_bytes_total{device!~"lo|veth.*"}[5m])

# Host network transmit (bytes/sec)
rate(node_network_transmit_bytes_total{device!~"lo|veth.*"}[5m])

# Host load averages
node_load1
node_load5
node_load15
```

---

## Troubleshooting cAdvisor

**cAdvisor not starting**:
```bash
# Check logs
docker compose logs cadvisor

# Common issues:
# 1. Privileged mode required
#    - Solution: Add "privileged: true" to docker-compose.yml
# 2. Volume mount issues on Windows
#    - Solution: Use Docker Desktop WSL2 backend
```

**No container metrics**:
```bash
# Verify cAdvisor is scraping
curl http://localhost:8082/metrics | grep container_

# Check Prometheus target
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | select(.job=="cadvisor")'
```

---

## Troubleshooting node-exporter

**node-exporter not starting**:
```bash
# Check logs
docker compose logs node-exporter

# Common issues:
# 1. Volume mount issues
#    - Ensure /:/host:ro,rslave is mounted
# 2. Collector errors
#    - Check --collector.filesystem.mount-points-exclude regex
```

**No host metrics**:
```bash
# Verify node-exporter is working
curl http://localhost:9100/metrics | grep node_

# Check Prometheus target
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | select(.job=="node-exporter")'
```

---

## Related Documentation

- **Docker Compose**: [infra/docker-compose.yml](../../../infra/docker-compose.yml)
- **Prometheus Config**: [infra/prometheus.yml](../../../infra/prometheus.yml)
- **Alert Rules**: [infra/prometheus-rules.yml](../../../infra/prometheus-rules.yml)
- **Dashboard**: [infra/dashboards/infrastructure-monitoring.json](../../../infra/dashboards/infrastructure-monitoring.json)
- **Deployment Guide**: [deployment/board-game-ai-deployment-guide.md](../deployment/board-game-ai-deployment-guide.md)
- **Resource Planning**: [resource-planning.md](../resource-planning.md)

---

## Quick Reference

### Useful Commands

```bash
# Container stats
docker stats --no-stream

# Disk usage
df -h
du -sh /var/lib/docker/*

# Memory info
free -h
cat /proc/meminfo

# CPU info
nproc
lscpu

# Load average
uptime

# Network stats
netstat -i
ss -s

# cAdvisor metrics
curl http://localhost:8082/metrics

# node-exporter metrics
curl http://localhost:9100/metrics

# Prometheus alerts
curl http://localhost:9090/api/v1/alerts
```

**Quick Resource Check**:
```bash
# Memory and CPU usage for all services
docker compose stats --no-stream
```

---

**Last Updated**: 2025-12-08 (Issue #705)
**Maintainer**: DevOps Team
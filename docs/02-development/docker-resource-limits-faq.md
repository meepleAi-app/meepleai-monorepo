# Docker Resource Limits - FAQ & Troubleshooting

**Purpose**: Quick reference for common resource limit issues and solutions

**See Also**: [Docker Compose Resource Limits Guide](./docker-compose-resource-limits.md)

---

## Diagnostics: Quick Checks

### Is my container out of memory?

```bash
# Check current resource usage
docker stats --no-stream postgres api web

# Look for signs:
# 1. Container in status "Up" but restarting frequently
# 2. Memory % near 100% before restart
docker inspect <container> | grep -E "RestartCount|State"

# Check container logs for OOM kill
docker logs <container> | tail -20
```

### Container shows high memory but isn't crashing

```bash
# Docker stats includes page cache (OS caches file I/O)
# True memory pressure only happens at hard limit

# Check actual memory limit
docker inspect postgres | grep -A 5 '"Memory"'

# Memory usage = Working Set + Page Cache
# Only OOM kill triggers at the limit (hard line)
```

### CPU usage is at 100% but service is slow

```bash
# Check if CPU is truly the bottleneck
docker stats api --no-stream

# If CPUPerc near 100% and CPUs in docker-compose is low:
# Solution: Increase CPU limit

# If CPUPerc well below 100% but latency high:
# Issue is I/O bound, not CPU (increase disk speed or connections)
```

---

## Common Issues & Solutions

### PostgreSQL OOM Kill

**Symptom**:
```
postgres exited with code 137
docker logs postgres: Out of memory
Container restarts every few hours
```

**Diagnosis**:
```bash
# Check restart count
docker inspect postgres | grep '"RestartCount"'

# Check configuration
docker exec postgres psql -U meepleai -c \
  "SELECT name, setting FROM pg_settings WHERE name LIKE 'shared%'"
```

**Root Causes**:

1. **`shared_buffers` too large**
   ```bash
   # Check what you set
   docker inspect postgres | grep POSTGRES_INITDB_ARGS

   # If shared_buffers > 30% of container limit:
   # Problem found! Reduce it
   ```

2. **`shm_size` missing or too small**
   ```bash
   # Check /dev/shm allocation
   docker exec postgres df -h /dev/shm

   # If "100%" usage: increase shm_size
   ```

3. **`work_mem` × parallel workers exceeds limit**
   ```bash
   # Each worker uses work_mem for sorting/hashing
   # Total = work_mem × max_parallel_workers_per_gather

   # Check your config
   docker exec postgres psql -U meepleai -c \
    "SELECT name, setting FROM pg_settings \
     WHERE name IN ('work_mem', 'max_parallel_workers_per_gather')"
   ```

**Solutions**:

```yaml
# Option 1: Increase container memory limit
postgres:
  deploy:
    resources:
      limits:
        memory: 2G    # Was 1G, now 2G
      reservations:
        memory: 1G

# Option 2: Decrease shared_buffers
postgres:
  environment:
    POSTGRES_INITDB_ARGS: |
      -c shared_buffers=256M      # Reduce from 512M
      -c effective_cache_size=1G
      -c work_mem=64M             # Also reduce work_mem

# Option 3: Increase shm_size
postgres:
  shm_size: 2G  # Was 1G, now 2G
```

**Prevention**:

```bash
# Before deploying, validate config:
docker inspect postgres | grep -E "Memory|shm"

# Monitor after changes:
docker stats postgres --no-stream
# Memory % should stabilize, not grow steadily
```

---

### Redis Memory Errors

**Symptom**:
```
redis-cli: OOM command not allowed
Errors in logs about "maxmemory"
```

**Root Cause**:
```bash
# Check if maxmemory is set
docker exec redis redis-cli CONFIG GET maxmemory
# If returns "0": Problem is here

# Or redis-cli is configured, but container limit not set
docker stats redis --no-stream
# If Memory % grows to 90%+: No limit enforced
```

**Solution**:

```yaml
redis:
  command:
    - redis-server
    - --maxmemory 512mb          # CRITICAL: Add this
    - --maxmemory-policy allkeys-lru  # Evict LRU keys when full
    - --appendonly yes
```

**Verify**:
```bash
docker restart redis
docker exec redis redis-cli CONFIG GET maxmemory
# Should return: 536870912 (512MB in bytes)

# Test eviction works:
docker exec redis redis-cli INFO memory | grep used_memory
# Fill cache, then check it evicts old keys instead of crashing
```

---

### Qdrant Out of Memory

**Symptom**:
```
Qdrant container OOM kill
Vector search fails with "memory exceeded"
Can't add more vectors
```

**Diagnosis**:
```bash
# Check vector count
curl -s http://localhost:6333/collections/game_rules | jq '.result.points_count'

# Estimate memory needed
# Rule of thumb: 1.2GB per million vectors

# Check current memory usage
docker stats qdrant --no-stream
```

**Solutions**:

**Option 1: Increase memory limit**
```yaml
qdrant:
  deploy:
    resources:
      limits:
        memory: 4G   # Was 2G, now 4G
```

**Option 2: Use on-disk storage (for large collections)**
```yaml
qdrant:
  environment:
    # Add to Qdrant config
    QDRANT_STORAGE_ON_DISK: 'true'
```

**Option 3: Scale horizontally (production)**
```bash
# Deploy Qdrant cluster instead of single instance
# See Qdrant documentation for cluster setup
```

---

### ML Services Memory Issues

**Symptom**:
```
ollama/embedding crashes when loading model
Unstructured OOM when processing PDF
SmolDocling fails on complex documents
```

**Diagnosis**:

```bash
# Check what's loaded
docker logs ollama | grep -i "memory\|vram\|oom"

# Check container memory at time of crash
docker stats ollama --no-stream
# If Memory % near 100% before crash: Limit too low

# Check VRAM usage (if GPU)
docker exec ollama nvidia-smi
```

**Solutions**:

**For Ollama**:
```yaml
ollama:
  environment:
    # Conservative VRAM usage
    OLLAMA_GPU_MEMORY_FRACTION: '0.75'  # Was 0.80
    # Reduce concurrent models
    OLLAMA_MAX_LOADED_MODELS: '2'       # Was 3
    # Faster unload
    OLLAMA_KEEP_ALIVE: '1m'             # Was 5m
  deploy:
    resources:
      limits:
        memory: 12G  # Increase if available
```

**For Embedding/Reranker**:
```yaml
embedding:
  deploy:
    resources:
      limits:
        memory: 3G  # Increase from 2G
        cpus: '2.0'
```

**For SmolDocling**:
```yaml
smoldocling:
  environment:
    MODEL_DTYPE: 'float16'  # Reduce memory by 50%
  deploy:
    resources:
      limits:
        memory: 4G  # Increase for complex PDFs
```

---

### n8n Workflow Timeouts

**Symptom**:
```
Workflows timeout or fail to execute
"Execution data too large" errors
Container restarts during heavy workflows
```

**Diagnosis**:
```bash
# Check Node.js heap setting
docker exec n8n env | grep NODE_OPTIONS

# Monitor memory during execution
docker stats n8n --no-stream
# Watch for steady growth (memory leak)

# Check logs for heap errors
docker logs n8n | grep -i "heap\|memory"
```

**Solutions**:

**Option 1: Increase Node.js heap**
```yaml
n8n:
  environment:
    # For 4G container: (4096 * 0.75) - 256 = 3072
    NODE_OPTIONS: '--max-old-space-size=3072'
  deploy:
    resources:
      limits:
        memory: 4G
```

**Option 2: Increase container memory**
```yaml
n8n:
  deploy:
    resources:
      limits:
        memory: 6G  # Was 2G, increase significantly
```

**Option 3: Optimize workflows**
```bash
# Reduce execution history retention
# (Settings > Execution > Cleanup)

# Use smaller batch sizes in loops
# Avoid loading entire datasets into memory
```

---

### ASP.NET Core API High Memory Usage

**Symptom**:
```
API memory usage steadily grows
GC doesn't release memory
Container nearing limit
```

**Diagnosis**:
```bash
# Monitor memory growth
watch -n 5 'docker stats api --no-stream'

# Check for memory leaks in code
# Look at: /apps/api/src/Api/BoundedContexts/*/Application/Handlers
# Are resources properly disposed?

# Check connection pool settings
docker exec api dotnet-dump analyze /dumps/api.dump
```

**Solutions**:

**Option 1: Increase memory limit**
```yaml
api:
  deploy:
    resources:
      limits:
        memory: 3G  # Was 1.5G
```

**Option 2: Optimize GC behavior**
```yaml
api:
  environment:
    DOTNET_GC_SERVER: '1'         # Server GC (default)
    DOTNET_GC_HEAP_COUNT: '4'     # Match CPU count
    DOTNET_GC_HEAP_AFFINITY_MASK: '0xF'  # Pin to cores
```

**Option 3: Fix code issues**
- Ensure `using` statements on all `IDisposable` objects
- Check `IHttpClientFactory` is used (not creating new HttpClient)
- Review LINQ queries for `.ToList()` on large datasets
- Check caching strategy (infinite cache growing memory)

---

### Docker Desktop Limited Resources

**Symptom**:
```
Cannot start all services
"Failed to allocate memory"
CPU maxed out immediately
```

**Issue**: Docker Desktop default allocation too small

**Solutions**:

**On Windows/Mac**:
1. Open Docker Desktop settings
2. Resources tab
3. Increase Memory (try 8GB+)
4. Increase CPUs (try 4+)
5. Increase Disk (try 50GB+)
6. Apply & Restart

**Configuration File** (alternative):
```json
// Windows: %AppData%\Docker\settings.json
{
  "memoryMiB": 8192,
  "cpus": 4,
  "diskSizeMB": 51200
}
```

**Verification**:
```bash
docker system info | grep -E "Memory|CPU"
```

---

### Multiple Services Competing for Resources

**Symptom**:
```
Services work individually but fail when all running
Timeouts and slowness when under load
Random container crashes
```

**Root Cause**: Over-subscription - sum of limits exceeds host resources

**Diagnosis**:
```bash
# Calculate total resource requirements
# Add up all "limits" from docker-compose.yml

# Example:
# postgres: 1.0 CPU + 1GB memory
# redis:    0.5 CPU + 512MB
# qdrant:   1.5 CPU + 2GB
# Total:    3.0 CPU + 3.5GB

# Check host resources
docker system df
docker stats --all --no-stream | tail -1  # Shows total

# If sum of limits > available resources: Problem found
```

**Solutions**:

**Option 1: Use reservations to prevent oversubscription**
```yaml
# Instead of high limits, use realistic reservations
postgres:
  deploy:
    resources:
      limits:        # Allow peak usage
        cpus: '2.0'
        memory: 4G
      reservations:  # Guarantee minimum (prevents overcommit)
        cpus: '1.0'
        memory: 2G
```

**Option 2: Disable non-critical services in development**
```bash
# Only start needed services
docker-compose -f docker-compose.yml \
  up -d postgres redis qdrant api web

# Don't start: ollama, prometheus, grafana, hyperdx
```

**Option 3: Increase host resources**
- Docker Desktop: See previous section
- VPS/Server: Add more RAM or CPU

---

### Observability Stack Too Heavy

**Symptom**:
```
Prometheus/Grafana/HyperDX consuming too much memory
Don't need full observability in development
```

**Solution**:

**Disable observability stack in development**:
```bash
# Use separate docker-compose files
docker-compose -f docker-compose.yml \
               -f docker-compose.obs-disable.yml \
               up -d
```

**Create `docker-compose.obs-disable.yml`**:
```yaml
# Override observability services to be disabled
services:
  prometheus:
    deploy:
      resources:
        limits:
          cpus: '0'
        reservations:
          cpus: '0'
    environment:
      DISABLE_OBSERVABILITY: 'true'
```

Or just comment out in main docker-compose.yml:
```yaml
# Observability (disabled in development)
# prometheus:
# grafana:
# hyperdx:
```

---

## Monitoring & Prevention

### Regular Checks

**Weekly**:
```bash
# Capture baseline
./scripts/docker-resource-monitor.sh --baseline

# Review for leaks (memory should stabilize, not grow)
```

**Monthly**:
```bash
# Analyze memory trends
docker stats --all --no-stream > stats-month-end.txt

# Compare to baseline - any services growing?
grep "postgres\|api\|qdrant" stats-month-end.txt
```

### Prometheus Alerts

**Set up alerts in Prometheus** (`prometheus.yml`):
```yaml
groups:
  - name: docker_resources
    rules:
      # Memory pressure alert
      - alert: ContainerHighMemory
        expr: container_memory_usage_bytes / container_spec_memory_limit_bytes > 0.85
        for: 5m
        annotations:
          summary: "Container {{ $labels.name }} memory usage high"

      # CPU throttling alert
      - alert: ContainerHighCPU
        expr: rate(container_cpu_usage_seconds_total[1m]) > 0.9
        for: 5m
        annotations:
          summary: "Container {{ $labels.name }} CPU usage high"
```

### Maintenance Script

**Run monthly to clean up and optimize**:
```bash
#!/bin/bash
# cleanup-docker-resources.sh

# Remove unused volumes
docker volume prune -f

# Remove stopped containers
docker container prune -f

# Check for bloated images
docker images | grep -v REPOSITORY

# Rebuild with BuildKit for better caching
DOCKER_BUILDKIT=1 docker-compose build --no-cache
```

---

## Reference: Recommended Limits by Service Type

| Service | Type | Dev Limits | Prod Limits | Notes |
|---------|------|-----------|-------------|-------|
| **PostgreSQL** | I/O-Heavy DB | 1 CPU, 1G mem | 2 CPU, 4G mem | Needs shm_size ≥ 1G |
| **Redis** | In-Memory Cache | 0.5 CPU, 512M | 1 CPU, 2G | Set maxmemory always |
| **Qdrant** | Vector Search | 1.5 CPU, 2G | 4 CPU, 8G | Can swap to disk if needed |
| **Ollama** | LLM Inference | 4 CPU, 8G | 8 CPU, 16G | GPU optional, CPU fallback |
| **Embedding** | Model Server | 2 CPU, 2G | 4 CPU, 4G | Lower latency needs CPU |
| **Reranker** | Model Server | 2 CPU, 2G | 4 CPU, 4G | Lightweight model |
| **SmolDocling** | VLM PDF | 2 CPU, 3G | 4 CPU, 6G | Memory for images |
| **Unstructured** | PDF Extract | 2 CPU, 2G | 4 CPU, 4G | Variable by file size |
| **Prometheus** | Metrics DB | 0.5 CPU, 512M | 1 CPU, 1G | Grows with retention |
| **Grafana** | Dashboard | 0.5 CPU, 512M | 1 CPU, 1G | Lightweight |
| **HyperDX** | Observability | 2 CPU, 2G | 4 CPU, 8G | ClickHouse heavy |
| **n8n** | Workflows | 2 CPU, 2G | 4 CPU, 4G | State in memory |
| **ASP.NET API** | App Server | 2 CPU, 1.5G | 4 CPU, 3G | Efficient, auto-respects limits |
| **Next.js Web** | Frontend | 1 CPU, 1G | 2 CPU, 1.5G | Lightweight |

---

## When to Escalate/Adjust

**Increase Limits When**:
- Container consistently using >75% of limit
- Memory growth prevents normal operation
- Performance needs more resources (add CPU)
- Database needs higher concurrency (add memory)

**Reduce Limits When**:
- Container never uses >20% of limit
- Want to fit more services on host
- Detecting resource waste

**Scale Horizontally When**:
- Single container at maximum limit (can't go higher)
- Need HA/redundancy anyway
- API or web layer hitting CPU ceiling
- Database reaching query concurrency limits

---

## Still Having Issues?

### Gather Diagnostics

```bash
# 1. Check system resources
docker system df
docker stats --all --no-stream

# 2. Check container config
docker inspect postgres | grep -A 10 "Resources"

# 3. Check service logs
docker logs postgres 2>&1 | tail -50

# 4. Check if OOM killed
docker inspect postgres | grep -E "OOMKilled|State"

# 5. Monitoring query
curl http://localhost:9090/api/v1/query?query=container_memory_usage_bytes
```

### Create Issue with Details

When filing a bug report, include:
- Service name and docker-compose excerpt
- `docker stats --no-stream` output
- `docker logs <container>` last 50 lines
- `docker inspect <container> | grep -A 20 Resources`
- Host OS and available memory
- How long service runs before issue appears

---

## References

- [Docker Compose Resource Limits Guide](./docker-compose-resource-limits.md)
- [PostgreSQL Memory Documentation](https://www.postgresql.org/docs/current/runtime-config-resource.html)
- [Qdrant Memory Consumption](https://qdrant.tech/articles/memory-consumption/)
- [n8n Memory Management](https://docs.n8n.io/hosting/scaling/memory-errors/)

---

**Last Updated**: 2025-12-08
**Version**: 1.0

# Docker Compose Resource Limits & Reservations Guide

**Purpose**: Configure CPU and memory limits/reservations for all services in docker-compose.yml to prevent resource contention, ensure predictable performance, and optimize for development vs production environments.

**Last Updated**: 2025-12-08
**Status**: Complete best practices reference

---

## Overview: Limits vs Reservations

### Core Concepts

| Aspect | Limits | Reservations |
|--------|--------|--------------|
| **Definition** | Hard ceiling on resource usage | Guaranteed minimum resources |
| **Behavior** | Container cannot exceed; triggers OOM kill if memory exceeded | Docker ensures resource available; can use more if available |
| **Use Case** | Prevent runaway processes from crashing entire host | Protect critical services from starvation |
| **When Active** | Always enforced | Used when system under memory pressure |

### Strategy: Always Use Both

**Recommended Pattern**: Set BOTH reservations and limits for critical services:

```yaml
deploy:
  resources:
    limits:
      cpus: '1.0'      # Hard ceiling - container cannot exceed
      memory: 2G       # Hard limit - kills if exceeded
    reservations:
      cpus: '0.5'      # Guaranteed minimum - reserved from host
      memory: 1G       # Protected minimum - won't be reclaimed
```

**Why Both?**
- **Reservations** = Guaranteed resources when host is under pressure
- **Limits** = Prevent runaway services from crashing entire host
- **Together** = Flexible resource management (expand if available, protect if not)

---

## Development Environment Configuration

### Philosophy

- **More lenient**: Developers need flexibility to debug and iterate
- **Memory focus**: Prevent OOM kills on developer machines (8-16GB RAM)
- **CPU flexible**: Allow bursts for compilation, testing
- **Reservations lighter**: Lower guardrails for shared resources

### Base Template (Development)

```yaml
version: '3.8'

services:
  # ============================================================================
  # DATABASE & CACHING
  # ============================================================================

  postgres:
    image: postgres:17
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
    shm_size: 1G  # CRITICAL: For parallel queries via work_mem

  redis:
    image: redis:7-alpine
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M

  qdrant:
    image: qdrant/qdrant:latest
    deploy:
      resources:
        limits:
          cpus: '1.5'
          memory: 2G
        reservations:
          cpus: '0.75'
          memory: 1G

  # ============================================================================
  # ML SERVICES (CPU-based inference in dev, GPU optional)
  # ============================================================================

  ollama:
    image: ollama/ollama
    deploy:
      resources:
        limits:
          cpus: '4.0'   # CPU inference is expensive
          memory: 8G
        reservations:
          cpus: '2.0'
          memory: 4G
    environment:
      # Limit VRAM usage if GPU available
      OLLAMA_GPU_MEMORY_FRACTION: '0.8'

  embedding:
    image: meepleai/embedding:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G

  reranker:
    image: meepleai/reranker:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G

  smoldocling:
    image: meepleai/smoldocling:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 3G  # VLM needs more memory
        reservations:
          cpus: '1.0'
          memory: 1.5G

  unstructured:
    image: meepleai/unstructured:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G

  # ============================================================================
  # OBSERVABILITY
  # ============================================================================

  prometheus:
    image: prom/prometheus:latest
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M

  grafana:
    image: grafana/grafana:latest
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M

  hyperdx:
    image: hyperdxio/hyperdx:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G  # Can be memory-intensive with OTLP
        reservations:
          cpus: '1.0'
          memory: 1G

  alertmanager:
    image: prom/alertmanager:latest
    deploy:
      resources:
        limits:
          cpus: '0.25'
          memory: 256M
        reservations:
          cpus: '0.1'
          memory: 128M

  # ============================================================================
  # WORKFLOW & AUTOMATION
  # ============================================================================

  n8n:
    image: n8nio/n8n:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G  # Node.js can be memory-hungry
        reservations:
          cpus: '1.0'
          memory: 1G
    environment:
      # Control Node.js heap size to respect container limits
      NODE_OPTIONS: '--max-old-space-size=1536'  # 1.5GB (75% of 2G limit)

  # ============================================================================
  # APPLICATION SERVICES
  # ============================================================================

  api:
    build:
      context: ./apps/api
      dockerfile: Dockerfile
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 1.5G  # ASP.NET .NET 9 is efficient
        reservations:
          cpus: '1.0'
          memory: 768M
    environment:
      # Help .NET runtime respect container limits (auto in .NET 3.0+)
      # COMPlus_GCHeapCount: '2'  # Optional: pin GC cores

  web:
    build:
      context: ./apps/web
      dockerfile: Dockerfile
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
```

---

## Production Environment Configuration

### Philosophy

- **More restrictive**: Prevent noisy neighbor problems
- **Reservations mandatory**: Guarantee critical services have resources
- **CPU focus**: I/O services get less CPU, compute services get more
- **Memory conservative**: Set meaningful limits to catch leaks early

### Base Template (Production)

```yaml
version: '3.8'

services:
  # ============================================================================
  # DATABASE & CACHING (I/O Intensive)
  # ============================================================================

  postgres:
    image: postgres:17
    deploy:
      resources:
        limits:
          cpus: '2.0'       # More CPU for I/O throughput
          memory: 4G
        reservations:
          cpus: '1.5'       # GUARANTEED: Critical service
          memory: 3G        # GUARANTEED: Production DB
    shm_size: 2G          # LARGER: More parallel query support

  redis:
    image: redis:7-alpine
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 2G
        reservations:
          cpus: '0.75'
          memory: 1.5G
    command: redis-server --maxmemory 1800mb --maxmemory-policy allkeys-lru

  qdrant:
    image: qdrant/qdrant:latest
    deploy:
      resources:
        limits:
          cpus: '4.0'       # Vector search CPU-intensive
          memory: 8G
        reservations:
          cpus: '2.0'       # GUARANTEED for RAG performance
          memory: 4G

  # ============================================================================
  # ML SERVICES (GPU/VRAM Critical in Prod)
  # ============================================================================

  ollama:
    image: ollama/ollama
    deploy:
      resources:
        limits:
          cpus: '8.0'       # CPU fallback if GPU unavailable
          memory: 16G
        reservations:
          cpus: '4.0'
          memory: 8G
    environment:
      OLLAMA_GPU_MEMORY_FRACTION: '0.85'  # Use 85% of VRAM
      OLLAMA_KEEP_ALIVE: '5m'             # Keep models 5min after use
      OLLAMA_MAX_LOADED_MODELS: '3'       # Prevent excessive VRAM swapping

  embedding:
    image: meepleai/embedding:latest
    deploy:
      resources:
        limits:
          cpus: '4.0'
          memory: 4G        # Embedding models need stable memory
        reservations:
          cpus: '2.0'       # GUARANTEED for RAG availability
          memory: 2G

  reranker:
    image: meepleai/reranker:latest
    deploy:
      resources:
        limits:
          cpus: '4.0'
          memory: 4G
        reservations:
          cpus: '2.0'
          memory: 2G

  smoldocling:
    image: meepleai/smoldocling:latest
    deploy:
      resources:
        limits:
          cpus: '4.0'
          memory: 6G        # VLM needs significant memory
        reservations:
          cpus: '2.0'
          memory: 3G

  unstructured:
    image: meepleai/unstructured:latest
    deploy:
      resources:
        limits:
          cpus: '4.0'
          memory: 4G
        reservations:
          cpus: '2.0'
          memory: 2G

  # ============================================================================
  # OBSERVABILITY (Lower priority, but essential for monitoring)
  # ============================================================================

  prometheus:
    image: prom/prometheus:latest
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 1G        # Time-series DB can grow
        reservations:
          cpus: '0.5'
          memory: 512M

  grafana:
    image: grafana/grafana:latest
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M

  hyperdx:
    image: hyperdxio/hyperdx:latest
    deploy:
      resources:
        limits:
          cpus: '4.0'       # HyperDX uses ClickHouse (memory-intensive)
          memory: 4G
        reservations:
          cpus: '2.0'
          memory: 2G

  alertmanager:
    image: prom/alertmanager:latest
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M

  # ============================================================================
  # WORKFLOW & AUTOMATION (Variable workload)
  # ============================================================================

  n8n:
    image: n8nio/n8n:latest
    deploy:
      resources:
        limits:
          cpus: '4.0'       # Workflows can be CPU-intensive
          memory: 4G
        reservations:
          cpus: '2.0'
          memory: 2G
    environment:
      NODE_OPTIONS: '--max-old-space-size=3072'  # 3GB (75% of 4G limit)
      N8N_EXECUTIONS_TIMEOUT: '3600'  # 1 hour timeout
      N8N_EXECUTIONS_MAX_TIMEOUT: '7200'  # 2 hour absolute max

  # ============================================================================
  # APPLICATION SERVICES (Frontend/Backend - Stable workload)
  # ============================================================================

  api:
    image: meepleai/api:latest  # Use pre-built image in prod
    deploy:
      resources:
        limits:
          cpus: '4.0'
          memory: 3G
        reservations:
          cpus: '2.0'
          memory: 1.5G
      replicas: 3                # Run 3 API instances
      update_config:
        parallelism: 1
        delay: 10s
    environment:
      # ASP.NET 9 runtime config
      DOTNET_GC_SERVER: '1'      # Server GC enabled
      DOTNET_GC_HEAP_COUNT: '4'  # Match CPU count hint

  web:
    image: meepleai/web:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 1.5G
        reservations:
          cpus: '1.0'
          memory: 768M
      replicas: 2
```

---

## Service-Specific Recommendations

### PostgreSQL

**Category**: I/O-Intensive Database

**Memory Configuration**:
```yaml
postgres:
  environment:
    POSTGRES_INITDB_ARGS:
      "-c shared_buffers=256M
       -c effective_cache_size=1G
       -c work_mem=256M"
  shm_size: 1G              # Dev/Test
  # or shm_size: 2G        # Production
```

**Calculations** (for 4G memory limit):
- `shared_buffers`: 25% of container memory = 1G (Docker limit)
- `effective_cache_size`: 50-75% of total = 2-3G
- `work_mem`: 256MB per sort/hash operation
- `shm_size`: At least 1GB (for parallel queries)

**CPU**: I/O bound, benefits from 1-2 CPUs (not CPU intensive)

**Monitoring**:
```bash
# Inside container
SELECT name, setting, unit FROM pg_settings
WHERE name IN ('shared_buffers', 'effective_cache_size', 'work_mem');

# From host
docker stats --no-stream postgres
```

---

### Redis

**Category**: In-Memory Cache (I/O-Intensive)

**Memory Limits** (REQUIRED):
```yaml
redis:
  command:
    - redis-server
    - --maxmemory
    - '512mb'           # Dev: Half of container limit
    - --maxmemory-policy
    - 'allkeys-lru'     # Evict least-recently-used when full
```

**Why**: Redis with `maxmemory=0` (unlimited) causes OOM kills after days of operation.

**CPU**: Minimal CPU needs (0.25-0.5 sufficient)

**Development**: 256-512MB cache
**Production**: 1.5-2GB cache

---

### Qdrant (Vector Database)

**Category**: Vector Search (CPU + Memory Balanced)

**Memory Usage**:
- ~1.2GB per million vectors (minimum)
- Can be optimized to ~135MB via on-disk storage
- Use `on_disk: true` for large collections

**CPU**: CPU-intensive for similarity search (needs 1-4 CPUs)

**Configuration**:
```yaml
qdrant:
  environment:
    QDRANT__STORAGE__STORAGE_PATH: /qdrant/storage
    QDRANT__STORAGE__SNAPSHOTS_PATH: /qdrant/snapshots
  # Enable on-disk vectors for massive collections
  # Add to qdrant config file: "on_disk": true
```

**Scaling**:
- Development: 2G memory, 1.5 CPUs
- Production: 8G memory, 4 CPUs (multi-node cluster recommended)

---

### ML Services (Ollama, Embedding, Reranker, SmolDocling)

**Category**: CPU & Memory Intensive (GPU optional)

#### Ollama (LLM Inference)

```yaml
ollama:
  environment:
    # GPU control
    OLLAMA_GPU: '1'                    # Enable GPU (auto-detects)
    OLLAMA_GPU_MEMORY_FRACTION: '0.80' # Use 80% of VRAM
    OLLAMA_NUM_GPU_LAYERS: '-1'        # All layers on GPU if space
    # Model management
    OLLAMA_MAX_LOADED_MODELS: '3'      # Max 3 models in VRAM
    OLLAMA_KEEP_ALIVE: '5m'            # Unload after 5min idle
```

**CPU Limits** (if GPU unavailable):
- Dev: 4 CPUs, 8GB RAM (CPU inference very slow)
- Prod: 8 CPUs, 16GB RAM

**VRAM Recommendations**:
- 8GB VRAM: 7B parameters max
- 16GB VRAM: 13B parameters max
- 24GB VRAM: 70B parameters max

#### Embedding Models

```yaml
embedding:
  # Typical models: 120M-600M parameters
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 2G      # Batch processing needs sustained memory
```

#### Reranker Models

```yaml
reranker:
  # Typical: 120M-900M parameters
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 2G
```

#### SmolDocling (VLM for PDF)

```yaml
smoldocling:
  # Vision Language Model: 256M parameters
  # Needs more memory than pure text models for image processing
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 3G      # Image buffers require extra memory
```

---

### Prometheus & Grafana

**Category**: Observability (I/O + Memory)

**Prometheus** (Time-Series Database):
- Memory grows with number of metrics and retention period
- Default: 500MB-2GB in most deployments
- Set `--storage.tsdb.retention.time=30d` to control growth

```yaml
prometheus:
  command:
    - '--storage.tsdb.path=/prometheus'
    - '--storage.tsdb.retention.time=30d'
    - '--query.timeout=5m'
```

**Grafana** (Dashboard):
- Lightweight, mostly dashboard rendering
- Memory usage: 256-512MB normal
- Grows with dashboard count and data sources

---

### HyperDX (Observability Platform)

**Category**: High-Performance Observability (OTLP ingestion + ClickHouse)

**Resource Requirements**:
- Minimum: 4GB RAM, 2 cores (testing only)
- Recommended: 8GB RAM, 4 cores (production)

**Why Memory-Heavy**:
- Uses ClickHouse (columnar database)
- Buffering incoming OTLP spans and logs
- Real-time aggregations for dashboards

```yaml
hyperdx:
  deploy:
    resources:
      limits:
        cpus: '4.0'
        memory: 8G      # ClickHouse can be greedy
      reservations:
        cpus: '2.0'
        memory: 4G
```

---

### n8n (Workflow Automation)

**Category**: CPU + Memory (Node.js-based)

**Memory Configuration**:
```yaml
n8n:
  environment:
    # CRITICAL: Node.js heap must not exceed container limit
    NODE_OPTIONS: '--max-old-space-size=3072'  # For 4G container
    # Calculation: (container_limit_mb * 0.75) - 256 (buffer)
    # For 4096M: (4096 * 0.75) - 256 = 3072M
```

**Why Large Memory**:
- In-memory execution history
- Workflow state management
- Concurrent execution buffers

**CPU**: Variable (0.5-4 CPUs depending on workflow complexity)

---

### ASP.NET Core API

**Category**: Balanced (Compute + I/O)

**Runtime Configuration**:
```yaml
api:
  environment:
    # .NET runtime respects Docker limits automatically (.NET 3.0+)
    DOTNET_GC_SERVER: '1'           # Server GC for multi-core
    DOTNET_GC_HEAP_COUNT: '2'       # Hint: number of GC threads
    DOTNET_GC_HEAP_AFFINITY_MASK: '0xC'  # Optional: pin to cores
```

**.NET 9 Behavior**:
- Automatically detects Docker memory limits
- GC heap limited to ~75% of container limit
- Respects CPU limits for thread scheduling

**Memory**:
- Dev: 1.5G limit, 768M reservation
- Prod: 3G limit, 1.5G reservation

**CPU**:
- Dev: 2 CPUs
- Prod: 4 CPUs (for high concurrency)

---

### Next.js Frontend

**Category**: Lightweight Application Server

**Memory**:
- Node.js process: 256-512MB typical
- SSR buffering: Add 256MB if using server-side rendering
- Build artifacts cache: Not persisted in container

```yaml
web:
  environment:
    NODE_OPTIONS: '--max-old-space-size=768'  # For 1G container
```

**CPU**: I/O-bound (wait for API), minimal CPU needed (0.5-1.0)

---

## Monitoring & Adjustment

### Real-Time Monitoring

```bash
# Watch resource usage across all services
docker stats --no-stream

# Specific service
docker stats api web postgres --no-stream

# Watch over time
watch -n 2 'docker stats --no-stream'

# Export to analyze peaks
docker stats --all --no-stream > stats_baseline.txt
```

### Prometheus Queries (via Grafana)

```promql
# Container memory usage percentage
(container_memory_usage_bytes{name="postgres"} / 1e9) / 4 * 100

# CPU usage percentage (sum of all cores)
rate(container_cpu_usage_seconds_total{name="api"}[1m]) * 100

# Memory limit
container_spec_memory_limit_bytes{name="postgres"} / 1e9

# Over-allocated warnings
container_memory_usage_bytes / container_spec_memory_limit_bytes > 0.85
```

### Adjustment Process

1. **Establish Baselines** (1-2 weeks in production):
   ```bash
   docker stats api postgres qdrant --no-stream > baseline.txt
   # Capture peak usage during normal operations
   ```

2. **Identify Patterns**:
   - When does each service peak?
   - Memory leaks? (steady growth)
   - CPU spikes? (identify triggers)

3. **Adjust Conservative** (10% buffer above peaks):
   ```yaml
   # If postgres peaks at 3.2GB:
   limits:
     memory: 3.5G    # 10% buffer above peak
   reservations:
     memory: 2.8G    # Still guarantee 70% for other services
   ```

4. **Monitor Alerts**:
   - Container restart count (OOM kills)
   - CPU throttling (if available in Docker stats)
   - Application latency spikes (correlated with resource limits)

---

## Development vs Production Checklist

### Development Environment

- [ ] Allow oversubscription (can allocate more than available RAM)
- [ ] CPU limits lighter (allow bursts for compilation)
- [ ] Memory limits focused on preventing OOM crashes
- [ ] Reservations lighter (don't lock up resources)
- [ ] Single instance of each service
- [ ] HyperDX optional (can disable OTLP for lightweight setup)

### Production Environment

- [ ] Sum of reservations ≤ available host resources (no overcommit)
- [ ] All critical services have BOTH limits and reservations
- [ ] Memory limits conservative (catch leaks early)
- [ ] CPU limits realistic for expected load
- [ ] Multiple replicas for stateless services
- [ ] HyperDX with full observability stack
- [ ] Regular monitoring and alerting

---

## Common Issues & Solutions

### PostgreSQL OOM Kill

**Symptom**: Container restarts with "Out of memory" error

**Causes**:
- `shared_buffers` too large
- `work_mem` × parallel workers exceeds limit
- Missing `shm_size` parameter

**Fix**:
```yaml
postgres:
  shm_size: 1G  # Add if missing
  environment:
    POSTGRES_INITDB_ARGS: "-c shared_buffers=256M -c work_mem=64M"
```

### Redis Memory Errors

**Symptom**: Redis rejects writes with "OOM command not allowed"

**Causes**:
- No `maxmemory` configured
- LRU policy not set
- Cache growing beyond limit

**Fix**:
```yaml
redis:
  command:
    - redis-server
    - --maxmemory 512mb
    - --maxmemory-policy allkeys-lru
```

### ML Service Crashes (Out of VRAM)

**Symptom**: Ollama/Embedding crashes when loading model

**Causes**:
- VRAM limit exceeded
- Too many models loaded simultaneously
- OLLAMA_MAX_LOADED_MODELS too high

**Fix**:
```yaml
ollama:
  environment:
    OLLAMA_GPU_MEMORY_FRACTION: '0.75'  # Conservative: 75%
    OLLAMA_MAX_LOADED_MODELS: '2'       # Reduce model count
    OLLAMA_KEEP_ALIVE: '1m'             # Unload faster
```

### n8n Workflow Timeouts

**Symptom**: Workflows timeout or crash

**Causes**:
- Insufficient memory for workflow state
- NODE_OPTIONS heap too small
- Too many concurrent executions

**Fix**:
```yaml
n8n:
  deploy:
    resources:
      limits:
        memory: 4G  # Increase if possible
  environment:
    NODE_OPTIONS: '--max-old-space-size=3072'
    N8N_EXECUTIONS_TIMEOUT: '3600'  # Increase timeout
```

### Docker Stats Shows High Memory But No OOM

**Symptom**: Container reports 90%+ memory usage but doesn't restart

**Causes**:
- OS page cache inflating memory usage
- Memory includes buffered I/O
- Docker stats counting cached memory

**Solution**:
Monitor actual working set, not total memory usage:
```bash
docker stats --no-stream  # Look at "MEM %" vs "LIMIT"
# True OOM only kills at hard limit
```

---

## Production Deployment Checklist

- [ ] Verify host has sufficient resources for all reservations
- [ ] Set CPU limits appropriate for service type
- [ ] Set memory limits with 10% buffer above peak usage
- [ ] Configure database (PostgreSQL `shared_buffers`, Redis `maxmemory`)
- [ ] Set `shm_size` for PostgreSQL (1G minimum)
- [ ] Configure Node.js `NODE_OPTIONS` for n8n and web
- [ ] Enable monitoring (Prometheus + Grafana)
- [ ] Set up alerts for memory/CPU threshold (85%+)
- [ ] Document actual usage patterns
- [ ] Test failover scenarios (container restarts)
- [ ] Plan upgrade path (more resources if scaling needed)

---

## References

### Docker Official Documentation
- [Compose Deploy Specification](https://docs.docker.com/reference/compose-file/deploy/)
- [Resource Constraints](https://docs.docker.com/engine/containers/resource_constraints/)
- [Using .NET and Docker](https://devblogs.microsoft.com/dotnet/using-net-and-docker-together-dockercon-2019-update/)

### Service-Specific Guides
- [PostgreSQL Docker Memory Settings](https://www.postgresql.org/message-id/CAGbX52Fm=k8hHJKEzo6-mnh7gn91s=Lz_t6B5uF1SotpXH3UeA@mail.postgresql.org)
- [PostgreSQL Tuning](https://wiki.postgresql.org/wiki/Tuning_Your_PostgreSQL_Server)
- [Qdrant Memory Consumption](https://qdrant.tech/articles/memory-consumption/)
- [Ollama GPU Configuration](https://markaicode.com/ollama-gpu-memory-allocation-vram-errors/)
- [n8n Production Deployment](https://docs.n8n.io/hosting/scaling/memory-errors/)

### Best Practices
- [Docker Memory Limits Guide](https://www.geeksforgeeks.org/devops/configure-docker-compose-memory-limits/)
- [CPU-Intensive vs I/O-Intensive Services](https://www.alibabacloud.com/blog/docker-container-resource-management-cpu-ram-and-io-part-3_594579)
- [Resource Management Best Practices](https://dev.to/docker/efficient-strategies-and-best-practices-to-minimize-resource-consumption-of-containers-in-host-systems-2o59)

---

## Next Steps

1. **Test Development Configuration**: Deploy dev docker-compose with resource limits, verify all services start
2. **Establish Production Baselines**: Run production config, monitor for 2 weeks, collect peak usage data
3. **Adjust Based on Actual Usage**: Update limits/reservations based on observed patterns
4. **Implement Monitoring**: Set up alerts for memory/CPU thresholds in Prometheus
5. **Document Changes**: Record any custom configurations in this guide

---

**Document Version**: 1.0
**Last Reviewed**: 2025-12-08
**Next Review**: 2025-03-08 (quarterly)

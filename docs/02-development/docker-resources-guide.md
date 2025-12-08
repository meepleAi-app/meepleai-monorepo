# Docker Resources Guide - MeepleAI

**Version**: 2.0 (Consolidated)
**Last Updated**: 2025-12-08
**Purpose**: Complete guide for Docker resource limits and optimization
**Location**: Consolidated from `docker-compose-resource-limits.md` + `docker-resource-limits-faq.md` + `docker-resource-limits-quick-reference.md`

---

## Table of Contents

1. [Overview](#overview)
2. [Quick Reference](#quick-reference)
3. [Resource Limits Configuration](#resource-limits-configuration)
4. [Service-Specific Limits](#service-specific-limits)
5. [Monitoring & Optimization](#monitoring--optimization)
6. [FAQ](#faq)
7. [Troubleshooting](#troubleshooting)
8. [Related Documentation](#related-documentation)

---

## Overview

Docker resource limits for MeepleAI services with optimized configurations for development, staging, and production environments.

### Why Resource Limits Matter

**Benefits**:
- ✅ Prevent resource exhaustion
- ✅ Fair resource distribution
- ✅ Predictable performance
- ✅ Cost optimization (production)
- ✅ Prevent OOM kills

**Risks Without Limits**:
- ❌ Single service can consume all resources
- ❌ Unpredictable performance
- ❌ System instability
- ❌ Higher cloud costs

### Environment Profiles

| Environment | Resource Strategy | File |
|-------------|-------------------|------|
| **Development** | No limits (flexibility) | `docker-compose.dev.yml` |
| **Staging** | Production-like limits | `compose.staging.yml` |
| **Production** | Strict limits + reservations | `compose.prod.yml` |
| **CI/Test** | Minimal limits (fast) | `compose.test.yml` |

---

## Quick Reference

### Resource Limits by Service (Production)

| Service | CPU Limit | Memory Limit | CPU Reserved | Memory Reserved |
|---------|-----------|--------------|--------------|-----------------|
| **postgres** | 2 | 4GB | 1 | 2GB |
| **qdrant** | 2 | 8GB | 1 | 4GB |
| **redis** | 1 | 2GB | 0.5 | 1GB |
| **api** | 4 | 8GB | 2 | 4GB |
| **web** | 2 | 4GB | 1 | 2GB |
| **ollama** | 4 | 16GB | 2 | 8GB |
| **embedding** | 2 | 4GB | 1 | 2GB |
| **unstructured** | 2 | 4GB | 1 | 2GB |
| **smoldocling** | 2 | 4GB | 1 | 2GB |
| **prometheus** | 1 | 2GB | 0.5 | 1GB |
| **grafana** | 1 | 1GB | 0.5 | 512MB |
| **hyperdx** | 2 | 4GB | 1 | 2GB |
| **n8n** | 1 | 2GB | 0.5 | 1GB |

**Total Production Requirements**:
- **CPU**: 28 cores (limits), 14 cores (reserved)
- **Memory**: 68GB (limits), 34GB (reserved)

### Quick Commands

```bash
# Check current resource usage
docker stats

# Check service-specific usage
docker stats postgres qdrant redis api

# View configured limits
docker compose config | grep -A 5 "deploy:"

# Update limits without restart (if supported)
docker update --memory=4g <container-id>
```

---

## Resource Limits Configuration

### Docker Compose Syntax

```yaml
services:
  postgres:
    deploy:
      resources:
        limits:
          cpus: '2'           # Maximum CPUs (2 cores)
          memory: 4G          # Maximum memory (4GB)
        reservations:
          cpus: '1'           # Guaranteed CPUs (1 core)
          memory: 2G          # Guaranteed memory (2GB)
```

**Limits vs Reservations**:
- **Limits**: Hard cap, service cannot exceed
- **Reservations**: Guaranteed minimum, always available

### Memory Formats

```yaml
memory: 4G     # Gigabytes
memory: 4096M  # Megabytes
memory: 4Gi    # Gibibytes (1024-based)
```

**Recommendation**: Use `G` (decimal) for consistency

### CPU Formats

```yaml
cpus: '2'      # 2 full cores
cpus: '0.5'    # Half a core
cpus: '1.5'    # 1.5 cores
```

---

## Service-Specific Limits

### PostgreSQL

**Production**:
```yaml
postgres:
  deploy:
    resources:
      limits:
        cpus: '2'
        memory: 4G
      reservations:
        cpus: '1'
        memory: 2G
```

**Tuning Parameters**:
```bash
# Inside PostgreSQL container
shared_buffers = 1GB            # 25% of reserved memory
effective_cache_size = 3GB      # 75% of reserved memory
max_connections = 100           # Connection pool size
work_mem = 10MB                 # Per-query working memory
```

**Monitoring**:
```bash
# Check memory usage
docker exec postgres psql -U meepleai -c \
  "SELECT pg_size_pretty(pg_database_size('meepleai'));"

# Check active connections
docker exec postgres psql -U meepleai -c \
  "SELECT count(*) FROM pg_stat_activity;"
```

### Qdrant (Vector Database)

**Production**:
```yaml
qdrant:
  deploy:
    resources:
      limits:
        cpus: '2'
        memory: 8G    # Large for vector caching
      reservations:
        cpus: '1'
        memory: 4G
```

**Why Large Memory**:
- Vector index caching (HNSW)
- In-memory filtering
- Batch processing

**Monitoring**:
```bash
# Check collection size
curl http://localhost:6333/collections/game_rules

# Check memory usage
curl http://localhost:6333/metrics
```

### API (ASP.NET Core)

**Production**:
```yaml
api:
  deploy:
    resources:
      limits:
        cpus: '4'     # Higher for concurrent requests
        memory: 8G
      reservations:
        cpus: '2'
        memory: 4G
```

**Memory Breakdown**:
- .NET Runtime: ~500MB
- HybridCache: ~2GB (configured)
- Request processing: ~4GB
- Headroom: ~1.5GB

**Monitoring**:
```bash
# Check .NET memory
curl http://localhost:8080/metrics | grep dotnet_total_memory

# Check GC collections
curl http://localhost:8080/metrics | grep dotnet_collection_count
```

### AI/ML Services

**Ollama** (Local LLM):
```yaml
ollama:
  deploy:
    resources:
      limits:
        cpus: '4'
        memory: 16G   # Large for model loading
      reservations:
        cpus: '2'
        memory: 8G
```

**Embedding Service** (BGE-M3):
```yaml
embedding:
  deploy:
    resources:
      limits:
        cpus: '2'
        memory: 4G
      reservations:
        cpus: '1'
        memory: 2G
```

**Unstructured** (PDF Stage 1):
```yaml
unstructured:
  deploy:
    resources:
      limits:
        cpus: '2'
        memory: 4G
      reservations:
        cpus: '1'
        memory: 2G
```

**SmolDocling** (PDF Stage 2, VLM):
```yaml
smoldocling:
  deploy:
    resources:
      limits:
        cpus: '2'
        memory: 4G    # 256M parameter model
      reservations:
        cpus: '1'
        memory: 2G
```

### Frontend (Next.js)

**Production**:
```yaml
web:
  deploy:
    resources:
      limits:
        cpus: '2'
        memory: 4G
      reservations:
        cpus: '1'
        memory: 2G
```

**Memory Breakdown**:
- Node.js: ~500MB
- Next.js cache: ~1GB
- SSR rendering: ~2GB
- Headroom: ~500MB

---

## Monitoring & Optimization

### Check Resource Usage

```bash
# Real-time stats
docker stats

# Specific services
docker stats postgres qdrant api

# Format output
{% raw %}
docker stats --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}"
{% endraw %}
```

### cAdvisor Metrics (Issue #705)

**Access**: http://localhost:8082

**Metrics Available**:
```
container_cpu_usage_seconds_total{name="postgres"}
container_memory_usage_bytes{name="qdrant"}
container_network_receive_bytes_total{name="api"}
container_fs_usage_bytes{name="postgres"}
```

### Prometheus Queries

```promql
# CPU usage by container
rate(container_cpu_usage_seconds_total{name="api"}[5m])

# Memory usage percentage
container_memory_usage_bytes{name="postgres"} / container_spec_memory_limit_bytes{name="postgres"} * 100

# Memory usage trend
increase(container_memory_usage_bytes{name="api"}[1h])
```

### Grafana Dashboard

**Infrastructure Dashboard** (`infrastructure.json`):
- CPU usage by service
- Memory usage trends
- Network I/O
- Disk usage

**Access**: http://localhost:3001 → Dashboards → Infrastructure

### Optimization Strategies

#### 1. Right-Sizing Limits

**Process**:
1. Monitor usage for 7 days
2. Set limit at P95 + 20% headroom
3. Set reservation at P50

**Example**:
```
Service: API
P50 memory: 2.1GB
P95 memory: 3.8GB

Reservation: 2GB (P50)
Limit: 4.5GB (P95 * 1.2)
```

#### 2. Memory Optimization

**API (.NET)**:
- Reduce HybridCache size if hit rate >90%
- Tune GC: Server GC for throughput
- Limit connection pools

**Qdrant**:
- Adjust HNSW ef_construct (lower = less memory)
- Use scalar quantization
- Reduce in-memory payload cache

**PostgreSQL**:
- Tune `shared_buffers` and `effective_cache_size`
- Reduce `max_connections` if not needed
- Use connection pooling in API

#### 3. CPU Optimization

**Identify CPU-heavy services**:
```bash
{% raw %}
docker stats --format "table {{.Name}}\t{{.CPUPerc}}" | sort -k2 -rn
{% endraw %}
```

**Optimization**:
- Ollama: Limit concurrent requests
- API: Tune thread pool size
- Unstructured: Reduce parallel workers

---

## FAQ

### Q: Why no limits in development?

**A**: Flexibility for debugging and experimentation. Developers may need more resources temporarily for:
- Large PDF processing
- Model experimentation
- Load testing
- Debugging memory issues

**Use `docker stats` to monitor** and set limits if specific service is problematic.

### Q: Should I set limits for all services?

**A**:
- **Production**: Yes, all services
- **Staging**: Yes, match production
- **Development**: Optional, only if needed
- **CI/Test**: Yes, for consistent performance

### Q: What happens when limit is exceeded?

**Memory**:
- **Linux**: Container killed (OOM)
- **Windows**: Throttled or killed

**CPU**:
- Container throttled (not killed)
- Performance degradation

### Q: How do I know if limits are too low?

**Signs**:
- OOM kills in logs: `docker compose logs | grep OOM`
- High throttling: Check `docker stats` for 100% CPU usage
- Performance degradation: Slow response times
- Errors in application logs

**Fix**: Increase limits incrementally (+20% at a time)

### Q: Can I update limits without restart?

**Yes** (for running containers):
```bash
# Update memory
docker update --memory=4g <container-id>

# Update CPU
docker update --cpus="2" <container-id>
```

**Note**: Requires `docker compose up -d` to persist changes in compose file.

### Q: How much overhead does Docker add?

**Typical Overhead**:
- Memory: ~100-200MB per container
- CPU: <5% for orchestration
- Disk: Minimal (layered filesystem)

**Total for 17 services**: ~2-3GB memory, <1 CPU core

---

## Troubleshooting

### Container Keeps Getting Killed (OOM)

**Diagnosis**:
```bash
# Check for OOM kills
docker compose logs postgres | grep -i "out of memory"
dmesg | grep -i "out of memory"

# Check current memory usage
docker stats postgres --no-stream
```

**Fixes**:

1. **Increase memory limit**:
   ```yaml
   deploy:
     resources:
       limits:
         memory: 6G  # Was 4G
   ```

2. **Optimize service configuration**:
   ```bash
   # PostgreSQL: Reduce shared_buffers
   shared_buffers = 512MB  # Was 1GB
   ```

3. **Check for memory leaks**:
   ```bash
   # Monitor memory over time
   docker stats postgres
   # Should stabilize, not constantly increase
   ```

### Service Performance Degraded

**Diagnosis**:
```bash
# Check if CPU throttled
docker stats | grep 100%

# Check container inspect
docker inspect <container> | grep -A 10 "NanoCpus"
```

**Fixes**:

1. **Increase CPU limit**:
   ```yaml
   deploy:
     resources:
       limits:
         cpus: '4'  # Was 2
   ```

2. **Optimize application**:
   - Reduce concurrent requests
   - Add caching
   - Optimize queries

3. **Scale horizontally** (if possible):
   ```yaml
   deploy:
     replicas: 2  # Run 2 instances
   ```

### High Memory Swap Usage

**Diagnosis**:
```bash
{% raw %}
docker stats --format "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}"
{% endraw %}
```

**Fixes**:

1. **Disable swap for containers**:
   ```yaml
   services:
     postgres:
       mem_swappiness: 0  # Disable swap
   ```

2. **Increase memory limit** (if usage consistently high)

3. **Optimize application** (reduce memory footprint)

### "No Space Left on Device"

**Diagnosis**:
```bash
# Check Docker disk usage
docker system df -v

# Check host disk
df -h
```

**Fixes**:

1. **Clean up Docker**:
   ```bash
   docker system prune -a --volumes  # ⚠️ Removes all unused
   docker image prune -a             # Images only
   docker volume prune               # Volumes only
   ```

2. **Increase disk space** (host)

3. **Set up log rotation**:
   ```yaml
   logging:
     driver: "json-file"
     options:
       max-size: "10m"
       max-file: "3"
   ```

### Resource Limits Not Applied

**Diagnosis**:
```bash
# Check if limits configured
docker inspect <container> | grep -A 20 "HostConfig"

# Should see:
# "Memory": 4294967296,  (4GB)
# "NanoCpus": 2000000000, (2 CPUs)
```

**Common Causes**:

1. **Using old Docker Compose syntax**:
   ```yaml
   # ❌ Old (deprecated)
   mem_limit: 4g
   cpus: 2

   # ✅ New (correct)
   deploy:
     resources:
       limits:
         cpus: '2'
         memory: 4G
   ```

2. **Missing `deploy` section**

3. **Limits only in override file** (not applied):
   ```bash
   # ❌ Wrong
   docker compose -f docker-compose.yml up  # Doesn't load override

   # ✅ Correct
   docker compose up  # Auto-loads docker-compose.override.yml
   ```

---

## Best Practices

### 1. Set Both Limits and Reservations

**Why**:
- Limits prevent resource exhaustion
- Reservations guarantee minimum resources
- Better resource scheduling

**Example**:
```yaml
deploy:
  resources:
    limits:
      cpus: '2'
      memory: 4G
    reservations:
      cpus: '1'      # 50% of limit
      memory: 2G     # 50% of limit
```

**Rule of Thumb**: Reservations = 50% of limits

### 2. Monitor Before Optimizing

**Process**:
1. Deploy with generous limits
2. Monitor for 7 days
3. Analyze P95 usage
4. Set limits at P95 + 20%
5. Set reservations at P50

**Don't**: Guess limits without data

### 3. Different Limits per Environment

```yaml
# Development: No limits (flexibility)
# docker-compose.dev.yml
services:
  postgres:
    # No deploy section

# Production: Strict limits
# compose.prod.yml
services:
  postgres:
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G
```

### 4. Log Rotation (Essential)

```yaml
services:
  api:
    logging:
      driver: "json-file"
      options:
        max-size: "10m"   # 10MB per file
        max-file: "3"     # Keep 3 files
        # Total: 30MB max
```

**Production Recommendation**:
- `max-size: 50m`
- `max-file: 10`
- Total: 500MB per service

### 5. Use Profiles for Optional Services

```yaml
services:
  grafana:
    profiles: ["observability"]  # Only with --profile observability
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 1G
```

See: [Infrastructure Overview](../05-operations/infrastructure-overview.md) for profile details

---

## Related Documentation

### Infrastructure
- **[Infrastructure Overview](../05-operations/infrastructure-overview.md)** - Complete infrastructure guide
- **[Docker Compose Profiles](../05-operations/infrastructure-overview.md#docker-compose-profiles)** - Service profiles (Issue #702)
- **[Local Setup Guide](../00-getting-started/guida-setup-locale.md)** - Environment setup

### Monitoring
- **[Prometheus Setup](../05-operations/monitoring/prometheus-setup.md)** - Metrics collection
- **[Grafana Dashboards](../05-operations/monitoring/grafana-llm-cost-dashboard.md)** - Visualization
- **[Infrastructure Monitoring Runbook](../05-operations/runbooks/infrastructure-monitoring.md)** - Resource alerts (Issue #705)

### Operations
- **[Deployment Guide](../05-operations/deployment-guide.md)** - Production deployment
- **[Resource Planning](../05-operations/resource-planning.md)** - Capacity planning
- **[High Memory Usage Runbook](../05-operations/runbooks/high-memory-usage.md)** - Memory troubleshooting

### Component-Specific
- **[cAdvisor Setup](../../infra/dashboards/README.md)** - Container metrics (Issue #705)
- **[node-exporter Setup](../../infra/prometheus/README.md)** - Host metrics (Issue #705)

---

## External Resources

### Official Documentation
- [Docker Compose Resources](https://docs.docker.com/compose/compose-file/deploy/#resources)
- [Docker Stats](https://docs.docker.com/engine/reference/commandline/stats/)
- [Docker Update](https://docs.docker.com/engine/reference/commandline/update/)

### Optimization Guides
- [PostgreSQL Memory Tuning](https://wiki.postgresql.org/wiki/Tuning_Your_PostgreSQL_Server)
- [Redis Memory Optimization](https://redis.io/docs/management/optimization/memory-optimization/)
- [ASP.NET Core Performance](https://learn.microsoft.com/en-us/aspnet/core/performance/performance-best-practices)

---

## Changelog

### 2025-12-08: Documentation Consolidation

**Changes**:
- ✅ Consolidated `docker-compose-resource-limits.md` + `docker-resource-limits-faq.md` + `docker-resource-limits-quick-reference.md`
- ✅ Added comprehensive quick reference table (all 13 services)
- ✅ Added service-specific tuning guidelines
- ✅ Added monitoring and optimization strategies
- ✅ Added complete FAQ and troubleshooting sections
- ✅ Moved to `docs/02-development/docker-resources-guide.md`
- ✅ Updated all cross-references

---

**Version**: 2.0 (Post-Consolidation)
**Last Updated**: 2025-12-08
**Services Covered**: 13 (postgres, qdrant, redis, api, web, ollama, embedding, unstructured, smoldocling, prometheus, grafana, hyperdx, n8n)
**Total Production Resources**: 28 CPU cores, 68GB memory
**Documentation**: Single comprehensive resource guide

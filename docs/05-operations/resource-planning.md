# Docker Resource Planning Guide

> **Issue**: [#701 - Add resource limits to all Docker services](https://github.com/meepleai/monorepo/issues/701)
> **Status**: ✅ Implemented
> **Last Updated**: 2025-12-08

## Overview

This document outlines the resource requirements and scaling strategy for the MeepleAI Docker infrastructure. All services have been configured with appropriate CPU and memory limits to prevent resource exhaustion and ensure predictable performance.

## Quick Reference

### Total Resource Requirements

| Environment | CPU Limits | Memory Limits | CPU Reservations | Memory Reservations |
|-------------|-----------|---------------|------------------|---------------------|
| **Development** | 24.75 cores | 34.5 GB | 11.5 cores | 15.5 GB |
| **Production (HA)** | 46.5 cores | 72.5 GB | 24.5 cores | 42.5 GB |

### Minimum Host Requirements

| Environment | Cores | RAM | Disk Space | Notes |
|-------------|-------|-----|------------|-------|
| **Development** | 4+ cores | 16 GB | 50 GB SSD | Base development stack |
| **Development (Full)** | 8+ cores | 32 GB | 100 GB SSD | All ML services active |
| **Production** | 16+ cores | 64 GB | 500 GB SSD | Single node deployment |
| **Production (HA)** | 32+ cores | 128 GB | 1 TB SSD | High availability with replicas |

## Service-Specific Resource Allocation

### Database & Storage Services

#### PostgreSQL
```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 2G
    reservations:
      cpus: '1.0'
      memory: 1G
```

**Configuration**:
- `shared_buffers`: 512MB (25% of memory limit)
- `effective_cache_size`: 1536MB (75% of memory limit)
- `work_mem`: 16MB
- `maintenance_work_mem`: 256MB
- `shm_size`: 1GB (critical for parallel queries)

**Scaling**:
- **4GB RAM**: `shared_buffers=1GB`, `effective_cache_size=3GB`, `shm_size=2GB`
- **8GB RAM**: `shared_buffers=2GB`, `effective_cache_size=6GB`, `shm_size=4GB`
- **16GB RAM**: `shared_buffers=4GB`, `effective_cache_size=12GB`, `shm_size=8GB`

**Production Recommendation**: Set `shm_size` to 50% of container memory limit to support parallel queries and large sorts.

**References**: [PostgreSQL Runtime Configuration](https://www.postgresql.org/docs/current/runtime-config-resource.html)

#### Qdrant (Vector Database)
```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 4G
    reservations:
      cpus: '1.0'
      memory: 2G
```

**Memory Calculation**:
- **Baseline**: 1.2GB per 1M vectors (in-memory)
- **On-Disk Storage**: ~135MB per 1M vectors (with `on_disk: true`)
- **Recommendation**: Start with 4GB for development, scale to 8-16GB for production

**Scaling**:
- **1M vectors**: 4GB RAM
- **5M vectors**: 8GB RAM
- **10M+ vectors**: 16GB+ RAM or enable on-disk storage

**References**: [Qdrant Memory Consumption](https://qdrant.tech/articles/memory-consumption/)

#### Redis (Cache)
```yaml
deploy:
  resources:
    limits:
      cpus: '1.0'
      memory: 1G
    reservations:
      cpus: '0.5'
      memory: 512M
```

**Configuration**:
- `maxmemory`: 768MB (75% of container limit)
- `maxmemory-policy`: allkeys-lru

**Critical**: Always set `maxmemory` to prevent OOM kills. Without it, Redis will consume all available memory and get killed after days of operation.

**Scaling**:
- **Light caching**: 512MB-1GB
- **Moderate caching**: 1-2GB
- **Heavy caching**: 2-4GB

**References**: [Redis Memory Management](https://redis.io/docs/manual/eviction/)

### Machine Learning Services

#### Ollama (LLM Server)
```yaml
deploy:
  resources:
    limits:
      cpus: '4.0'
      memory: 8G
    reservations:
      cpus: '2.0'
      memory: 4G
```

**Environment Variables**:
```yaml
OLLAMA_MAX_LOADED_MODELS: 3
OLLAMA_KEEP_ALIVE: 5m
# GPU mode (uncomment if available):
# OLLAMA_GPU_MEMORY_FRACTION: 0.80
```

**Memory Requirements by Model Size**:
- **7B models**: 4-6GB RAM
- **13B models**: 8-12GB RAM
- **30B+ models**: 20GB+ RAM or GPU with VRAM

**GPU Considerations**:
- Enable GPU for 5-10x speedup
- Set `OLLAMA_GPU_MEMORY_FRACTION: 0.80` (conservative)
- Reduce `OLLAMA_MAX_LOADED_MODELS` to prevent swap

**References**: [Ollama GPU Configuration](https://markaicode.com/ollama-gpu-memory-allocation-vram-errors/)

#### Embedding Service
```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 4G
    reservations:
      cpus: '1.0'
      memory: 2G
```

**Model Memory Requirements**:
- **mxbai-embed-large (1024 dims)**: 2-3GB RAM
- **multilingual-e5-large (1024 dims)**: 3-4GB RAM

**Scaling**: Add GPU for faster batch processing.

#### Unstructured PDF Service (Stage 1)
```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 2G
    reservations:
      cpus: '1.0'
      memory: 1G
```

**Performance**: Handles 80% of PDFs, average processing time 1.3s per page.

**Scaling**: Increase CPU for concurrent PDF processing.

#### SmolDocling VLM Service (Stage 2)
```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 4G
    reservations:
      cpus: '1.0'
      memory: 2G
```

**Performance**: Fallback for complex layouts (15% of PDFs), average 3-5s per page.

**Model Size**: SmolDocling-256M-preview (~500MB)

**Scaling**: GPU highly recommended for production use (10x speedup).

#### Reranker Service (Cross-Encoder)
```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 2G
    reservations:
      cpus: '1.0'
      memory: 1G
```

**Model**: BAAI/bge-reranker-v2-m3 (~550MB)

**Performance**: Batch size 32, processes 100+ document pairs/second on CPU.

### Application Services

#### API (ASP.NET Core)
```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 2G
    reservations:
      cpus: '0.5'
      memory: 1G
```

**Notes**:
- .NET 3.0+ automatically respects Docker limits
- Server GC mode optimized for multi-core
- GC heap limited to ~75% of container memory
- No additional tuning required

**Scaling**:
- **High traffic**: 3-4 CPU cores, 4GB RAM
- **Very high traffic**: Horizontal scaling with load balancer

**References**: [.NET and Docker Together](https://devblogs.microsoft.com/dotnet/using-net-and-docker-together-dockercon-2019-update/)

#### Web (Next.js)
```yaml
deploy:
  resources:
    limits:
      cpus: '1.0'
      memory: 1G
    reservations:
      cpus: '0.5'
      memory: 512M
```

**Notes**:
- Lightweight Next.js server
- Static assets served efficiently
- No additional Node.js tuning needed

**Scaling**: Horizontal scaling with CDN for static assets.

#### n8n (Workflow Automation)
```yaml
deploy:
  resources:
    limits:
      cpus: '1.0'
      memory: 1G
    reservations:
      cpus: '0.5'
      memory: 512M
```

**Environment Variables**:
```yaml
NODE_OPTIONS: "--max-old-space-size=512"
```

**Formula**: `(container_limit_mb * 0.75) - 256`

**Scaling**:
- **Many workflows**: 2GB RAM, increase NODE_OPTIONS to `--max-old-space-size=1280`
- **Large data processing**: 4GB RAM, increase NODE_OPTIONS to `--max-old-space-size=2816`

**References**: [n8n Memory Management](https://docs.n8n.io/hosting/scaling/memory-errors/)

### Observability Services

#### Prometheus
```yaml
deploy:
  resources:
    limits:
      cpus: '1.0'
      memory: 2G
    reservations:
      cpus: '0.5'
      memory: 1G
```

**Storage**: 30-day retention, max 5GB TSDB size

**Scaling**:
- **More metrics**: Increase memory 2-4GB
- **Longer retention**: Increase storage limit or use remote storage

#### Grafana
```yaml
deploy:
  resources:
    limits:
      cpus: '1.0'
      memory: 1G
    reservations:
      cpus: '0.5'
      memory: 512M
```

**Notes**: Lightweight dashboard server, rarely needs scaling.

#### Alertmanager
```yaml
deploy:
  resources:
    limits:
      cpus: '0.5'
      memory: 512M
    reservations:
      cpus: '0.25'
      memory: 256M
```

**Notes**: Very lightweight, minimal resource usage.

#### HyperDX (Unified Observability)
```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 4G
    reservations:
      cpus: '1.0'
      memory: 2G
```

**Storage**: ClickHouse backend, 30-day retention for logs/traces

**Scaling**:
- **High ingestion**: 4-8 CPU cores, 8GB+ RAM
- **Long retention**: Increase ClickHouse memory

## Production Scaling Strategies

### High Availability (HA) Deployment

```yaml
api:
  replicas: 3  # Active-active load balancing
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 2G

web:
  replicas: 2  # Redundancy for frontend
  deploy:
    resources:
      limits:
        cpus: '1.0'
        memory: 1G

postgres:
  # Primary-Replica setup
  # Primary: Same as development
  # Replicas: 2x for read scaling

qdrant:
  # Cluster mode for large-scale vector search
  replicas: 3  # Distributed cluster

redis:
  # Redis Sentinel for HA
  replicas: 3  # 1 master + 2 replicas
```

### Vertical Scaling (Single Node)

| Service | Development | Production | Production (Heavy) |
|---------|-------------|------------|-------------------|
| **PostgreSQL** | 2 CPU, 2G RAM | 4 CPU, 8G RAM | 8 CPU, 16G RAM |
| **Qdrant** | 2 CPU, 4G RAM | 4 CPU, 8G RAM | 8 CPU, 32G RAM |
| **Ollama** | 4 CPU, 8G RAM | 8 CPU, 16G RAM | GPU with 24GB VRAM |
| **API** | 2 CPU, 2G RAM | 4 CPU, 4G RAM | 8 CPU, 8G RAM |

### Horizontal Scaling

**Stateless Services** (easy to scale):
- `api`: 3-5 replicas behind load balancer
- `web`: 2-3 replicas with CDN
- `embedding-service`: 2-3 replicas for batch processing
- `reranker-service`: 2-3 replicas for high query volume

**Stateful Services** (require careful planning):
- `postgres`: Primary-replica setup + connection pooling (PgBouncer)
- `qdrant`: Distributed cluster mode
- `redis`: Redis Sentinel (HA) or Redis Cluster (sharding)

## Monitoring and Adjustment

### Real-Time Monitoring

Use the provided monitoring script:
```bash
# Real-time stats
./scripts/docker-resource-monitor.sh --watch

# Capture baseline
./scripts/docker-resource-monitor.sh --baseline

# Analyze trends
./scripts/docker-resource-monitor.sh --analyze resource-baseline.txt
```

### Key Metrics to Monitor

| Metric | Threshold | Action |
|--------|-----------|--------|
| **Memory Usage** | > 80% of limit | Investigate memory leaks, increase limit |
| **CPU Usage** | > 70% sustained | Increase CPU limit or optimize code |
| **OOM Kills** | Any occurrence | Immediate investigation + increase memory |
| **Swap Usage** | > 0 MB | Reduce memory pressure or add more RAM |
| **Disk I/O** | > 80% utilization | Optimize queries or add faster storage |

### Adjustment Triggers

**Increase Resources If**:
- ✅ Memory usage consistently > 80% of limit
- ✅ CPU usage > 70% sustained over 1 hour
- ✅ OOM kills detected in logs
- ✅ Slow response times (P95 latency increase)
- ✅ Queue backlogs growing

**Decrease Resources If**:
- ✅ Memory usage < 50% of limit for 7+ days
- ✅ CPU usage < 30% average over 7+ days
- ✅ No performance degradation observed

## Troubleshooting

### PostgreSQL OOM Kills

**Symptoms**: Container restarts, `OOMKilled` in `docker inspect`

**Diagnosis**:
```bash
docker logs postgres | grep -i "out of memory"
docker stats postgres
```

**Solutions**:
1. Increase memory limit to 4GB
2. Reduce `shared_buffers` to 1GB (25% of new limit)
3. Increase `shm_size` to 2GB
4. Add connection pooling (PgBouncer)

### Redis "OOM command not allowed"

**Symptoms**: Redis refuses commands, errors in logs

**Diagnosis**:
```bash
docker exec redis redis-cli -a $(cat infra/secrets/redis-password.txt) INFO memory
```

**Solutions**:
1. Verify `maxmemory` is set (768mb for 1GB container)
2. Check eviction policy: `maxmemory-policy allkeys-lru`
3. Increase memory limit if needed
4. Clear unused keys

### Ollama Memory Swap

**Symptoms**: Slow inference, high memory usage

**Diagnosis**:
```bash
docker stats ollama
```

**Solutions**:
1. Reduce `OLLAMA_MAX_LOADED_MODELS` to 2
2. Decrease `OLLAMA_KEEP_ALIVE` to 3m
3. Increase memory limit to 12GB
4. Enable GPU with `OLLAMA_GPU_MEMORY_FRACTION: 0.80`

### ML Services Crash on Startup

**Symptoms**: Container exits, model download failures

**Diagnosis**:
```bash
docker logs embedding-service
docker logs smoldocling-service
```

**Solutions**:
1. Check available disk space (models can be 1-5GB)
2. Increase memory limit (minimum 2GB per service)
3. Verify internet connectivity for model downloads
4. Wait for `start_period` to complete (60-120s)

### Multiple Services Competing for Resources

**Symptoms**: Slow overall performance, high CPU/memory usage

**Diagnosis**:
```bash
docker stats --no-stream
```

**Solutions**:
1. Ensure reservations are set (prevent oversubscription)
2. Prioritize critical services (increase reservations)
3. Disable non-essential services (e.g., ML services in dev)
4. Scale horizontally across multiple hosts

## Best Practices

### 1. Always Use Both Limits and Reservations

**Why**: Limits prevent resource exhaustion, reservations guarantee minimum resources.

**Example**:
```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 2G
    reservations:
      cpus: '1.0'
      memory: 1G
```

### 2. Set Service-Specific Memory Configuration

**PostgreSQL**: `shared_buffers`, `effective_cache_size`
**Redis**: `maxmemory`, `maxmemory-policy`
**n8n**: `NODE_OPTIONS --max-old-space-size`
**Ollama**: `OLLAMA_MAX_LOADED_MODELS`, `OLLAMA_KEEP_ALIVE`

### 3. Monitor Before Scaling

**Step 1**: Establish baseline with `docker stats`
**Step 2**: Monitor for 7 days
**Step 3**: Analyze trends
**Step 4**: Adjust conservatively (+20-50%)
**Step 5**: Monitor impact

### 4. Test Resource Limits in Development

**Before deploying to production**:
```bash
# Test with realistic data volume
docker-compose up -d
./scripts/docker-resource-monitor.sh --watch

# Run load tests
# Monitor for OOM kills, CPU throttling

# Verify health checks pass
docker ps --filter "health=unhealthy"
```

### 5. Document Changes

**When adjusting limits**:
- Update this document with new values
- Document rationale (e.g., "Increased due to OOM kills during peak traffic")
- Include monitoring data supporting the change
- Set reminder to review after 30 days

## Environment-Specific Configurations

### Development
- **Purpose**: Local development, testing
- **Resources**: 16GB RAM, 4 cores minimum
- **Services**: All services enabled (optional ML services can be disabled)
- **Limits**: Conservative (current values in docker-compose.yml)

### Staging
- **Purpose**: Pre-production testing, load testing
- **Resources**: 32GB RAM, 8 cores
- **Services**: All production services + monitoring
- **Limits**: Production values (50% of production traffic capacity)

### Production
- **Purpose**: Live user traffic, high availability
- **Resources**: 64-128GB RAM, 16-32 cores
- **Services**: Full stack with HA replicas
- **Limits**: Scaled based on actual load (see Production Scaling section)

## Cost Optimization

### Cloud Provider Recommendations

| Provider | Instance Type | vCPUs | RAM | Cost/Month | Use Case |
|----------|--------------|-------|-----|------------|----------|
| **AWS** | t3.xlarge | 4 | 16 GB | ~$121 | Development |
| **AWS** | m6i.2xlarge | 8 | 32 GB | ~$350 | Staging/Light Production |
| **AWS** | r6i.4xlarge | 16 | 128 GB | ~$967 | Production (HA) |
| **DigitalOcean** | Droplet 4GB | 2 | 4 GB | ~$24 | Minimal dev |
| **DigitalOcean** | Droplet 16GB | 4 | 16 GB | ~$84 | Development |
| **DigitalOcean** | Droplet 64GB | 16 | 64 GB | ~$336 | Production |

**Savings Tips**:
1. Use spot/preemptible instances for development (-70% cost)
2. Disable non-essential ML services in development
3. Scale down during off-peak hours (nights, weekends)
4. Use managed services for PostgreSQL/Redis (reduce operational overhead)

## Validation Checklist

Before deploying to production, verify:

- ✅ All services have `deploy.resources.limits` defined
- ✅ All services have `deploy.resources.reservations` defined
- ✅ PostgreSQL has `shm_size` set (minimum 1GB)
- ✅ Redis has `maxmemory` configuration in command
- ✅ n8n has `NODE_OPTIONS` set appropriately
- ✅ Ollama has `OLLAMA_MAX_LOADED_MODELS` set
- ✅ Total reservations < host capacity (allow 20% buffer)
- ✅ Monitoring script runs without errors
- ✅ No OOM kills during 1-hour test run
- ✅ All health checks pass
- ✅ Documentation updated with any custom values

## References

**Official Documentation**:
- [Docker Compose Deploy Specification](https://docs.docker.com/reference/compose-file/deploy/)
- [Docker Resource Constraints](https://docs.docker.com/engine/containers/resource_constraints/)
- [PostgreSQL Runtime Configuration](https://www.postgresql.org/docs/current/runtime-config-resource.html)
- [Qdrant Memory Consumption](https://qdrant.tech/articles/memory-consumption/)
- [n8n Memory Errors](https://docs.n8n.io/hosting/scaling/memory-errors/)
- [.NET and Docker](https://devblogs.microsoft.com/dotnet/using-net-and-docker-together-dockercon-2019-update/)

**Additional Resources**:
- [Docker Resource Limits Quick Reference](../../02-development/docker-resource-limits-quick-reference.md)
- [Complete Resource Limits Guide](../../02-development/docker-compose-resource-limits.md)
- [Troubleshooting FAQ](../../02-development/docker-resource-limits-faq.md)
- [Resource Monitoring Script](../../scripts/docker-resource-monitor.sh)

---

**Last Verified**: 2025-12-08
**Issue**: [#701](https://github.com/meepleai/monorepo/issues/701)
**Owner**: Platform Engineering Team

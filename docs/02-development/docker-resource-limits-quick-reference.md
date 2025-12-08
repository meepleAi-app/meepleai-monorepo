# Docker Resource Limits - Quick Reference Card

**Print this page** or save as reference when configuring docker-compose.yml

---

## Basic Syntax

```yaml
services:
  postgres:
    deploy:
      resources:
        limits:
          cpus: '1.0'      # Hard ceiling: container cannot exceed
          memory: 1G       # Hard limit: OOM kill if exceeded
        reservations:
          cpus: '0.5'      # Guaranteed minimum from host
          memory: 512M     # Protected minimum, won't be reclaimed
```

### Units

- **CPU**: Decimal cores (0.5 = half core, 2.0 = 2 cores)
- **Memory**: Use M/MB, G/GB (512M, 1G, 2G, etc.)

---

## Limits vs Reservations Decision Tree

```
┌─ Is this a CRITICAL service?
│  ├─ YES → Set BOTH limits and reservations
│  │        Example: postgres, redis, qdrant
│  │
│  └─ NO → Set limits, light reservations
│           Example: api, web, tools

┌─ Does service BURST in memory?
│  ├─ YES → limit > reservation (2x gap is good)
│  │        Example: limit: 2G, reservation: 1G
│  │
│  └─ NO → limit ≈ reservation (small gap)
│           Example: limit: 1G, reservation: 900M

┌─ Sum of all reservations > host memory?
│  ├─ YES → Problem! Can't guarantee resources
│  │        Solution: Reduce reservations or add host RAM
│  │
│  └─ NO → OK, continue
```

---

## Service-Specific Quick Configs

### Database Tier

**PostgreSQL** (I/O-Bound, Memory Critical):
```yaml
postgres:
  shm_size: 1G
  environment:
    POSTGRES_INITDB_ARGS: |
      -c shared_buffers=256M
      -c effective_cache_size=1G
  deploy:
    resources:
      limits:
        cpus: '1.0'
        memory: 1G
      reservations:
        cpus: '0.5'
        memory: 512M
```

**Redis** (Memory-Bound Cache):
```yaml
redis:
  command:
    - redis-server
    - --maxmemory 512mb
    - --maxmemory-policy allkeys-lru
  deploy:
    resources:
      limits:
        cpus: '0.5'
        memory: 512M
      reservations:
        cpus: '0.25'
        memory: 256M
```

**Qdrant** (Vector DB, CPU-Heavy):
```yaml
qdrant:
  deploy:
    resources:
      limits:
        cpus: '1.5'
        memory: 2G
      reservations:
        cpus: '0.75'
        memory: 1G
```

---

### ML Services Tier

**Ollama** (LLM, VRAM Critical):
```yaml
ollama:
  environment:
    OLLAMA_GPU_MEMORY_FRACTION: '0.80'
    OLLAMA_MAX_LOADED_MODELS: '3'
    OLLAMA_KEEP_ALIVE: '5m'
  deploy:
    resources:
      limits:
        cpus: '4.0'
        memory: 8G
      reservations:
        cpus: '2.0'
        memory: 4G
```

**Embedding** (Lightweight Model Server):
```yaml
embedding:
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 2G
      reservations:
        cpus: '1.0'
        memory: 1G
```

**Reranker** (Lightweight Model Server):
```yaml
reranker:
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 2G
      reservations:
        cpus: '1.0'
        memory: 1G
```

**SmolDocling** (VLM, Image Processing):
```yaml
smoldocling:
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 3G
      reservations:
        cpus: '1.0'
        memory: 1.5G
```

**Unstructured** (PDF Extraction):
```yaml
unstructured:
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 2G
      reservations:
        cpus: '1.0'
        memory: 1G
```

---

### Observability Tier

**Prometheus** (Time-Series DB):
```yaml
prometheus:
  command:
    - '--storage.tsdb.retention.time=30d'
  deploy:
    resources:
      limits:
        cpus: '0.5'
        memory: 512M
      reservations:
        cpus: '0.25'
        memory: 256M
```

**Grafana** (Dashboard):
```yaml
grafana:
  deploy:
    resources:
      limits:
        cpus: '0.5'
        memory: 512M
      reservations:
        cpus: '0.25'
        memory: 256M
```

**HyperDX** (Observability Platform):
```yaml
hyperdx:
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 2G
      reservations:
        cpus: '1.0'
        memory: 1G
```

**AlertManager** (Notification):
```yaml
alertmanager:
  deploy:
    resources:
      limits:
        cpus: '0.25'
        memory: 256M
      reservations:
        cpus: '0.1'
        memory: 128M
```

---

### Application Tier

**n8n** (Workflow Engine, Node.js):
```yaml
n8n:
  environment:
    NODE_OPTIONS: '--max-old-space-size=1280'  # 75% of limit
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 2G
      reservations:
        cpus: '1.0'
        memory: 1G
```

**ASP.NET Core API** (.NET 9):
```yaml
api:
  environment:
    DOTNET_GC_SERVER: '1'
    DOTNET_GC_HEAP_COUNT: '2'
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 1.5G
      reservations:
        cpus: '1.0'
        memory: 768M
```

**Next.js Web** (Node.js Frontend):
```yaml
web:
  environment:
    NODE_OPTIONS: '--max-old-space-size=768'
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

## Total Resource Requirements

### Development Setup (Recommended Host: 16GB RAM, 4 cores)

| Service | CPU Limit | Memory Limit | CPU Res | Mem Res |
|---------|-----------|--------------|---------|---------|
| postgres | 1.0 | 1G | 0.5 | 512M |
| redis | 0.5 | 512M | 0.25 | 256M |
| qdrant | 1.5 | 2G | 0.75 | 1G |
| ollama | 4.0 | 8G | 2.0 | 4G |
| embedding | 2.0 | 2G | 1.0 | 1G |
| reranker | 2.0 | 2G | 1.0 | 1G |
| smoldocling | 2.0 | 3G | 1.0 | 1.5G |
| unstructured | 2.0 | 2G | 1.0 | 1G |
| prometheus | 0.5 | 512M | 0.25 | 256M |
| grafana | 0.5 | 512M | 0.25 | 256M |
| hyperdx | 2.0 | 2G | 1.0 | 1G |
| alertmanager | 0.25 | 256M | 0.1 | 128M |
| n8n | 2.0 | 2G | 1.0 | 1G |
| api | 2.0 | 1.5G | 1.0 | 768M |
| web | 1.0 | 1G | 0.5 | 512M |
| **TOTAL** | **24.75** | **31.5G** | **11.5** | **15.5G** |

**Notes**:
- Total limits exceed typical host (can burst when space available)
- Total reservations should fit in host memory
- Dev host should have: 16GB+ RAM, 8+ cores, 100GB SSD
- If OOM occurs, disable non-critical services (ollama, hyperdx, prometheus)

### Production Setup (Recommended Host: 32GB RAM, 8 cores)

Scale up reservations by 1.5-2x for production stability

| Service | CPU Limit | Memory Limit | CPU Res | Mem Res |
|---------|-----------|--------------|---------|---------|
| postgres | 2.0 | 4G | 1.5 | 3G |
| redis | 1.0 | 2G | 0.75 | 1.5G |
| qdrant | 4.0 | 8G | 2.0 | 4G |
| ollama | 8.0 | 16G | 4.0 | 8G |
| embedding | 4.0 | 4G | 2.0 | 2G |
| reranker | 4.0 | 4G | 2.0 | 2G |
| smoldocling | 4.0 | 6G | 2.0 | 3G |
| unstructured | 4.0 | 4G | 2.0 | 2G |
| prometheus | 1.0 | 1G | 0.5 | 512M |
| grafana | 1.0 | 1G | 0.5 | 512M |
| hyperdx | 4.0 | 8G | 2.0 | 4G |
| alertmanager | 0.5 | 512M | 0.25 | 256M |
| n8n | 4.0 | 4G | 2.0 | 2G |
| api (x3) | 3×4.0 | 3×3G | 3×2.0 | 3×1.5G |
| web (x2) | 2×2.0 | 2×1.5G | 2×1.0 | 2×768M |
| **TOTAL** | **46.5** | **72.5G** | **24.5** | **42.5G** |

**Notes**:
- Includes HA (api x3, web x2)
- Host should have: 32GB+ RAM, 8+ cores, 250GB SSD
- All critical services have guaranteed resources (reservations)
- Total reservations = ~42.5G (fits in 32GB+ with careful monitoring)

---

## Quick Checks

### Before Deploying

```bash
# 1. Verify total reservations fit
docker-compose config | grep -E "cpus|memory" | grep reservations
# Sum should be ≤ host resources

# 2. Check individual service limits
docker-compose config | grep -B 5 "cpus"

# 3. Test startup
docker-compose up -d
docker stats --no-stream
```

### During Operation

```bash
# Real-time monitoring
docker stats --all

# Memory growth check (should plateau)
watch -n 30 'docker stats --all --no-stream | grep -E "postgres|redis"'

# Export baseline
docker stats --all --no-stream > baseline-$(date +%s).txt

# Check for restarts
for svc in postgres redis qdrant api; do
  echo -n "$svc: "
  docker inspect $svc | grep RestartCount
done
```

---

## Common Edits

### Increase Memory for Service X

```yaml
# Before
api:
  deploy:
    resources:
      limits:
        memory: 1.5G

# After
api:
  deploy:
    resources:
      limits:
        memory: 3G    # Double the limit
```

### Add Reservation for Service X

```yaml
# Before
api:
  deploy:
    resources:
      limits:
        memory: 1.5G

# After
api:
  deploy:
    resources:
      limits:
        memory: 1.5G
      reservations:
        memory: 768M  # Add guarantee
```

### Reduce Observability Overhead

```yaml
# Disable non-critical services
prometheus:
  deploy:
    resources:
      limits:
        cpus: '0'    # Don't run

grafana:
  deploy:
    resources:
      limits:
        cpus: '0'

hyperdx:
  deploy:
    resources:
      limits:
        cpus: '0'
```

### Use Production Config

```bash
# Apply production limits
docker-compose -f docker-compose.yml \
               -f docker-compose.prod.yml \
               up -d
```

---

## Emergency Troubleshooting

### Service keeps restarting?

```bash
# Check reason
docker logs <service>
docker inspect <service> | grep -E "State|OOMKilled"

# Quick fix: Disable reservation temporarily
# (in docker-compose, remove reservations section)
# Then increase limit and reservations together
```

### All services failing?

```bash
# Host out of resources
docker system df
docker stats --all --no-stream | tail -1

# Quick fix: Start essential services only
docker-compose down
docker-compose up -d postgres redis api web
# Don't start: ollama, qdrant, prometheus, hyperdx
```

### Memory keeps growing?

```bash
# Check for leaks
docker stats postgres api --no-stream
# Watch for 1 hour - memory should stabilize

# If still growing: Code issue or cache not bounded
# Temporary fix: Restart container
docker restart api

# Permanent fix: Add cache limits or fix code
```

---

## See Also

- [Full Docker Compose Resource Limits Guide](./docker-compose-resource-limits.md)
- [FAQ & Troubleshooting](./docker-resource-limits-faq.md)
- [Docker Official Docs](https://docs.docker.com/reference/compose-file/deploy/)
- [Resource Monitoring Script](../../scripts/docker-resource-monitor.sh)

---

**Last Updated**: 2025-12-08
**Print-Friendly**: Yes (save as PDF for reference)

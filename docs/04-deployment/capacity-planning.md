# MeepleAI Capacity Planning

**Last Updated**: 2026-01-19
**Purpose**: Memory, storage, compute requirements for production scaling

---

## Quick Reference

| Scale | Users | Storage | RAM (Prod) | RAM (Test) | Infrastructure |
|-------|-------|---------|-----------|-----------|----------------|
| **Small** | 100 | 4GB | 5GB | 9GB | Small VPS (8GB RAM) |
| **Medium** | 1,000 | 40GB | 7GB | 9GB | Standard VPS (12-16GB) |
| **Large** | 10,000 | 404GB | 12GB | 10GB | Dedicated (24-32GB) |

**Critical Finding**: PostgreSQL crashes in tests at 2GB limit (needs 3-4GB for concurrent load)

---

## Data Model Assumptions

### Per-Game Storage
```
Game (avg):
├── PDFs: 2.5 files × 5MB = 12.5MB
├── Metadata: ~50KB (PostgreSQL)
└── Total: 12.5MB
```

### Per-User Storage
```
User (avg):
├── Games in library: 3 → 37.5MB PDFs
├── Personal uploads: 10% × 3 PDFs → 15MB
├── Vector chunks: 156 → 390KB (Qdrant)
└── Total: 39MB storage + 390KB vectors
```

### PDF Processing
```
PDF (5MB):
├── Text: ~1MB raw
├── Chunks: 20 @ 250KB each
├── Embeddings: 384-dim vectors
└── Qdrant: 20 × 2.5KB = 50KB
```

---

## Storage Scaling

| Scale | PDFs | PostgreSQL | Qdrant | Redis | Logs | Backups | Total | Plan For |
|-------|------|-----------|--------|-------|------|---------|-------|----------|
| 100 users | 3.9GB | 100MB | 39MB | 50MB | 500MB | - | 4.6GB | 15GB |
| 1K users | 39GB | 1GB | 390MB | 100MB | 2GB | 10GB | 52GB | 80-100GB |
| 10K users | 390GB | 10GB | 3.9GB | 500MB | 10GB | 50GB | 464GB | 600GB+ |

**Growth Rate**: ~40MB/user (38MB PDFs + 2MB overhead)

**Optimization Strategies**:
1. **PDF Deduplication**: Store shared game PDFs once (50-70% savings)
2. **Compression**: pgBackRest for PostgreSQL backups
3. **Object Storage**: Migrate to S3/R2 for PDFs ($0.023/GB vs $0.10/GB block storage)

---

## RAM Scaling

### Service Memory Requirements

#### PostgreSQL

**Formula**: `Peak = shared_buffers + (work_mem × concurrent_operations)`

| Config | Connections | Queries/Conn | shared_buffers | work_mem | Peak RAM | Recommended |
|--------|------------|--------------|----------------|----------|----------|-------------|
| **100 users prod** | 20 | 2 | 512MB | 16MB | 1.2GB | 2GB ✅ |
| **1K users prod** | 50 | 2 | 768MB | 16MB | 2.1GB | 3GB |
| **10K users prod** | 100 | 2 | 1280MB | 16MB | 3.7GB | 5GB |
| **Test suite** | 50 | 3 | 512MB | 16MB | **2.9GB** | **4GB** ⚠️ |

**Critical Issue**: Test environment crashes at 2GB limit (needs 3-4GB)

**Tuning Options**:
```yaml
# Option 1: Reduce work_mem for tests (reduces peak to 1.7GB)
POSTGRES_WORK_MEM: 8MB

# Option 2: Increase limit (recommended)
limits:
  memory: 3G  # Test environment
  memory: 5G  # 10K users production
```

#### Qdrant Vector Database

**HNSW Formula**: `Bytes/vector = (dimensions × 4 + M × 2 × 4) × 1.5 = 2.5KB`

| Users | Vectors | HNSW RAM | Payload | Total | Limit | Status |
|-------|---------|----------|---------|-------|-------|--------|
| 100 | 15,600 | 39MB | 10MB | 49MB | 4GB | ✅✅ Over-provisioned |
| 1,000 | 156,000 | 390MB | 100MB | 490MB | 4GB | ✅ Comfortable |
| 10,000 | 1,560,000 | 3.9GB | 500MB | 4.4GB | 4GB | ⚠️ Tight! |

**Optimization Options**:

| Option | RAM Reduction | Trade-off |
|--------|---------------|-----------|
| **In-Memory (current)** | - | Fast, high RAM |
| **Memory-Mapped** | ~50% (2GB at 10K) | Slight speed reduction |
| **Quantization (int8)** | 75% (~1GB at 10K) | ~2% accuracy loss |

**Recommendation**:
- <5K users: Keep in-memory
- 5-10K users: Enable mmap OR increase to 6GB
- >10K users: Quantization + sharding

#### Redis

**Configuration**: maxmemory=768MB, policy=allkeys-lru

| Users | Active Sessions (20%) | Session RAM | Cache Available | Total |
|-------|----------------------|-------------|----------------|-------|
| 100 | 20 | 100KB | ~768MB | ~300MB |
| 1,000 | 200 | 1MB | ~767MB | ~500MB |
| 10,000 | 2,000 | 10MB | ~758MB | ~700MB |

**Recommendation**: 768MB adequate for all scales (LRU handles overflow)

#### Embedding Service

**Model**: all-MiniLM-L6-v2 (22.7M params, 384-dim)

| Operation | Batch | RAM | Notes |
|-----------|-------|-----|-------|
| Model load | - | 90MB (FP32) / 43MB (FP16) | One-time |
| Inference (CPU) | 32 | ~500MB | Conservative |
| Inference (CPU) | 64 | ~1GB | Peak batch |
| Inference (GPU) | 256 | ~2GB | If GPU available |

**Optimization**: Use FP16 quantization (90MB → 43MB)
**Recommendation**: 4GB adequate, consider FP16 for production

#### Unstructured (PDF Processing)

**Memory Profile**:
- Base: ~200MB
- Per PDF: 300MB-1GB (complexity-dependent)
- Large PDFs (>10MB): Spike to 1.5GB
- OCR operations: Memory-intensive

**Recommendation**:
- Dev/Test: 4GB (concurrent processing)
- Production: 2-3GB (sequential with queuing)

---

## Test Environment Configuration

### Why Tests Need More RAM

Tests create higher memory pressure than production:
1. **High Concurrency**: 50-100 parallel DB connections
2. **No Connection Pooling**: Each test creates fresh connections
3. **Frequent Resets**: TRUNCATE operations, index rebuilds
4. **PDF Batches**: Multiple concurrent processing (no queuing)

### Test Crash Analysis

**Exit Code**: `-1073741819` (0xC0000005) = **ACCESS_VIOLATION** (Windows/WSL2)

**Timeline**:
```
t=0       Tests start (512MB PostgreSQL)
t=20min   Normal execution
t=20-26   High concurrency → PostgreSQL peaks at 2.9GB
t=26min   Exceeds 2GB limit → Docker OOM Killer
t=27min   Test crash: ACCESS_VIOLATION (-1073741819)
```

**Confidence**: 85% - Memory constraint hypothesis supported by evidence

### Test Environment Fix

**docker-compose.test.yml** (override for tests):
```yaml
services:
  postgres:
    deploy:
      resources:
        limits:
          memory: 4G  # +2GB for concurrent load with headroom
        reservations:
          memory: 2G

  qdrant:
    deploy:
      resources:
        limits:
          memory: 1G  # Reduce (small test dataset)
        reservations:
          memory: 512M

  redis:
    deploy:
      resources:
        limits:
          memory: 512M  # Reduce for tests

  embedding-service:
    deploy:
      resources:
        limits:
          memory: 2G  # Reduce from 4G

  unstructured-service:
    deploy:
      resources:
        limits:
          memory: 2G  # Keep for PDF processing
```

**Total Test RAM**: ~9.5GB services + overhead → **WSL2: 16-20GB recommended**

**WSL2 Configuration** (`C:\Users\Utente\.wslconfig`):
```ini
[wsl2]
memory=16GB
processors=4
swap=4GB
localhostForwarding=true
```

**Apply**: `wsl --shutdown` → Restart Docker Desktop

---

## Production Scaling Recommendations

### Small Scale (100 Users)

**Storage**: 15GB total (4GB data + 10GB images + overhead)

**RAM**:
- PostgreSQL: 2GB ✅
- Qdrant: 1GB (reduce from 4GB)
- Redis: 768MB ✅
- Embedding: 2GB (reduce from 4GB)
- Unstructured: 2GB (reduce from 4GB)
- **Total: 8GB** (current: 15GB over-provisioned)

**Infrastructure**: Small VPS (8GB RAM)

### Medium Scale (1,000 Users)

**Storage**: 100GB (40GB data + 50GB growth buffer + 10GB images)

**RAM**:
- PostgreSQL: **3GB** (increase)
- Qdrant: 2GB
- Redis: 1GB
- Embedding: 2GB
- Unstructured: 2GB
- **Total: 10GB**

**Configuration Changes**:
```yaml
postgres:
  environment:
    POSTGRES_SHARED_BUFFERS: 768MB  # 25% of 3GB
    POSTGRES_EFFECTIVE_CACHE_SIZE: 2GB
    POSTGRES_WORK_MEM: 16MB
  deploy:
    resources:
      limits:
        memory: 3G
```

**Infrastructure**: Standard VPS (12-16GB RAM)

### Large Scale (10,000 Users)

**Storage**: 600GB (404GB data + 100GB backups + 96GB growth)

**RAM**:
- PostgreSQL: **5GB**
- Qdrant: **6GB** (or quantization to 2GB)
- Redis: 1GB
- Embedding: 2GB
- Unstructured: 3GB
- **Total: 17GB**

**Advanced Strategies**:
- **Database sharding**: Split by user cohorts
- **Qdrant sharding**: Multiple instances, route by user_id
- **CDN offload**: PDFs to S3/R2
- **Quantization**: int8 (4x compression, minimal accuracy loss)

**Infrastructure**: Dedicated server (24-32GB RAM)

---

## Immediate Actions

### Fix Test Crash (Today)

- [ ] Create `C:\Users\Utente\.wslconfig` with `memory=16GB`
- [ ] Edit `infra/docker-compose.yml`: postgres `2G` → `3G`
- [ ] Run `wsl --shutdown`
- [ ] Restart Docker Desktop
- [ ] Verify: `docker system info | grep Memory`
- [ ] Start: `docker compose --profile dev up -d`
- [ ] Test: `dotnet test`
- [ ] Expected: No crash, all tests complete

### Optimize Current Scale (This Week)

- [ ] Reduce Qdrant to 2GB (over-provisioned at 100 users)
- [ ] Reduce Embedding to 2GB
- [ ] Add Prometheus memory monitoring
- [ ] Set capacity alerts (>80% threshold)
- [ ] Document baseline metrics

### Before 500 Users

- [ ] Increase PostgreSQL to 3GB (production)
- [ ] Plan Qdrant optimization (quantization vs increase)
- [ ] Implement PDF deduplication
- [ ] Add storage growth monitoring

### Before 5,000 Users

- [ ] Database sharding strategy
- [ ] Qdrant scaling decision (6GB vs sharding vs quantization)
- [ ] Object storage migration (S3/R2)
- [ ] Load testing for 10K simulation

---

## Monitoring

### Real-time Commands

```bash
# Watch container memory
docker stats --format "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}"

# WSL2 memory (Windows)
powershell -Command "Get-Process vmmem | Select-Object WorkingSet64"
```

### Prometheus Alerts

```yaml
- alert: PostgresHighMemory
  expr: container_memory_usage_bytes{name="postgres"} / container_spec_memory_limit_bytes > 0.85

- alert: QdrantHighMemory
  expr: container_memory_usage_bytes{name="qdrant"} / container_spec_memory_limit_bytes > 0.90
```

### Capacity Triggers

| Metric | Threshold | Action |
|--------|-----------|--------|
| PostgreSQL > 80% | Alert | Plan upgrade to next tier |
| Qdrant > 90% | Alert | Enable quantization or increase |
| Qdrant vectors > 1M | Warning | Plan sharding |
| Storage > 80% | Alert | Provision additional |
| PDF count > 50K | Warning | Consider object storage |

---

## Formulas

### Storage
```
Per User = (3 games × 2.5 PDFs × 5MB) + (3 personal PDFs × 5MB × 10%)
         = 37.5MB + 1.5MB = 39MB/user

Total Storage (N users) = N × 39MB
```

### Qdrant HNSW
```
Bytes/vector = (384 dim × 4 + 16 M × 2 × 4) × 1.5 overhead
             = (1,536 + 128) × 1.5
             = 2,496 ≈ 2.5KB

Vectors/user = (3 games × 2.5 PDFs × 20 chunks) + (3 personal × 20 × 10%)
             = 150 + 6 = 156 vectors

Memory/user = 156 × 2.5KB = 390KB

Total RAM (N users) = N × 390KB
```

### PostgreSQL Peak
```
Peak = shared_buffers + (work_mem × concurrent_ops)
     = 512MB + (16MB × 150 test connections)
     = 2.9GB (exceeds 2GB limit!)
```

---

## Related Documentation

- [Docker Compose Config](../../infra/docker-compose.yml)
- [Test Crash Analysis](../claudedocs/docker-memory-analysis-2026-01-19.md)
- [Integration Test Optimization](../05-testing/backend/integration-test-optimization.md)

---

**Research Sources**:
- [Qdrant Memory Consumption](https://qdrant.tech/articles/memory-consumption/)
- [PostgreSQL Performance Tuning](https://www.tigerdata.com/learn/postgresql-performance-tuning-key-parameters)
- [Redis Memory Optimization](https://redis.io/docs/latest/operate/oss_and_stack/management/optimization/memory-optimization/)
- [Docker Resource Constraints](https://docs.docker.com/engine/containers/resource_constraints/)
- [WSL2 Configuration](https://learn.microsoft.com/en-us/windows/wsl/wsl-config)

**Status**: Active - Immediate fixes required for test environment
**Next Review**: After 500 users or test pattern changes

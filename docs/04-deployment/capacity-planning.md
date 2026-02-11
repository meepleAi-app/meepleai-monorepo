# MeepleAI Capacity Planning Guide

**Last Updated**: 2026-01-19
**Purpose**: Memory, storage, and compute requirements for production scaling
**Research**: Based on deep analysis of service requirements and growth projections

---

## Executive Summary

### Quick Reference Table

| Scale | Users | Storage | RAM (Prod) | RAM (Test) | Cost Impact |
|-------|-------|---------|-----------|-----------|-------------|
| **Small** | 100 | 4GB | 5GB | 9GB | Low |
| **Medium** | 1,000 | 40GB | 7GB | 9GB | Medium |
| **Large** | 10,000 | 404GB | 12GB | 10GB | High |

### Critical Findings

1. **PostgreSQL undersized for tests**: 2GB limit causes crash (needs 3GB)
2. **Qdrant scales well**: 4GB adequate until 10K users (then needs 6GB)
3. **Redis well-sized**: 768MB/1GB sufficient for all scales
4. **WSL2 configuration**: Windows requires 12-16GB for test environment

---

## Input Assumptions

### Per-Game Data Model

```
Game (avg):
├── PDFs: 2.5 files (range: 2-3)
│   └── Size: 5MB each
├── Metadata: ~50KB (PostgreSQL)
└── Total: 12.5MB per game
```

### Per-User Data Model

```
User (avg):
├── Games in library: 3
│   ├── PDFs: 3 × 2.5 = 7.5 files
│   └── Storage: 37.5MB
├── Personal PDFs: 10% of users
│   ├── Count: 3 PDFs
│   └── Storage: 15MB
├── Vector chunks: 156 chunks
│   └── Qdrant: 390KB
└── Total: 39MB storage + 390KB vectors
```

### PDF-to-Vector Processing

```
PDF (5MB):
├── Text extraction: ~1MB raw text
├── Chunking: 20 chunks @ 250KB each
├── Embedding: 384-dim vectors
└── Storage: 20 × 2.5KB = 50KB per PDF
```

---

## Storage Scaling

### Disk Space Requirements

#### 100 Users (Small Scale)

```
Component                    Size        Details
──────────────────────────────────────────────────
PDF Files                    3.9GB       750 PDFs total
PostgreSQL Database          100MB       Metadata
Qdrant Vectors              39MB        15,600 vectors
Redis (persistent)          50MB        Sessions dump
Application Logs            500MB       30 days retention
Docker Images               10GB        All service images
──────────────────────────────────────────────────
TOTAL                       ~15GB       Comfortable
```

#### 1,000 Users (Medium Scale)

```
Component                    Size        Details
──────────────────────────────────────────────────
PDF Files                    39GB        7,500 PDFs total
PostgreSQL Database          1GB         Full metadata
Qdrant Vectors              390MB       156,000 vectors
Redis (persistent)          100MB       Sessions + cache dump
Application Logs            2GB         90 days retention
Docker Images               10GB        All service images
Backups (weekly)            10GB        Compressed DB backups
──────────────────────────────────────────────────
TOTAL                       ~62GB       Plan for 80-100GB
```

#### 10,000 Users (Large Scale)

```
Component                    Size        Details
──────────────────────────────────────────────────
PDF Files                    390GB       75,000 PDFs total
PostgreSQL Database          10GB        Full analytics
Qdrant Vectors              3.9GB       1,560,000 vectors
Redis (persistent)          500MB       Extensive cache
Application Logs            10GB        365 days retention
Docker Images               10GB        All service images
Backups (daily)             50GB        Compressed + incremental
──────────────────────────────────────────────────
TOTAL                       ~475GB      Plan for 600GB+
```

**Storage Growth Rate:** ~40MB per user (38MB PDFs + 2MB overhead)

---

## RAM Scaling

### Service-by-Service Analysis

#### PostgreSQL Memory Requirements

**Formula** (from research):
```
Base RAM = shared_buffers (25% of limit)
Peak RAM = shared_buffers + (work_mem × concurrent_queries × connections)
```

**Current Configuration:**
- shared_buffers: 512MB
- work_mem: 16MB
- Limit: 2GB

**Scaling Matrix:**

| Environment | Connections | Queries/Conn | Peak Calculation | Peak RAM | Recommended |
|-------------|------------|--------------|-----------------|----------|-------------|
| 100 users prod | 20 | 2 | 512MB + (16MB × 40) | 1.2GB | 2GB ✅ |
| 1K users prod | 50 | 2 | 512MB + (16MB × 100) | 2.1GB | 3GB |
| 10K users prod | 100 | 2 | 512MB + (16MB × 200) | 3.7GB | 5GB |
| **Test suite** | 50 | 3 | 512MB + (16MB × 150) | **2.9GB** | **4GB** ✅ |

**Critical:** Test environment currently at 2GB → crashes at 2.9GB peak!

**Tuning Options:**
```yaml
# Conservative (reduce work_mem for tests)
POSTGRES_WORK_MEM: 8MB    # Reduces peak to 1.7GB

# Recommended (increase limit)
deploy:
  resources:
    limits:
      memory: 3G  # Accommodates 2.9GB peak + overhead
```

#### Qdrant Vector Database

**HNSW Memory Formula** (from research):
```
Bytes per vector = (dimensions × 4 + M × 2 × 4) × 1.5
                 = (384 × 4 + 16 × 2 × 4) × 1.5
                 = 2,496 bytes ≈ 2.5KB
```

**Scaling:**

| Users | Vectors | HNSW RAM | Payload RAM | Total | Current Limit | Status |
|-------|---------|----------|-------------|-------|---------------|--------|
| 100 | 15,600 | 39MB | 10MB | 49MB | 4GB | ✅✅ Over |
| 1,000 | 156,000 | 390MB | 100MB | 490MB | 4GB | ✅✅ Over |
| 10,000 | 1,560,000 | 3.9GB | 500MB | 4.4GB | 4GB | ❌ **Tight!** |

**Optimization Options:**

1. **In-Memory (current)** - Fast, high RAM:
   ```python
   # collection config
   vectors: {"on_disk": false}
   hnsw_config: {"m": 16, "on_disk": false}
   ```

2. **Memory-Mapped** - Balanced:
   ```python
   vectors: {"on_disk": true}  # Reduces RAM by ~50%
   hnsw_config: {"m": 16, "on_disk": true}
   # 10K users: 3.9GB → ~2GB RAM (rest on disk)
   ```

3. **Quantization** - Memory efficient:
   ```python
   quantization_config: {
       "scalar": {"type": "int8"}  # 4x compression
   }
   # 10K users: 3.9GB → ~1GB RAM
   ```

**Recommendation:**
- < 5K users: Keep current (in-memory)
- 5-10K users: Enable mmap OR increase to 6GB
- > 10K users: Quantization + sharding

#### Redis Cache & Sessions

**Configuration:**
- maxmemory: 768MB
- Policy: allkeys-lru (evicts least recently used)

**Memory Formula:**
```
Sessions: num_active × 5KB
Cache: LRU-managed, up to maxmemory
Overhead: ~20% for fragmentation
```

**Scaling:**

| Users | Active Sessions | Session RAM | Cache Available | Total Usage |
|-------|----------------|-------------|----------------|-------------|
| 100 | 20 (20%) | 100KB | ~768MB | ~300MB |
| 1,000 | 200 (20%) | 1MB | ~767MB | ~500MB |
| 10,000 | 2,000 (20%) | 10MB | ~758MB | ~700MB |

**Recommendation:** Current 768MB adequate for all scales (LRU handles overflow)

#### Embedding Service

**Model:** all-MiniLM-L6-v2 (sentence-transformers)
- Parameters: 22.7M
- Dimensions: 384
- Model size: ~90MB (FP32), ~43MB (FP16)

**Memory Usage:**

| Operation | Batch Size | RAM Required | Notes |
|-----------|-----------|-------------|-------|
| Model load | - | 90MB | One-time |
| Inference (CPU) | 32 | ~500MB | Conservative |
| Inference (CPU) | 64 | ~1GB | Peak batch |
| Inference (GPU) | 256 | ~2GB | If GPU available |

**Current:** 4GB limit is adequate

**Optimization:**
```python
# Use FP16 quantization
model = SentenceTransformer(
    "all-MiniLM-L6-v2",
    model_kwargs={"torch_dtype": "float16"}
)
# Reduces model size: 90MB → 43MB
```

**Recommendation:** Keep 4GB, consider FP16 optimization for production

#### Unstructured Service (PDF Processing)

**Memory Profile:**
- Base service: ~200MB
- Per PDF processing: 300MB-1GB (depends on PDF complexity)
- Native libraries: Additional overhead

**Risk Areas:**
- Large PDFs (>10MB): Can spike to 1.5GB
- Concurrent processing: Memory accumulates
- OCR operations: Memory-intensive

**Recommendation:**
- Development/Test: 4GB (handle multiple concurrent)
- Production: 2-3GB (sequential processing with queuing)
- Add per-job memory limits

---

## Test Crash Root Cause Analysis

### Exit Code -1073741819 Explanation

**Error Code:** `-1073741819` (decimal) = `0xC0000005` (hex) = **ACCESS_VIOLATION**

**On Windows/WSL2 Docker**, this typically means:
1. Container exceeded memory limit
2. Docker OOM killer terminated container
3. Process received ACCESS_VIOLATION signal

### Crash Timeline

```
Test Start (t=0)
↓
Tests run normally (t=0 to t=20min)
- PostgreSQL memory: 512MB (shared_buffers)
- Gradual increase as queries accumulate
↓
High concurrency phase (t=20-26min)
- PostgreSQL peak: ~2.9GB
- Exceeds 2GB container limit
↓
Docker OOM Killer (t=26min)
- Detects PostgreSQL > 2GB
- Terminates postgres container
↓
Test Process Crash (t=27min)
- Test loses DB connection
- Receives ACCESS_VIOLATION
- Catastrophic failure: exit -1073741819
```

### Evidence Chain

1. **Timing**: Crash after 26 minutes (when many tests running concurrently)
2. **Memory calculation**: PostgreSQL peak 2.9GB > 2GB limit
3. **Research confirms**: Docker OOM → ACCESS_VIOLATION on Windows
4. **Pattern**: Consistent crash point suggests resource limit, not code bug

**Confidence:** 85% - Strong evidence supports memory constraint hypothesis

---

## Immediate Fixes (Test Environment)

### Fix #1: Increase PostgreSQL Memory

**File:** `infra/docker-compose.yml`

```yaml
# BEFORE
postgres:
  deploy:
    resources:
      limits:
        memory: 2G
      reservations:
        memory: 1G

# AFTER (for test environment)
postgres:
  deploy:
    resources:
      limits:
        memory: 3G      # +1GB for concurrent test load
      reservations:
        memory: 1.5G    # +500MB
```

**Impact:** Prevents PostgreSQL OOM during test suite concurrent execution

### Fix #2: Configure WSL2 Memory (Windows)

**File:** `C:\Users\Utente\.wslconfig` (create if doesn't exist)

```ini
[wsl2]
memory=16GB       # Total WSL2 memory allocation
processors=4      # CPU cores for WSL2
swap=4GB          # Swap space
localhostForwarding=true
```

**After creating/updating:**
```powershell
# Shutdown WSL2
wsl --shutdown

# Restart Docker Desktop
# (WSL2 will restart automatically)
```

**Impact:** Provides sufficient memory for all Docker services + test overhead

### Fix #3: Verify Docker Desktop Settings

**Path:** Docker Desktop → Settings → Resources

**Check:**
- Memory: Should show "WSL 2 based engine" (uses .wslconfig)
- If showing slider: Set to ≥ 16GB

### Verification Commands

```bash
# Check WSL2 memory
wsl -l -v

# Check Docker system info
docker system info | grep Memory

# Start services
cd infra && docker compose --profile dev up -d

# Monitor during test
docker stats &

# Run tests
cd apps/api && dotnet test
```

**Expected Result:**
- No catastrophic crash
- PostgreSQL memory stays under 3GB
- All 5,414 tests complete successfully

---

## Production Scaling Recommendations

### Small Scale (100 Users)

**Target:** Initial launch, MVP validation

**Storage:**
- Disk: 15GB total (4GB data + 10GB images + overhead)
- Backups: Weekly, 5GB

**RAM:**
- PostgreSQL: 2GB (current ✅)
- Qdrant: 1GB (current: 4GB - can reduce)
- Redis: 768MB (current ✅)
- Embedding: 2GB (current: 4GB - can reduce)
- Unstructured: 2GB (current: 4GB - can reduce)
- **Total: 8GB** (current: 15GB - over-provisioned)

**Optimization:**
```yaml
# Reduced limits for small scale
qdrant:
  deploy:
    resources:
      limits:
        memory: 1G      # Reduce from 4G
      reservations:
        memory: 512M

embedding-service:
  deploy:
    resources:
      limits:
        memory: 2G      # Reduce from 4G
      reservations:
        memory: 1G
```

**Cost Savings:** Can run on smaller VPS (8GB RAM vs 16GB)

### Medium Scale (1,000 Users)

**Target:** Product-market fit, growing user base

**Storage:**
- Disk: 100GB total (40GB data + 10GB images + 50GB growth buffer)
- Backups: Daily, 30GB retention

**RAM:**
- PostgreSQL: **3GB** (increase from 2GB)
- Qdrant: 2GB (adequate with headroom)
- Redis: 1GB (keep current)
- Embedding: 2GB (reduce from 4GB)
- Unstructured: 2GB (reduce from 4GB)
- **Total: 10GB**

**Configuration Changes:**
```yaml
postgres:
  deploy:
    resources:
      limits:
        memory: 3G      # +1GB for concurrent load
      reservations:
        memory: 1.5G
  environment:
    POSTGRES_SHARED_BUFFERS: 768MB     # 25% of 3GB
    POSTGRES_EFFECTIVE_CACHE_SIZE: 2GB # 66% of 3GB
    POSTGRES_WORK_MEM: 16MB            # Keep

qdrant:
  deploy:
    resources:
      limits:
        memory: 2G      # Reduce from 4G (390MB usage)
      reservations:
        memory: 1G
```

**Infrastructure:** Standard VPS with 12-16GB RAM

### Large Scale (10,000 Users)

**Target:** Established product, significant user base

**Storage:**
- Disk: 600GB total (404GB data + 50GB backups + 146GB growth)
- Backups: Daily + weekly, 100GB retention
- CDN: Consider offloading PDFs to S3/object storage

**RAM:**
- PostgreSQL: **5GB** (increase from 2GB)
- Qdrant: **6GB** (increase from 4GB)
- Redis: 1GB (LRU handles scale)
- Embedding: 2GB (keep)
- Unstructured: 3GB (increase slightly)
- **Total: 17GB**

**Configuration:**
```yaml
postgres:
  deploy:
    resources:
      limits:
        memory: 5G      # +3GB from current
      reservations:
        memory: 2.5G
  environment:
    POSTGRES_SHARED_BUFFERS: 1280MB    # 25% of 5GB
    POSTGRES_WORK_MEM: 16MB
    POSTGRES_MAX_CONNECTIONS: 200      # Scale up

qdrant:
  deploy:
    resources:
      limits:
        memory: 6G      # +2GB from current
      reservations:
        memory: 3G
  # Consider quantization:
  # collections with scalar quantization (int8)
  # reduces 3.9GB → ~1GB with minimal accuracy loss
```

**Infrastructure:** Dedicated server or cloud instance with 24-32GB RAM

**Scaling Beyond 10K:**
- **Database sharding**: Split by user cohorts
- **Qdrant sharding**: Multiple instances, route by user_id
- **CDN offload**: Move PDFs to object storage (S3, R2)
- **Quantization**: Enable int8 quantization (4x compression)

---

## Test Environment Requirements

### Why Tests Need More Memory

Test suites create higher memory pressure than production:

1. **High Concurrency:**
   - Tests run in parallel (Issue #2541 optimization)
   - 50-100 concurrent DB connections
   - Multiple queries per connection

2. **No Connection Pooling:**
   - Each test creates fresh connections
   - No reuse like production apps
   - Higher work_mem consumption

3. **Frequent DB Resets:**
   - TRUNCATE operations between tests
   - Index rebuilds
   - Higher maintenance_work_mem usage

4. **PDF Processing Batches:**
   - Multiple PDFs processed concurrently during tests
   - Native library memory accumulation
   - No queuing like production

### Test Environment Configuration

**Minimum for Successful Test Suite:**

```yaml
postgres:
  deploy:
    resources:
      limits:
        memory: 4G      # +2GB from production small scale, provides headroom
      reservations:
        memory: 2G

qdrant:
  deploy:
    resources:
      limits:
        memory: 1G      # Small test dataset
      reservations:
        memory: 512M

redis:
  deploy:
    resources:
      limits:
        memory: 512M    # Reduce from 1G for tests
      reservations:
        memory: 256M

embedding-service:
  deploy:
    resources:
      limits:
        memory: 2G      # Reduce from 4G
      reservations:
        memory: 1G

unstructured-service:
  deploy:
    resources:
      limits:
        memory: 2G      # Keep for PDF test processing
      reservations:
        memory: 1G
```

**Total Test RAM:** ~9.5GB (services only)
**WSL2 Allocation:** 16-20GB (recommended for overhead + OS)

### Test-Specific docker-compose Override

**Create:** `infra/docker-compose.test.yml`

```yaml
# docker-compose.test.yml - Test environment overrides
services:
  postgres:
    deploy:
      resources:
        limits:
          memory: 4G  # Increased for concurrent test load with headroom
        reservations:
          memory: 2G

  qdrant:
    deploy:
      resources:
        limits:
          memory: 1G  # Reduced - small test dataset
        reservations:
          memory: 512M

  redis:
    deploy:
      resources:
        limits:
          memory: 512M  # Reduced for tests
        reservations:
          memory: 256M

  embedding-service:
    deploy:
      resources:
        limits:
          memory: 2G  # Reduced from 4G
        reservations:
          memory: 1G

  reranker-service:
    deploy:
      resources:
        limits:
          memory: 1G  # Reduced from 2G
        reservations:
          memory: 512M

  unstructured-service:
    deploy:
      resources:
        limits:
          memory: 2G  # Keep for PDF processing
        reservations:
          memory: 1G
```

**Usage:**
```bash
# Start test environment
docker compose -f docker-compose.yml -f docker-compose.test.yml --profile dev up -d

# Run tests
cd apps/api && dotnet test

# Cleanup
docker compose down
```

---

## Memory Monitoring & Alerts

### Real-time Monitoring

```bash
# Watch container memory usage
docker stats --format "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}"

# Check WSL2 memory (Windows)
powershell -Command "Get-Process vmmem | Select-Object WorkingSet64"
```

### Prometheus Metrics (Production)

Add alerts for:
```yaml
# PostgreSQL memory
- alert: PostgresHighMemory
  expr: container_memory_usage_bytes{name="meepleai-postgres"} / container_spec_memory_limit_bytes > 0.85
  annotations:
    summary: "PostgreSQL using >85% memory"

# Qdrant memory
- alert: QdrantHighMemory
  expr: container_memory_usage_bytes{name="meepleai-qdrant"} / container_spec_memory_limit_bytes > 0.90
  annotations:
    summary: "Qdrant approaching memory limit"
```

### Capacity Planning Triggers

| Metric | Threshold | Action |
|--------|-----------|--------|
| PostgreSQL > 80% | Alert | Plan upgrade to next tier |
| Qdrant > 90% | Alert | Enable quantization or increase memory |
| Qdrant vectors > 1M | Warning | Plan sharding strategy |
| Storage > 80% | Alert | Provision additional storage |
| PDF count > 50K | Warning | Consider object storage migration |

---

## Cost Optimization Strategies

### Storage Optimization

1. **PDF Deduplication:**
   - Many users may have same game PDFs
   - Store unique PDFs once, reference by hash
   - Potential savings: 50-70% for shared game PDFs

2. **Compression:**
   - PDFs already compressed (minimal gain)
   - PostgreSQL: Enable pg_compress extension for JSONB
   - Backups: Use pgBackRest with compression

3. **Object Storage Migration:**
   - Move PDFs to S3/R2/Cloudflare R2
   - Keep metadata in PostgreSQL
   - Serve via CDN
   - Cost: ~$0.023/GB/month (S3) vs $0.10/GB/month (block storage)

### RAM Optimization

1. **Qdrant Quantization:**
   ```python
   # int8 quantization: 4x compression
   # 3.9GB → 975MB at 10K users
   # Trade-off: ~2% accuracy reduction
   ```

2. **PostgreSQL Connection Pooling:**
   - Use PgBouncer
   - Reduce max_connections from 200 → 50
   - Reduce work_mem consumption

3. **Embedding Model Optimization:**
   - Use ONNX runtime (20-30% faster)
   - Enable FP16 (50% memory reduction)
   - Consider Model2Vec (50x smaller, 90% accuracy)

### Compute Optimization

1. **Batch Processing:**
   - Queue PDF processing jobs
   - Process during off-peak hours
   - Reduce concurrent memory pressure

2. **Caching:**
   - Cache embedding results in Redis
   - Deduplicate vector generation for shared PDFs
   - Reduce embedding service load

---

## Implementation Roadmap

### Phase 1: Immediate (Fix Test Crash)

**Timeline:** 1 day

**Actions:**
1. Create `.wslconfig` with 16GB memory
2. Update `docker-compose.yml`: PostgreSQL 2GB → 3GB
3. Restart Docker Desktop
4. Re-run test suite
5. Verify no crash

**Expected Result:** Test suite completes successfully

### Phase 2: Optimize for Current Scale (100 users)

**Timeline:** 1 week

**Actions:**
1. Reduce over-provisioned services (Qdrant, Embedding)
2. Implement monitoring with Prometheus
3. Add capacity alerts
4. Document baseline metrics

**Expected Result:** Efficient resource usage, cost reduction

### Phase 3: Prepare for Growth (1,000 users)

**Timeline:** Before reaching 500 users

**Actions:**
1. Increase PostgreSQL to 3GB
2. Plan Qdrant mmap or quantization
3. Implement PDF deduplication
4. Add storage monitoring

**Expected Result:** Smooth scaling to 1K users

### Phase 4: Enterprise Scaling (10,000 users)

**Timeline:** Before reaching 5,000 users

**Actions:**
1. Upgrade PostgreSQL to 5GB
2. Upgrade Qdrant to 6GB OR enable quantization
3. Implement database sharding strategy
4. Migrate PDFs to object storage (S3/R2)
5. Add read replicas for PostgreSQL

**Expected Result:** Support 10K+ users with linear cost scaling

---

## Formulas & References

### Storage Calculation

```
Per User Storage = (games × pdfs_per_game × pdf_size) + (personal_pdfs × pdf_size × personal_pdf_percentage)
                 = (3 × 2.5 × 5MB) + (3 × 5MB × 0.10)
                 = 37.5MB + 1.5MB
                 = 39MB per user

Total Storage (N users) = N × 39MB
```

### Vector Memory Calculation

```
HNSW Memory Formula (Qdrant):
bytes_per_vector = (dimensions × 4 + M × 2 × 4) × overhead
                 = (384 × 4 + 16 × 2 × 4) × 1.5
                 = (1,536 + 128) × 1.5
                 = 2,496 bytes ≈ 2.5KB

Vectors per user = chunks_per_user
                 = (games × pdfs × chunks_per_pdf) + (personal_pdfs × chunks × percentage)
                 = (3 × 2.5 × 20) + (3 × 20 × 0.10)
                 = 150 + 6
                 = 156 vectors per user

Memory per user = 156 × 2.5KB = 390KB

Total Qdrant RAM (N users) = N × 390KB
```

### PostgreSQL Memory Formula

```
Peak RAM = shared_buffers + (work_mem × concurrent_operations)

Where:
- shared_buffers = 25% of memory limit
- work_mem = 16MB (current config)
- concurrent_operations = connections × queries_per_connection

Example (test suite):
Peak = 512MB + (16MB × 150) = 2.9GB
```

### Research Sources

**Qdrant HNSW:**
- [Qdrant Memory Consumption](https://qdrant.tech/articles/memory-consumption/)
- [Capacity Planning Guide](https://qdrant.tech/documentation/guides/capacity-planning/)
- [Vector Search Optimization](https://qdrant.tech/articles/vector-search-resource-optimization/)

**Sentence Transformers:**
- [all-MiniLM-L6-v2 Model Card](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)
- [Inference Efficiency Guide](https://sbert.net/docs/sentence_transformer/usage/efficiency.html)
- [Memory Requirements](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/discussions/39)

**PostgreSQL:**
- [Performance Tuning Parameters](https://www.tigerdata.com/learn/postgresql-performance-tuning-key-parameters)
- [Memory Tuning Guide](https://www.enterprisedb.com/postgres-tutorials/how-tune-postgresql-memory)
- [High Connections Optimization](https://dev.to/matthewlafalce/optimizing-postgresql-for-high-connections-a-comprehensive-guide-21o9)

**Redis:**
- [Key Eviction Policies](https://redis.io/docs/latest/develop/reference/eviction/)
- [Memory Optimization](https://redis.io/docs/latest/operate/oss_and_stack/management/optimization/memory-optimization/)
- [Azure Best Practices](https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/cache-best-practices-memory-management)

**Docker:**
- [Resource Constraints](https://docs.docker.com/engine/containers/resource_constraints/)
- [WSL2 Configuration](https://learn.microsoft.com/en-us/windows/wsl/wsl-config)
- [Memory Limits Guide](https://overcast.blog/docker-resource-limits-a-guide-5461355171b2)

---

## Quick Action Checklist

### To Fix Test Crash (Today)

- [ ] Create `C:\Users\Utente\.wslconfig` with memory=16GB
- [ ] Edit `infra/docker-compose.yml`: postgres memory 2G → 3G
- [ ] Run `wsl --shutdown` (PowerShell)
- [ ] Restart Docker Desktop
- [ ] Verify: `docker system info | grep Memory`
- [ ] Run: `cd infra && docker compose --profile dev up -d`
- [ ] Test: `cd apps/api && dotnet test`
- [ ] Expected: No crash, all tests complete

### To Optimize for Current Scale (This Week)

- [ ] Reduce Qdrant to 2GB (over-provisioned)
- [ ] Reduce Embedding service to 2GB
- [ ] Add Prometheus monitoring for memory
- [ ] Set up capacity alerts (>80% threshold)
- [ ] Document baseline memory metrics

### Before Reaching 500 Users

- [ ] Increase PostgreSQL to 3GB (production)
- [ ] Plan Qdrant optimization (quantization vs memory increase)
- [ ] Implement PDF deduplication for shared games
- [ ] Add storage growth monitoring

### Before Reaching 5,000 Users

- [ ] Design database sharding strategy
- [ ] Plan Qdrant scaling (6GB vs sharding vs quantization)
- [ ] Evaluate object storage migration (S3/R2)
- [ ] Load testing for 10K user simulation

---

## Related Documentation

- [Docker Compose Configuration](../infra/docker-compose.yml)
- [Test Crash Analysis](../docs/claudedocs/docker-memory-analysis-2026-01-19.md)
- [Integration Test Optimization](./05-testing/backend/integration-test-optimization.md)
- [Architecture Overview](./01-architecture/README.md)

---

## Appendix: Detailed Service Specs

### All-MiniLM-L6-v2 Specifications

```
Model: sentence-transformers/all-MiniLM-L6-v2
Parameters: 22.7M
Embedding Dimensions: 384
Input Limit: 256 word pieces
Model Size:
  - FP32: 90MB
  - FP16: 43MB
  - INT8: 22MB
  - INT4: 11MB
Inference (CPU, batch=32): ~500MB RAM
Inference (CPU, batch=64): ~1GB RAM
```

### Qdrant HNSW Parameters

```
Current Configuration (inferred):
- m: 16 (HNSW graph edges)
- ef_construct: 100 (construction quality)
- on_disk: false (in-memory for speed)

Optimization Options:
- m=0: Disable indexing during upload (memory efficient)
- on_disk=true: Use mmap (50% RAM reduction)
- quantization: int8 (4x compression, ~2% accuracy loss)
```

### PostgreSQL Tuning Matrix

```
Parameter              | 2GB Limit | 3GB Limit | 5GB Limit
──────────────────────────────────────────────────────────
shared_buffers         | 512MB     | 768MB     | 1280MB
effective_cache_size   | 1536MB    | 2304MB    | 3840MB
work_mem               | 16MB      | 16MB      | 24MB
maintenance_work_mem   | 256MB     | 384MB     | 640MB
max_connections        | 100       | 150       | 200
```

---

**Last Updated**: 2026-01-19
**Next Review**: After reaching 500 users or if test patterns change
**Owner**: Infrastructure team
**Status**: Active - Immediate fixes required for test environment

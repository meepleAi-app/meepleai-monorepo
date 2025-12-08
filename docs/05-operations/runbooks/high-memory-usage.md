# Runbook: High Memory Usage

**Alert**: `HighMemoryUsage`
**Severity**: WARNING
**Threshold**: Memory usage > 80% for 5 minutes
**Expected Response Time**: < 30 minutes

## Symptoms

**Observable indicators when this alert fires:**
- Alert: "HighMemoryUsage" firing in Alertmanager
- API memory usage > 80% of allocated limit
- Dashboard shows memory consumption trend increasing
- Potential performance degradation (GC pressure, swapping)
- Risk of OOMKill if memory continues growing

## Impact

**Effect on system and users:**
- **User Experience**: Slow response times, intermittent errors, potential service crashes
- **Data Integrity**: Generally not affected unless OOMKill occurs during write operation
- **Business Impact**: Performance degradation, service instability, potential downtime if OOMKilled
- **System Health**: High GC pressure, risk of cascading failures, potential container restart

## Investigation Steps

### 1. Verify Alert (30 seconds)

**Dashboard Check**:
```
http://localhost:3001/d/infrastructure
```

**Verification checklist**:
- ✅ Is memory usage actually > 80%?
- ✅ Is memory usage stable or increasing?
- ✅ When did high usage start? (correlate with events)
- ✅ Which service has high memory? (API, Postgres, Redis, Qdrant)

**Prometheus Query**:
```promql
process_working_set_bytes / process_memory_limit_bytes
```

**If false alarm**:
- Silence alert for 30 minutes
- Document if memory spike was temporary (large batch operation)
- Create issue to adjust alert threshold if recurring

### 2. Identify Memory Trend (1 minute)

**Questions to answer**:
1. **Is memory growing?** (steady increase or stable high usage)
2. **How fast is growth?** (MB/hour - indicates leak severity)
3. **When did it start?** (deployment, config change, traffic spike)
4. **Will it OOMKill soon?** (estimate time to 100% based on trend)

**Prometheus queries**:
```promql
# Memory usage over time (1 hour window)
process_working_set_bytes{job="meepleai-api"}[1h]

# Memory growth rate (MB/hour)
deriv(process_working_set_bytes{job="meepleai-api"}[1h])

# Time to OOMKill estimate (if growing linearly)
# Calculate: (limit - current) / growth_rate
```

**Dashboard trend analysis**:
- Navigate to Infrastructure dashboard
- Check "API Memory Usage" panel
- Look for steady increase (leak) vs stable high (legitimate usage)

### 3. Check Recent Changes (1 minute)

**Recent deployment?**
```bash
# Check recent commits (last 4 hours)
git log --oneline --since="4 hours ago"

# Check deployed version
curl http://localhost:8080/health | jq '.version'
```

**Recent configuration changes?**
- Cache settings (cache size, TTL)
- Connection pool sizes (database, HTTP clients)
- Batch operation sizes (PDF processing, bulk imports)
- Memory limits in docker-compose.yml

**Recent load changes?**
- Traffic spike (more concurrent users)
- Large file uploads (PDF processing)
- Background jobs running (re-indexing, batch operations)

### 4. Analyze Memory Composition (2 minutes)

**Docker stats details**:
```bash
# Current memory usage
docker stats --no-stream api

# Memory limit
docker inspect api | grep -i memory

# Check if swap is being used (Linux)
free -h
# Look for swap usage (indicates memory pressure)
```

**Process memory breakdown** (if available):
```bash
# .NET memory metrics
docker compose exec api curl http://localhost:8080/metrics | grep "dotnet_gc"

# Heap size
# Gen0, Gen1, Gen2 collections
# Large object heap
```

### 5. Check for Memory Leaks (2 minutes)

**Leak indicators**:
```bash
# Check GC stats
docker compose logs api | grep -i "gc\|garbage\|heap"

# Check object retention
docker compose exec api curl http://localhost:8080/metrics | grep "dotnet_gc_heap_size_bytes"

# Check collection counts (high Gen2 = potential leak)
docker compose exec api curl http://localhost:8080/metrics | grep "dotnet_gc_collection"
```

**HyperDX - Memory-related logs**:
```
http://localhost:8180
Filter: memory OR gc OR heap AND @timestamp:[now-1h TO now]
```

**Look for**:
- OutOfMemoryException warnings
- GC pause time increasing
- Large allocations (MB+ objects)
- Undisposed objects (missing using statements)

### 6. Check Application Metrics (1 minute)

**Request metrics** (correlate with memory):
```promql
# Active requests (high concurrency = high memory)
http_server_active_requests

# Request rate (load correlation)
rate(http_server_request_duration_count[5m])

# Long-running requests (may hold memory)
http_server_request_duration_bucket{le="+Inf"}
```

**Application-specific metrics**:
```bash
# PDF processing queue (large files = high memory)
curl http://localhost:8080/metrics | grep "pdf_queue"

# Cache size (Redis memory usage)
docker compose exec redis redis-cli info memory | grep used_memory_human

# Vector operations (Qdrant memory)
curl http://localhost:6333/metrics | grep memory
```

### 7. Check System Resources (1 minute)

**System-wide memory**:
```bash
# Total system memory
free -h

# Per-process memory (top processes)
docker stats --no-stream

# Swap usage (indicates memory pressure)
swapon --show
```

**Docker resource limits**:
```bash
# Check all container memory limits
docker compose config | grep -A 5 "resources:"

# Verify limits are appropriate for workload
```

## Common Root Causes & Fixes

### Cause 1: Memory Leak in Application Code

**Symptoms**:
- Memory usage growing steadily over hours/days
- Eventually leads to OOMKill (exit code 137)
- GC unable to reclaim memory (Gen2 collections increasing)
- Specific code path holding references (event handlers, caches)

**Investigation**:
```bash
# Check GC pressure
docker compose exec api curl http://localhost:8080/metrics | grep "dotnet_gc_"

# Look for high Gen2 collection count
# Look for growing heap size despite GC

# Profile with dotMemory (requires tooling)
# Identify objects with high retention
# Find root references preventing GC
```

**Fix**:
```bash
# Option A: Restart API (temporary relief)
docker compose restart api
# This clears memory but leak will recur

# Option B: Hotfix if leak source identified
# Common leaks:
# - Event handlers not unsubscribed
# - Static collections growing unbounded
# - IDisposable not disposed (DbContext, HttpClient)
# - Large objects in closures
# Fix in code, deploy fix

# Option C: Increase memory limit (buys time)
# Edit docker-compose.yml:
# api:
#   deploy:
#     resources:
#       limits:
#         memory: 2G  # Increased from 1G
docker compose down
docker compose up -d

# Option D: Implement memory profiling (long-term)
# Use dotMemory or PerfView to find leak
# Fix root cause in code
```

**Verification**:
```bash
# Memory usage stable after restart
docker stats --no-stream api
# Should be <500MB after restart

# Monitor for growth (check every 30 minutes)
# Memory should stay stable, not grow continuously

# No OOMKill events
docker compose ps api
# Should show "Up" continuously, not restart
```

**Prevention**:
- Code review for IDisposable usage (all using statements)
- Add memory profiling to CI/CD (detect leaks early)
- Monitor memory growth rate (alert on >10MB/hour growth)
- Regular memory profiling sessions (quarterly)

**Resolution time**: 2 minutes (restart), hours to days (fix leak)

### Cause 2: Large Batch Operation

**Symptoms**:
- Memory spike during specific operations (PDF upload, bulk import)
- Memory usage high but stable (not continuously growing)
- Returns to normal after operation completes
- Correlates with specific user actions or cron jobs

**Investigation**:
```bash
# Check active operations
docker compose logs api | grep -i "batch\|bulk\|upload\|processing"

# Check PDF processing queue
curl http://localhost:8080/api/internal/pdf-queue-status

# Check background jobs
docker compose logs api | grep -i "background\|scheduled"
```

**Fix**:
```bash
# Option A: Reduce batch size
# Edit appsettings.json:
# "PdfProcessing": {
#   "MaxConcurrentProcessing": 2  # Reduced from 5
# }
docker compose restart api

# Option B: Implement streaming (code change)
# Process large files in chunks
# Use streaming APIs instead of loading full file

# Option C: Increase memory limit (if legitimate usage)
# Edit docker-compose.yml:
# api:
#   deploy:
#     resources:
#       limits:
#         memory: 3G  # Increased for large operations
docker compose down
docker compose up -d

# Option D: Throttle batch operations
# Add queue with max concurrency
# Process operations sequentially instead of parallel
```

**Verification**:
```bash
# Batch operation completes without memory spike
# Monitor memory during next batch operation

# Memory returns to baseline after operation
docker stats --no-stream api
# Should be <60% after operation completes

# No errors during batch processing
# Check HyperDX: batch OR processing AND error
```

**Prevention**:
- Profile memory usage for batch operations
- Set appropriate batch sizes (based on profiling)
- Implement streaming for large files
- Add memory usage metrics for batch operations

**Resolution time**: 5-15 minutes (config change) or hours (code refactoring)

### Cause 3: Cache Growth Unbounded

**Symptoms**:
- Memory growing steadily but slowly
- Cache hit rate very high (>95%)
- Redis or in-memory cache growing
- No obvious memory leak in application code

**Investigation**:
```bash
# Check Redis memory usage
docker compose exec redis redis-cli info memory | grep used_memory_human

# Check number of keys
docker compose exec redis redis-cli dbsize

# Check cache eviction policy
docker compose exec redis redis-cli config get maxmemory-policy

# Check if evictions happening
docker compose exec redis redis-cli info stats | grep evicted_keys
```

**Fix**:
```bash
# Option A: Configure maxmemory and eviction policy
docker compose exec redis redis-cli config set maxmemory 256mb
docker compose exec redis redis-cli config set maxmemory-policy allkeys-lru

# Option B: Reduce cache TTL (expire faster)
# Edit appsettings.json:
# "Caching": {
#   "DefaultTTLMinutes": 5  # Reduced from 10
# }
docker compose restart api

# Option C: Clear cache manually (temporary)
docker compose exec redis redis-cli flushdb
# Cache will rebuild automatically

# Option D: Increase Redis memory limit (if legitimate)
# Edit docker-compose.yml:
# redis:
#   command: redis-server --maxmemory 512mb
docker compose restart redis
```

**Verification**:
```bash
# Redis memory stable or decreasing
docker compose exec redis redis-cli info memory | grep used_memory_human

# Evictions happening (old keys being removed)
docker compose exec redis redis-cli info stats | grep evicted_keys
# Should be > 0

# Cache still effective (hit rate >70%)
docker compose exec redis redis-cli info stats | grep keyspace_hits
```

**Prevention**:
- Configure maxmemory and eviction policy from start
- Monitor cache size continuously
- Use appropriate TTLs (balance performance vs memory)
- Profile cache key distribution (identify outliers)

**Resolution time**: 2-10 minutes

### Cause 4: High Concurrency

**Symptoms**:
- Memory spikes during high traffic periods
- Memory usage correlates with active request count
- Many concurrent requests (>50 active)
- Memory returns to normal when traffic decreases

**Investigation**:
```promql
# Check concurrent requests
http_server_active_requests

# Correlate with memory usage
process_working_set_bytes AND http_server_active_requests

# Check thread pool stats
dotnet_threadpool_num_threads
```

**Fix**:
```bash
# Option A: Add request throttling
# Edit appsettings.json:
# "Concurrency": {
#   "MaxConcurrentRequests": 50  # Limit concurrent requests
# }
docker compose restart api

# Option B: Scale horizontally (add API replicas)
docker service scale meepleai_api=3

# Option C: Optimize per-request memory usage
# Review code for large allocations per request
# Use object pooling (ArrayPool, MemoryPool)
# Stream large responses instead of buffering

# Option D: Increase memory limit (if within capacity)
# Edit docker-compose.yml:
# api:
#   deploy:
#     resources:
#       limits:
#         memory: 2G
docker compose down
docker compose up -d
```

**Verification**:
```bash
# Concurrent request count limited
curl http://localhost:8080/metrics | grep "http_server_active_requests"
# Should be < configured limit

# Memory usage per request reasonable
# Monitor memory while load testing
# Should not grow >5MB per concurrent request

# No 429 Too Many Requests errors
# Check error rate dashboard
```

**Prevention**:
- Configure max concurrency limits
- Load test to find safe concurrency level
- Monitor active request count
- Implement request queuing (vs rejection)

**Resolution time**: 10-30 minutes

## Mitigation Steps

### Immediate (< 5 minutes)

1. **Check memory trend** (growing vs stable):
   ```bash
   docker stats --no-stream api
   # Wait 1 minute, check again
   docker stats --no-stream api
   # Compare values
   ```

2. **Silence alert** (if actively working):
   ```
   http://localhost:9093
   Silences → New Silence
   alertname=HighMemoryUsage, duration=30m
   Comment: "Investigating memory leak, monitoring trend"
   ```

3. **Notify team**:
   ```
   #incidents: "🚨 HighMemoryUsage alert - API at 85% memory - investigating"
   ```

### Short-term (< 30 minutes)

1. **Identify root cause** (use investigation steps 1-7 above)

2. **Apply fix** (use appropriate fix from Common Root Causes section)

3. **Verify fix**:
   - Memory usage decreases to < 70%
   - Memory stable (not growing over time)
   - No OOMKill events (check `docker compose ps` for restarts)

4. **Update incident channel**:
   - Post resolution: "✅ Fixed by restarting API - memory now 45% - monitoring"
   - Or post ETA: "⏳ Reducing batch size - testing - ETA 15 min"

### Medium-term (< 2 hours)

1. **Monitor for recurrence**:
   - Watch Infrastructure dashboard for 30-60 minutes
   - Ensure memory stays < 70%
   - Check for gradual growth (indicates leak not fully fixed)

2. **Root cause analysis**:
   - Why was memory high? (leak, legitimate usage, config issue)
   - Is fix permanent or temporary? (restart vs code fix)
   - Any code changes needed? (dispose resources, optimize allocations)

3. **Create follow-up tasks**:
   - GitHub issue for memory leak fix (if restart was applied)
   - Add memory profiling session (dotMemory investigation)
   - Update monitoring (add memory growth rate alert)

## Escalation

### When to Escalate

Escalate if:
- ✅ Memory continues growing despite fixes (leak not identified)
- ✅ Memory at 95%+ and OOMKill imminent (need immediate expertise)
- ✅ Cannot identify leak source after 30 minutes
- ✅ Requires code profiling expertise (dotMemory, PerfView)
- ✅ Architectural change needed (caching strategy, data structures)

### Escalation Contacts

**Performance team**:
- #engineering Slack channel
- Senior backend engineer (for memory profiling)

**DevOps/Infrastructure**:
- #ops Slack channel
- On-call DevOps (for resource scaling)

**Emergency contacts**:
- Team Lead: [name] - [phone]
- CTO: [name] - [phone] (critical only)

## Prevention

### Monitoring

1. **Memory metrics**:
   ```promql
   # Memory usage (alert at 80%)
   process_working_set_bytes / process_memory_limit_bytes > 0.8

   # Memory growth rate (alert on >10MB/hour sustained)
   deriv(process_working_set_bytes[1h]) > 10485760

   # GC pressure (alert on high Gen2 collections)
   rate(dotnet_gc_collections_total{generation="2"}[5m]) > 0.1
   ```

2. **Alert levels**:
   - Memory > 70% for 10 min = INFO (early warning)
   - Memory > 80% for 5 min = WARNING (current)
   - Memory > 90% for 2 min = CRITICAL (imminent OOMKill)

3. **Trending alerts**:
   - Memory growing >50MB/hour for 2 hours
   - Indicates leak requiring investigation

### Configuration

1. **Appropriate memory limits** (docker-compose.yml):
   ```yaml
   services:
     api:
       deploy:
         resources:
           limits:
             memory: 2G  # Based on profiling
           reservations:
             memory: 512M  # Minimum for startup
   ```

2. **GC tuning** (environment variables):
   ```yaml
   api:
     environment:
       - DOTNET_gcServer=true
       - DOTNET_GCHeapCount=2
       - DOTNET_GCHeapAffinitizeMask=0x3
   ```

3. **Cache limits** (appsettings.json):
   ```json
   "Caching": {
     "MaxMemoryMB": 256,
     "ExpirationMinutes": 10,
     "CompactionPercentage": 0.25
   }
   ```

### Code Quality

1. **IDisposable pattern**:
   - All DbContext in using statements
   - All HttpClient via IHttpClientFactory
   - All streams disposed properly
   - Event handlers unsubscribed

2. **Memory-efficient patterns**:
   - Use ArrayPool for large byte arrays
   - Stream large responses (don't buffer)
   - Avoid large string concatenation (use StringBuilder)
   - Limit collection sizes (ToList() with Take())

3. **Profiling practices**:
   - Profile application quarterly (dotMemory)
   - Identify hot paths for allocations
   - Review high-allocation code paths
   - Implement allocation benchmarks

## Testing This Runbook

**Simulate high memory usage**:
```bash
# Option A: Load test (gradual memory increase)
k6 run --vus 100 --duration 10m scripts/load-test.js
# Monitor memory during load

# Option B: Trigger memory-intensive operation
# Upload large PDF files
for i in {1..10}; do
  curl -X POST http://localhost:8080/api/v1/pdf/upload \
    -F "file=@large-file.pdf" &
done

# Wait 5 minutes for alert to fire
# Follow runbook investigation steps
```

**Expected behavior**:
- HighMemoryUsage alert fires when memory > 80%
- Dashboard shows memory spike
- Alert auto-resolves when memory drops < 80%

## Related Runbooks

- [High Error Rate](./high-error-rate.md): OOMKill causes errors
- [Slow Performance](./slow-performance.md): Memory pressure causes slowness (GC pauses)
- [Dependency Down](./dependency-down.md): OOMKill crashes containers

## Related Dashboards

- [Infrastructure](http://localhost:3001/d/infrastructure): Primary memory monitoring
- [API Performance](http://localhost:3001/d/api-performance): Performance impact of memory pressure
- [Error Monitoring](http://localhost:3001/d/meepleai-error-monitoring): Errors from OOMKill

## Changelog

- **2025-12-08**: Initial version (Issue #706)

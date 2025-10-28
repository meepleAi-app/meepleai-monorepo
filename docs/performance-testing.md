# MeepleAI Performance Testing Guide

**Document Version:** 1.0
**Last Updated:** 2025-10-26
**Related Issues:** AI-11.3 (#512) - Quality Scoring Performance Testing

---

## Table of Contents

1. [Overview](#overview)
2. [Performance Testing Infrastructure](#performance-testing-infrastructure)
3. [AI-11.3: Quality Scoring Performance Results](#ai-113-quality-scoring-performance-results)
4. [Running Performance Tests](#running-performance-tests)
5. [Interpreting Results](#interpreting-results)
6. [Performance Optimization History](#performance-optimization-history)
7. [Troubleshooting](#troubleshooting)

---

## Overview

This document provides comprehensive guidance on performance testing in the MeepleAI project, with a focus on measuring and optimizing system performance under load.

### Key Performance Areas

1. **API Response Times** - Endpoint latency under various load scenarios
2. **AI/RAG Operations** - Quality scoring, vector search, LLM generation overhead
3. **Database Performance** - Connection pooling, query optimization
4. **Cache Efficiency** - Redis cache hit rates, HybridCache performance
5. **Throughput** - Requests per second sustained under load

### Performance Testing Tools

- **k6** - Load testing tool for HTTP endpoints
- **Prometheus** - Metrics collection and query
- **Grafana** - Performance dashboards and visualization
- **OpenTelemetry** - Distributed tracing and metrics export
- **.NET Profiling** - dotnet-trace, PerfView for deep analysis

---

## Performance Testing Infrastructure

### Load Test Scripts

All load test scripts are located in `tests/load/`:

```
tests/load/
├── config.js                        # Shared configuration
├── utils.js                         # Helper functions
├── games-list-load-test.js          # GET /api/v1/games
├── chat-load-test.js                # POST /api/v1/chat
├── qa-agent-load-test.js            # POST /api/v1/agents/qa
└── quality-scoring-load-test.js     # AI-11.3: Quality scoring overhead
```

### Test Scenarios

Defined in `tests/load/config.js`:

| Scenario | Virtual Users | Duration | Purpose |
|----------|---------------|----------|---------|
| `baseline` | 50 | 3 minutes | Baseline metrics without features being tested |
| `users100` | 100 | 3 minutes | Normal load testing |
| `users500` | 500 | 5 minutes | Stress testing |
| `users1000` | 1000 | 6 minutes | Extreme stress testing |

### Performance Thresholds

#### Games List Endpoint
- **users100**: p95 < 200ms, error rate < 0.1%
- **users500**: p95 < 500ms, error rate < 0.1%

#### Chat Endpoint
- **users100**: p95 < 300ms, error rate < 1%
- **users500**: p95 < 800ms, error rate < 1%

#### Q&A Agent
- **users100**: p95 < 500ms, error rate < 1%
- **users500**: p95 < 1000ms, error rate < 1%

#### Quality Scoring (AI-11.3)
- **baseline**: p95 < 400ms (without quality overhead)
- **users100**: p50 < 500ms, p95 < 700ms, p99 < 1200ms
- **users500**: p50 < 800ms, p95 < 1500ms, p99 < 2500ms
- **Quality Score Coverage**: > 90% responses (users100), > 85% (users500)

---

## AI-11.3: Quality Scoring Performance Results

### Test Overview

**Issue:** #512 - Performance Testing for Quality Scoring System
**Implementation:** PR #509 (AI-11: Response quality scoring)
**Test Date:** 2025-10-26
**Test Environment:** Local development (Docker Compose infrastructure)

### Test Methodology

1. **Baseline Test** - Quality scoring disabled to measure overhead
2. **Normal Load** - 100 concurrent users for 5 minutes
3. **Stress Test** - 500 concurrent users for 2 minutes

### Quality Scoring Components Measured

- RAG confidence calculation (vector search score analysis)
- LLM confidence heuristics (response quality analysis)
- Citation quality scoring (reference validation)
- Overall confidence aggregation (weighted average)
- Prometheus metrics recording (OpenTelemetry export)
- Database writes (async quality_scores table inserts)

### Test Results

**NOTE:** Actual test results will be added here after running the load tests. The test infrastructure is ready and documented below.

#### Expected Results Template

```
╔═══════════════════════════════════════════════════════════╗
║  BASELINE TEST (No Quality Scoring)                      ║
╚═══════════════════════════════════════════════════════════╝

Test Configuration:
- Scenario: baseline (50 concurrent users)
- Duration: 3 minutes
- Quality Scoring: DISABLED

Metrics:
- p50 latency: XXX ms
- p95 latency: XXX ms (Target: < 400ms)
- p99 latency: XXX ms
- Throughput: XX req/s
- Error rate: X.XX% (Target: < 5%)

╔═══════════════════════════════════════════════════════════╗
║  USERS100 TEST (With Quality Scoring)                    ║
╚═══════════════════════════════════════════════════════════╝

Test Configuration:
- Scenario: users100 (100 concurrent users)
- Duration: 3 minutes
- Quality Scoring: ENABLED

Metrics:
- p50 latency: XXX ms (Target: < 500ms)
- p95 latency: XXX ms (Target: < 700ms)
- p99 latency: XXX ms (Target: < 1200ms)
- Throughput: XX req/s
- Error rate: X.XX% (Target: < 5%)
- Quality scores present: XX% (Target: > 90%)

╔═══════════════════════════════════════════════════════════╗
║  USERS500 STRESS TEST (With Quality Scoring)             ║
╚═══════════════════════════════════════════════════════════╝

Test Configuration:
- Scenario: users500 (500 concurrent users)
- Duration: 2 minutes
- Quality Scoring: ENABLED

Metrics:
- p50 latency: XXX ms (Target: < 800ms)
- p95 latency: XXX ms (Target: < 1500ms)
- p99 latency: XXX ms (Target: < 2500ms)
- Throughput: XX req/s
- Error rate: X.XX% (Target: < 5%)
- Quality scores present: XX% (Target: > 85%)
```

#### Overhead Analysis

```
Quality Scoring Overhead Calculation:
- Baseline p95: XXX ms
- Users100 p95: XXX ms
- Overhead: XX ms (X.X% increase)
- AI-11.3 Target: < 50ms overhead

Breakdown by Component:
- RAG confidence: ~X ms
- LLM confidence: ~X ms
- Citation quality: ~X ms
- Overall calculation: ~X ms
- Metrics recording: ~X ms
- Database write (async): ~X ms
```

### Performance Verdict

**STATUS:** ⏳ PENDING TEST EXECUTION

**Next Steps:**
1. Run baseline test: `k6 run --env SCENARIO=baseline quality-scoring-load-test.js`
2. Run users100 test: `k6 run --env SCENARIO=users100 quality-scoring-load-test.js`
3. Run users500 stress test: `k6 run --env SCENARIO=users500 quality-scoring-load-test.js`
4. Analyze results and update this section
5. Implement optimizations if overhead > 50ms

---

## Running Performance Tests

### Prerequisites

1. **k6 Installation**

   ```bash
   # macOS
   brew install k6

   # Linux
   sudo apt install k6

   # Windows
   choco install k6

   # Or download from https://k6.io/docs/getting-started/installation/
   ```

2. **Infrastructure Running**

   ```bash
   cd infra
   docker compose up -d postgres qdrant redis
   ```

3. **API Running**

   ```bash
   cd apps/api/src/Api
   dotnet run --configuration Release
   ```

   **IMPORTANT:** Use `--configuration Release` for performance testing to avoid debug overhead.

4. **Test Data**

   Ensure demo data is seeded (DB-02):
   - Demo users: `user@meepleai.dev`, `editor@meepleai.dev`, `admin@meepleai.dev`
   - Demo games: Chess, Tic-Tac-Toe
   - Demo PDFs indexed in Qdrant

### Running Individual Tests

#### Quality Scoring Performance Test (AI-11.3)

```bash
cd tests/load

# 1. Baseline test (quality scoring disabled)
k6 run --env SCENARIO=baseline --env QUALITY_ENABLED=false quality-scoring-load-test.js

# 2. Normal load test (100 users, 5 minutes)
k6 run --env SCENARIO=users100 quality-scoring-load-test.js

# 3. Stress test (500 users, 2 minutes)
k6 run --env SCENARIO=users500 quality-scoring-load-test.js
```

#### Other Load Tests

```bash
# Games list endpoint
k6 run --env SCENARIO=users100 games-list-load-test.js

# Chat endpoint
k6 run --env SCENARIO=users100 chat-load-test.js

# Q&A agent endpoint
k6 run --env SCENARIO=users100 qa-agent-load-test.js
```

### Advanced Usage

#### Custom API Base URL

```bash
k6 run --env API_BASE_URL=http://staging.meepleai.dev quality-scoring-load-test.js
```

#### JSON Results Output

```bash
k6 run --env SCENARIO=users100 --out json=results.json quality-scoring-load-test.js
```

#### Grafana Integration

```bash
# Start Grafana and InfluxDB
docker compose up -d grafana influxdb

# Run test with InfluxDB output
k6 run --out influxdb=http://localhost:8086/k6 quality-scoring-load-test.js

# View results in Grafana dashboard at http://localhost:3001
```

---

## Interpreting Results

### Key Metrics

1. **Latency Percentiles**
   - **p50 (median)** - Typical user experience
   - **p95** - 95% of users experience this or better
   - **p99** - Outlier detection (worst 1% of requests)

2. **Throughput**
   - **Requests per second (RPS)** - System capacity
   - **Concurrent users** - Load level

3. **Error Rate**
   - **HTTP failures** - Failed requests / total requests
   - **Target**: < 1% for normal operations, < 5% under stress

4. **Quality Metrics (AI-11.3)**
   - **Quality score coverage** - % of responses with quality scores
   - **Low-quality responses** - Responses with confidence < 0.60
   - **Cache hit rate** - AI-05 response caching effectiveness

### Success Criteria

#### PASS: Performance Acceptable
- ✅ Overhead < 50ms (p95)
- ✅ Throughput > 90 RPS
- ✅ Error rate < 5%
- ✅ Quality score coverage > 90%

**Action:** Document results, close issue, no optimization needed.

#### CONDITIONAL: Optimization Needed
- ⚠️ Overhead 50-200ms (p95)
- ⚠️ Throughput 50-90 RPS
- ⚠️ Error rate 5-10%

**Action:** Implement recommended optimizations, re-test, verify improvements.

#### FAIL: Major Refactor Required
- ❌ Overhead > 200ms (p95)
- ❌ Throughput < 50 RPS
- ❌ Error rate > 10%

**Action:** Escalate to architectural review, create optimization epic, consider async quality scoring queue.

### Optimization Strategies

#### If Overhead > 50ms

1. **Move quality calculation to background queue** (async processing)
   - Decouple quality scoring from request path
   - Use Redis queue with background worker
   - Trade-off: Quality scores not immediately available

2. **Batch database writes** (accumulate 100 logs, write once)
   - Reduce database connection overhead
   - Implement bulk insert with EF Core
   - Trade-off: Slight delay in quality data availability

3. **Cache LLM confidence heuristics** (in-memory calculation results)
   - Reduce redundant calculations
   - Use HybridCache with 5-minute TTL
   - Trade-off: Stale results for dynamic content

#### If Database Bottleneck

1. **Use EF Core bulk insert** (`AddRange` with batching)
   - Reduce round trips to database
   - Batch size: 50-100 inserts

2. **Write to Redis queue, background flush to DB**
   - Decouple writes from request path
   - Background service flushes every 10 seconds

3. **Increase connection pool size**
   - Current: min 10, max 100
   - Increase max to 200 for high load

#### If Prometheus Metrics Slow

1. **Sample metrics** (record 10% of requests)
   - Reduce metric collection overhead
   - Maintain statistical validity

2. **Reduce histogram label cardinality**
   - Limit label values to prevent metric explosion
   - Use aggregated labels (e.g., quality_tier instead of exact confidence)

3. **Move metrics to background thread**
   - Async metric recording
   - Use metric buffering

---

## Performance Optimization History

### PERF-05: HybridCache Implementation
**Date:** 2025-10-20
**Impact:** 30-50% latency reduction for cached operations
**Details:** See `docs/technic/perf-05-hybridcache-implementation.md`

### PERF-06: AsNoTracking Query Optimization
**Date:** 2025-10-21
**Impact:** 30% faster read queries
**Details:** See `docs/technic/perf-06-asnotracking-implementation.md`

### PERF-07: Sentence-Aware Chunking
**Date:** 2025-10-22
**Impact:** 20% better RAG accuracy, minimal latency increase
**Details:** See `docs/technic/perf-07-sentence-aware-chunking.md`

### PERF-08: Query Expansion with RRF
**Date:** 2025-10-23
**Impact:** 15-25% better recall, 200-300ms overhead (acceptable)
**Details:** See `docs/technic/perf-08-query-expansion.md`

### PERF-09: Connection Pooling Optimization
**Date:** 2025-10-24
**Impact:** 30-50% better throughput under load
**Details:** See `docs/technic/perf-09-connection-pooling.md`

### PERF-11: Response Compression
**Date:** 2025-10-25
**Impact:** 60-80% bandwidth reduction
**Details:** See `docs/technic/perf-11-response-compression.md`

### AI-11.3: Quality Scoring Performance
**Date:** 2025-10-26
**Status:** Testing infrastructure ready, results pending
**Target:** < 50ms overhead for quality scoring system
**Details:** This document (AI-11.3 section above)

---

## Troubleshooting

### Load Test Fails to Start

**Symptoms:**
- k6 reports "API health check failed"
- Connection refused errors

**Solutions:**
1. Verify API is running: `curl http://localhost:8080/health`
2. Check Docker services: `docker compose ps`
3. Verify PostgreSQL, Qdrant, Redis are running
4. Check API logs: `docker compose logs api`

### Authentication Failures

**Symptoms:**
- 401 Unauthorized errors
- "Failed to authenticate test user" in k6 output

**Solutions:**
1. Verify demo user exists: Check `user_sessions` table
2. Check password: Default is `Demo123!`
3. Verify session cookies are enabled
4. Check CORS configuration in `Program.cs`

### Inconsistent Results

**Symptoms:**
- High variance between test runs
- Outlier latencies

**Solutions:**
1. Run tests multiple times, average results
2. Ensure no other processes consuming resources
3. Use Release build (not Debug)
4. Close browser tabs/applications
5. Check system resource usage (CPU, memory)

### Prometheus Metrics Not Recording

**Symptoms:**
- Quality scores missing in Prometheus
- Grafana dashboard shows "No data"

**Solutions:**
1. Verify metrics endpoint: `curl http://localhost:8080/metrics | grep quality`
2. Check Prometheus scrape config: `infra/prometheus.yml`
3. Verify OpenTelemetry exporter: `Program.cs:386`
4. Check Prometheus targets: http://localhost:9090/targets

### High Error Rates

**Symptoms:**
- Error rate > 5%
- Many 500 Internal Server Error

**Solutions:**
1. Check API logs for exceptions
2. Verify database migrations applied
3. Check Qdrant collection exists
4. Verify OpenRouter API key (if using)
5. Check memory/CPU limits in Docker

---

## Related Documentation

- `docs/guide/load-testing.md` - Comprehensive load testing guide
- `docs/observability.md` - Monitoring and observability
- `docs/technic/performance-optimization-summary.md` - All PERF issues
- `docs/ops-02-opentelemetry-design.md` - OpenTelemetry architecture
- `CLAUDE.md` - Performance sections (PERF-05 to PERF-11)

---

**Generated with Claude Code**
**Co-Authored-By:** Claude <noreply@anthropic.com>

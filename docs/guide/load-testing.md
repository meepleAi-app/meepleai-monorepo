# Load Testing Guide

Comprehensive guide for running and interpreting load tests for the MeepleAI API.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Installation](#installation)
- [Running Tests](#running-tests)
- [Performance Targets](#performance-targets)
- [Test Scenarios](#test-scenarios)
- [Interpreting Results](#interpreting-results)
- [CI/CD Integration](#cicd-integration)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)
- [References](#references)

## Overview

The MeepleAI load testing framework uses **k6**, an open-source load testing tool, to validate API performance under various load conditions. The framework tests three critical endpoints:

1. **`GET /api/v1/games`**: Games listing (no authentication)
2. **`POST /api/v1/chat`**: Chat message creation (authenticated)
3. **`POST /api/v1/agents/qa`**: RAG-powered Q&A agent (authenticated)

### Why Load Testing?

- **Identify bottlenecks** before production deployment
- **Validate performance targets** (p95 latency, error rates)
- **Ensure scalability** under increasing user load
- **Detect regressions** in performance over time

## Architecture

### Load Test Components

```
tests/load/
├── config.js                    # Shared configuration
├── utils.js                     # Utility functions (auth, helpers)
├── games-list-load-test.js      # /api/v1/games test
├── chat-load-test.js            # /api/v1/chat test
├── qa-agent-load-test.js        # /api/v1/agents/qa test
└── README.md                    # Quick reference
```

### Test Flow

```mermaid
graph LR
    A[Setup] --> B[Authenticate]
    B --> C[Fetch Game IDs]
    C --> D[Main Test Loop]
    D --> E[Make HTTP Request]
    E --> F[Validate Response]
    F --> G[Check Thresholds]
    G --> H[Sleep/Think Time]
    H --> D
    D --> I[Teardown]
    I --> J[Generate Reports]
```json
### Dependencies

Each test relies on the following services:

| Service | Purpose | Health Check |
|---------|---------|--------------|
| **PostgreSQL** | User auth, chat persistence, game data | `pg_isready -U meepleai` |
| **Redis** | AI response caching (AI-05), sessions | `redis-cli ping` |
| **Qdrant** | Vector search for RAG | `curl http://localhost:6333/healthz` |
| **API** | MeepleAI backend | `curl http://localhost:8080/health/ready` |
| **OpenRouter** (optional) | LLM generation | API key required |

## Installation

### 1. Install k6

**Windows (Chocolatey)**:
```bash
choco install k6
```

**Windows (Scoop)**:
```bash
scoop install k6
```

**macOS (Homebrew)**:
```bash
brew install k6
```

**Linux (Debian/Ubuntu)**:
```bash
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg \
  --keyserver hkp://keyserver.ubuntu.com:80 \
  --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69

echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | \
  sudo tee /etc/apt/sources.list.d/k6.list

sudo apt-get update
sudo apt-get install k6
```

**Verify Installation**:
```bash
k6 version
# Output: k6 v0.53.0 (or later)
```

### 2. Start Infrastructure Services

```bash
cd infra
docker compose up -d postgres redis qdrant api
```

### 3. Verify Services

```bash
# Check API health
curl http://localhost:8080/health

# Check games endpoint
curl http://localhost:8080/api/v1/games
```

## Running Tests

### Basic Usage

Navigate to the load tests directory:
```bash
cd tests/load
```

Run a single test with default scenario (100 users):
```bash
k6 run games-list-load-test.js
```

### Specify Scenario

Run with specific load scenario:
```bash
# 100 concurrent users
k6 run --env SCENARIO=users100 chat-load-test.js

# 500 concurrent users
k6 run --env SCENARIO=users500 qa-agent-load-test.js

# 1000 concurrent users (stress test)
k6 run --env SCENARIO=users1000 games-list-load-test.js
```

### Custom API URL

Override the default API base URL:
```bash
k6 run --env API_BASE_URL=http://api.production.com qa-agent-load-test.js
```

### Generate Reports

Export results to JSON:
```bash
k6 run --out json=results.json --summary-export=summary.json games-list-load-test.js
```

View summary in terminal:
```bash
k6 run games-list-load-test.js | tee results.txt
```

### Run All Tests

Bash script to run all tests sequentially:
```bash
#!/bin/bash
SCENARIO=${1:-users100}

echo "Running load tests with scenario: $SCENARIO"

for test in games-list-load-test.js chat-load-test.js qa-agent-load-test.js; do
  echo "======================================"
  echo "Running: $test"
  echo "======================================"
  k6 run --env SCENARIO=$SCENARIO \
         --out json=${test%.js}-$SCENARIO.json \
         --summary-export=${test%.js}-$SCENARIO-summary.json \
         $test

  if [ $? -ne 0 ]; then
    echo "FAILED: $test"
    exit 1
  fi
done

echo "All tests passed!"
```

## Performance Targets

Performance targets are defined in the **TEST-04** issue and enforced via k6 thresholds.

### Target Response Times (p95)

| Endpoint | 100 Users | 500 Users | 1000 Users | Error Rate |
|----------|-----------|-----------|------------|------------|
| **`/api/v1/games`** | <200ms | <500ms | - | <0.1% |
| **`/api/v1/chat`** | <300ms | <800ms | - | <1% |
| **`/api/v1/agents/qa`** | <500ms | <1s | - | <1% |

### Why These Targets?

- **Games List** (<200ms): Simple database query, minimal processing
- **Chat** (<300ms): Database write + validation, moderate complexity
- **Q&A Agent** (<500ms): Involves vector search + LLM + caching, most complex

### Threshold Configuration

Thresholds are defined in `config.js`:
```javascript
thresholds: {
  qa: {
    users100: {
      http_req_duration: ['p(95)<500'], // 95th percentile <500ms
      http_req_failed: ['rate<0.01'],    // error rate <1%
    },
  },
}
```

## Test Scenarios

### Scenario: users100 (Baseline)

**Purpose**: Validate baseline performance with normal load

**Configuration**:
```javascript
{
  executor: 'ramping-vus',
  stages: [
    { duration: '30s', target: 100 },  // ramp up to 100 users
    { duration: '2m', target: 100 },   // maintain 100 users
    { duration: '30s', target: 0 },    // ramp down
  ],
}
```json
**Total Duration**: ~3 minutes
**Peak VUs**: 100
**Use Case**: Normal daily traffic

### Scenario: users500 (Average Load)

**Purpose**: Validate performance under expected peak load

**Configuration**:
```javascript
{
  executor: 'ramping-vus',
  stages: [
    { duration: '1m', target: 500 },   // ramp up to 500 users
    { duration: '3m', target: 500 },   // maintain 500 users
    { duration: '1m', target: 0 },     // ramp down
  ],
}
```json
**Total Duration**: ~5 minutes
**Peak VUs**: 500
**Use Case**: Expected peak traffic (e.g., product launch)

### Scenario: users1000 (Stress Test)

**Purpose**: Identify system breaking points and bottlenecks

**Configuration**:
```javascript
{
  executor: 'ramping-vus',
  stages: [
    { duration: '2m', target: 1000 },  // ramp up to 1000 users
    { duration: '3m', target: 1000 },  // maintain 1000 users
    { duration: '1m', target: 0 },     // ramp down
  ],
}
```json
**Total Duration**: ~6 minutes
**Peak VUs**: 1000
**Use Case**: Stress testing, capacity planning

## Interpreting Results

### Console Output

k6 provides real-time metrics during test execution:

```
     ✓ get_games: status is 200
     ✓ get_games: response time < 5s
     ✓ get_games: has games array

     checks.........................: 100.00% ✓ 2430      ✗ 0
     data_received..................: 1.2 MB  40 kB/s
     data_sent......................: 145 kB  4.8 kB/s
     http_req_duration..............: avg=124.5ms  p(95)=186.2ms  p(99)=248.3ms
       { expected_response:true }...: avg=124.5ms  p(95)=186.2ms  p(99)=248.3ms
     http_req_failed................: 0.00%   ✓ 0         ✗ 810
     http_reqs......................: 810     27/s
     iterations.....................: 810     27/s
     vus............................: 100     min=0       max=100
```json
### Key Metrics

#### http_req_duration

Response time distribution:
- **avg**: Average response time (all requests)
- **min**: Fastest response
- **med**: Median response time (50th percentile)
- **max**: Slowest response
- **p(90)**: 90th percentile (90% of requests faster than this)
- **p(95)**: 95th percentile (**threshold target**)
- **p(99)**: 99th percentile (outlier detection)

#### http_req_failed

Error rate:
- **rate**: Percentage of failed requests (non-2xx/3xx status)
- **passes**: Count of successful requests
- **fails**: Count of failed requests

#### checks

Validation pass rate:
- **rate**: Percentage of checks that passed
- **passes**: Count of successful checks
- **fails**: Count of failed checks

#### iterations

Test iterations:
- **count**: Total iterations completed
- **rate**: Iterations per second

#### vus (Virtual Users)

Concurrency:
- **value**: Current number of active VUs
- **min**: Minimum VUs during test
- **max**: Maximum VUs during test

### Threshold Evaluation

Thresholds determine test pass/fail status:

```
█ THRESHOLDS

  ✓ http_req_duration: p(95)<500
    ↳ p(95)=186.2ms

  ✓ http_req_failed: rate<0.01
    ↳ rate=0.00%
```

- **✓ (Green checkmark)**: Threshold passed
- **✗ (Red X)**: Threshold failed

**Exit Code**:
- `0`: All thresholds passed
- `99`: One or more thresholds failed

### JSON Report Analysis

Load test results are exported as JSON:

```bash
k6 run --out json=results.json --summary-export=summary.json qa-agent-load-test.js
```

**summary.json** contains aggregated metrics:
```json
{
  "metrics": {
    "http_req_duration": {
      "avg": 342.56,
      "min": 102.34,
      "med": 298.12,
      "max": 1024.67,
      "p(90)": 456.23,
      "p(95)": 524.89,
      "p(99)": 782.34
    },
    "http_req_failed": {
      "rate": 0.005,
      "passes": 1990,
      "fails": 10
    }
  }
}
```

**results.json** contains per-request data (useful for detailed analysis).

## CI/CD Integration

### GitHub Actions Workflow

Load tests run automatically via **`.github/workflows/load-test.yml`** with comprehensive performance optimizations (OPS-06).

**Trigger**: Manual workflow dispatch

**Inputs**:
- `scenario`: Choose `users100`, `users500`, or `users1000`

### Optimized Workflow Architecture

The workflow uses a **4-stage pipeline** with strategic caching to reduce execution time by **~10-15 minutes**:

```
┌────────────────────────────┐
│ 1. Setup Dependencies      │
│   ├─ Cache NuGet packages  │
│   └─ Cache k6 binary       │
└────────────────────────────┘
         ↓
┌────────────────────────────┐
│ 2. Build Infrastructure    │
│   ├─ Service containers    │
│   │   ├─ postgres          │
│   │   ├─ redis             │
│   │   └─ qdrant            │
│   ├─ Build API (Buildx)    │
│   └─ Upload API artifact   │
└────────────────────────────┘
         ↓
┌─────────────────────────────────┐
│ 3. Load Tests (Matrix)          │
│   ├─ games-list-load-test.js    │
│   ├─ chat-load-test.js          │
│   └─ qa-agent-load-test.js      │
│      │                           │
│      ├─ Restore k6 cache        │
│      ├─ Download API image      │
│      ├─ Service containers      │
│      └─ Run test                │
└─────────────────────────────────┘
         ↓
┌────────────────────────────┐
│ 4. Summary                 │
│   └─ Aggregate results     │
└────────────────────────────┘
```

### Workflow Optimizations

The optimized workflow implements **6 major performance improvements**:

| Optimization | Original | Optimized | Time Saved | Impact |
|--------------|----------|-----------|------------|---------|
| **NuGet Packages** | Downloaded every run | Cached with `actions/cache` | ~2-3 min | High |
| **Docker Build** | Full rebuild each time | Buildx with layer cache | ~5-7 min | Very High |
| **k6 Installation** | APT install each run | Binary cached | ~30s | Medium |
| **Infrastructure** | docker-compose | Service containers | ~1-2 min | Medium |
| **Setup Duplication** | 3x matrix jobs | Shared setup job | ~3-4 min | High |
| **API Image** | Built 3x (matrix) | Built once, artifact shared | ~10-12 min | Very High |

**Total Estimated Savings**: **~22-28 minutes per run**

### Performance Improvement Metrics

| Scenario | Original | Optimized (Cold Cache) | Optimized (Warm Cache) | Savings |
|----------|----------|------------------------|------------------------|---------|
| **First Run** | ~25-30 min | ~18-22 min | - | ~7-8 min |
| **Subsequent Runs** | ~25-30 min | - | ~8-12 min | **~17-18 min** |
| **Code-only Change** | ~25-30 min | - | ~10-14 min | ~15-16 min |
| **Dependency Change** | ~25-30 min | ~15-20 min | - | ~10 min |

For detailed information on workflow optimizations, see [Load Test Workflow Optimization](./load-test-workflow-optimization.md).

### Running Load Tests in CI

1. Navigate to **Actions** tab in GitHub
2. Select **Load Testing** workflow
3. Click **Run workflow**
4. Choose scenario (default: `users100`)
5. Monitor test execution
6. Download artifacts (JSON + HTML reports)

### Interpreting CI Results

**Success**: All thresholds passed, green checkmarks in workflow
**Failure**: One or more thresholds failed, red X in workflow

Check artifacts for detailed reports:
- `load-test-results-{test-file}-{scenario}.zip`

## Best Practices

### Before Running Load Tests

1. **Warm up caches**: Run a few manual requests to populate Redis cache
2. **Verify seed data**: Ensure demo games and users exist (DB-02 migration)
3. **Check service health**: All infrastructure services must be healthy
4. **Isolate environment**: Avoid running other heavy processes during tests

### During Load Tests

1. **Monitor system resources**: CPU, memory, network (use `htop`, `docker stats`)
2. **Watch logs**: Tail API logs for errors (`docker compose logs -f api`)
3. **Check database connections**: Monitor Postgres connection pool

### After Load Tests

1. **Analyze bottlenecks**: Identify slow endpoints, database queries, external API calls
2. **Review error logs**: Investigate failed requests
3. **Compare results**: Track performance trends over time
4. **Tune thresholds**: Adjust targets based on findings

### Optimization Strategies

If thresholds fail, consider:

1. **Database**:
   - Add indexes on frequently queried columns
   - Optimize slow queries (use `EXPLAIN ANALYZE`)
   - Increase connection pool size

2. **Caching**:
   - Leverage AI response caching (AI-05)
   - Implement query result caching
   - Use Redis for session storage

3. **API**:
   - Enable response compression (gzip)
   - Implement pagination for large datasets
   - Use asynchronous processing for heavy tasks

4. **Infrastructure**:
   - Scale horizontally (multiple API instances)
   - Increase resource allocation (CPU, memory)
   - Use CDN for static assets

## Troubleshooting

### Authentication Failures

**Symptom**: `auth: status is 200` check fails

**Solution**:
1. Verify demo user exists:
   ```bash
   docker compose exec postgres psql -U meepleai -d meepleai -c "SELECT email FROM users WHERE email='user@meepleai.dev';"
   ```

2. Check password hash is correct (DB-02 seed data)

3. Verify API authentication endpoint:
   ```bash
   curl -X POST http://localhost:8080/api/v1/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"user@meepleai.dev","password":"Demo123!"}'
   ```

### High Error Rates

**Symptom**: `http_req_failed: rate<0.01` threshold fails

**Solution**:
1. Check API logs for errors:
   ```bash
   docker compose logs api | grep ERROR
   ```

2. Verify all services are healthy:
   ```bash
   docker compose ps
   curl http://localhost:8080/health
   ```

3. Reduce load scenario (e.g., `users100` instead of `users500`)

### Timeout Issues

**Symptom**: Requests timing out (>30s)

**Solution**:
1. Increase timeout in test script:
   ```javascript
   const params = {
     headers: getAuthHeaders(sessionCookie),
     timeout: '60s', // Increase from default 30s
   };
   ```

2. Check for slow database queries:
   ```bash
   docker compose exec postgres psql -U meepleai -d meepleai -c "SELECT query, mean_exec_time FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10;"
   ```

3. Monitor Qdrant vector search performance:
   ```bash
   curl http://localhost:6333/collections/meepleai_documents
   ```

### Performance Degradation Over Time

**Symptom**: p95 latency increases as test progresses

**Solution**:
1. **Memory leaks**: Monitor API memory usage (`docker stats`)
2. **Connection pool exhaustion**: Increase Postgres/Redis pool size
3. **Disk I/O bottleneck**: Check disk usage and I/O wait
4. **Garbage collection**: Monitor .NET GC metrics

### CI Test Failures

**Symptom**: Tests pass locally but fail in CI

**Solution**:
1. **Resource constraints**: GitHub Actions runners have limited CPU/memory
2. **Network latency**: CI environment may have higher latency
3. **Service startup time**: Increase wait times for service health checks
4. **Concurrent jobs**: Ensure tests run in isolation

### Workflow Optimization Issues

**Symptom**: Cache not restoring or Docker build slow

**Solution**:
1. **Cache key mismatch**: Verify cache key generation in workflow
   ```bash
   # Check cache key format
   echo "Cache key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}"
   ```

2. **Docker Buildx cache corrupted**: Clear cache and force fresh build
   ```yaml
   # Update cache key to force rebuild
   key: ${{ runner.os }}-buildx-v2-${{ github.sha }}
   ```

3. **API image too large**: Optimize Docker multi-stage build
   - Review Dockerfile for unnecessary files
   - Use `.dockerignore` to exclude build artifacts

4. **Service health checks failing**: Increase retry count or interval
   ```yaml
   options: >-
     --health-cmd pg_isready
     --health-interval 5s
     --health-timeout 3s
     --health-retries 20  # Increased from 10
   ```

For detailed troubleshooting of workflow optimizations, see [Load Test Workflow Optimization](./load-test-workflow-optimization.md#troubleshooting).

## References

### k6 Documentation

- [k6 Official Docs](https://k6.io/docs/)
- [k6 Thresholds](https://k6.io/docs/using-k6/thresholds/)
- [k6 Scenarios](https://k6.io/docs/using-k6/scenarios/)
- [k6 Metrics](https://k6.io/docs/using-k6/metrics/)

### Related Documentation

- [Load Test Workflow Optimization (OPS-06)](./load-test-workflow-optimization.md)
- [Performance Tuning Guide](./performance-tuning.md)
- [Database Schema](./database-schema.md)
- [Observability Guide](./observability.md)
- [CI/CD Guide](./ci-cd.md)

### MeepleAI Components

- [RAG Evaluation (AI-06)](./ai-06-rag-evaluation.md)
- [Response Caching (AI-05)](./ai-05-response-caching.md)
- [Session Management (AUTH-03)](./auth-03-session-management.md)
- [OpenTelemetry (OPS-02)](./ops-02-opentelemetry-design.md)

### Issue Tracker

- [TEST-04: Load testing framework with k6](https://github.com/your-org/meepleai-monorepo/issues/426)
- [OPS-06: CI/CD pipeline optimization](https://github.com/your-org/meepleai-monorepo/issues/450)

---

**Last Updated**: 2025-10-17
**Version**: 1.1.0
**Author**: Claude Code (TEST-04 Implementation, OPS-06 Optimization)

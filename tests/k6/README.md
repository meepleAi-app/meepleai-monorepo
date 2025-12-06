# K6 Performance Test Suite

## Overview

Comprehensive k6 load testing suite for MeepleAI API endpoints. Tests critical paths including RAG search, chat messaging, game catalog, session management, database operations, Redis caching, and WebSocket connections.

## Prerequisites

1. **Install k6**:
   ```bash
   # macOS
   brew install k6

   # Linux
   sudo gpg -k
   sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
   echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
   sudo apt-get update
   sudo apt-get install k6

   # Windows
   choco install k6

   # Or download from: https://k6.io/docs/get-started/installation/
   ```

2. **Environment Setup**:
   ```bash
   cp config/env.example.json config/env.json
   # Edit config/env.json with your API base URL and credentials
   ```

## Quick Start

```bash
# Run all performance tests (smoke test mode)
npm run test:smoke

# Run specific endpoint tests
npm run test:rag-search
npm run test:chat
npm run test:games
npm run test:sessions

# Run full load test
npm run test:load

# Run stress test (find breaking point)
npm run test:stress

# Run spike test (sudden traffic surge)
npm run test:spike
```

## Test Scenarios

### 1. RAG Search (`scenarios/rag-search.js`)
- **Target**: < 2s p95 latency, 1000 req/s throughput
- **Endpoint**: `POST /api/v1/knowledge-base/search`
- **Load Profile**: Ramping VUs (10 → 100 → 10 over 5 minutes)

### 2. Chat Messaging (`scenarios/chat.js`)
- **Target**: < 1s p95 latency, 500 req/s throughput
- **Endpoint**: `POST /api/v1/agents/qa`
- **Load Profile**: Constant VUs (50) for 3 minutes

### 3. Game Search (`scenarios/games.js`)
- **Target**: < 500ms p95 latency, 2000 req/s throughput
- **Endpoint**: `GET /api/v1/games`
- **Load Profile**: High throughput (200 VUs) for 2 minutes

### 4. Session Updates (`scenarios/sessions.js`)
- **Target**: < 100ms p95 latency, 1000 req/s throughput
- **Endpoint**: `GET /api/v1/auth/sessions`
- **Load Profile**: Burst pattern (20 → 150 → 20 over 4 minutes)

### 5. Database Stress (`scenarios/database-stress.js`)
- **Target**: Concurrent query handling validation
- **Tests**: Complex joins, aggregations, concurrent writes

### 6. Redis Cache (`scenarios/redis-cache.js`)
- **Target**: Cache hit rates, eviction behavior
- **Tests**: Read/write patterns, TTL validation

### 7. WebSocket Load (`scenarios/websocket.js`)
- **Target**: 1000+ concurrent connections
- **Tests**: Connection stability, message throughput

### 8. HyperDX Load Testing (`hyperdx-ingestion-test.js`)
- **Purpose**: Comprehensive HyperDX performance validation (Issues #1565, #1568)
- **Scenarios**: 
  - Telemetry ingestion (100 users peak)
  - Business endpoints load (100 users sustained 3 min)
  - Stress test (1000 logs/sec for 1 min)
  - Spike test (bulk operations)
- **Endpoints**: `/api/v1/games`, `/api/v1/chat`, `/api/v1/search` + telemetry endpoints
- **Performance Targets**:
  - API response: P95 < 500ms
  - Log search: P95 < 1s
  - Trace query: P95 < 500ms
  - HyperDX resource: < 4GB RAM
  - ClickHouse storage: < 5GB
- **Duration**: ~17 minutes total
- **Run**: `k6 run hyperdx-ingestion-test.js`
- **Validation**: Follow teardown checklist for manual verification

## Test Types

### Smoke Test
Quick validation that endpoints work under minimal load.
```bash
npm run test:smoke
```
- Duration: 1 minute
- VUs: 1-5
- Purpose: Verify basic functionality

### Load Test
Sustained normal traffic to validate performance targets.
```bash
npm run test:load
```
- Duration: 10 minutes
- VUs: 50-200
- Purpose: Verify performance under expected load

### Stress Test
Push system beyond normal capacity to find breaking point.
```bash
npm run test:stress
```
- Duration: 15 minutes
- VUs: 200-1000
- Purpose: Identify capacity limits

### Spike Test
Sudden traffic surge to test auto-scaling and recovery.
```bash
npm run test:spike
```
- Duration: 10 minutes
- VUs: 10 → 500 (instant) → 10
- Purpose: Test resilience under sudden load

## Performance Targets

| Endpoint | P95 Latency | Throughput | Error Rate |
|----------|-------------|------------|------------|
| RAG Search | < 3000ms (CI/CD relaxed) | 1000 req/s | < 2% |
| Chat (QA) | < 1500ms (CI/CD relaxed) | 500 req/s | < 2% |
| Game Search | < 800ms (CI/CD relaxed) | 2000 req/s | < 2% |
| Sessions | < 200ms (CI/CD relaxed) | 1000 req/s | < 2% |

## Thresholds

**Updated (Issue #1629)**: Dynamic thresholds based on TEST_TYPE environment variable.

### Smoke Tests (Default for Nightly CI/CD)
Quick infrastructure validation without external services (Ollama, n8n):

```javascript
{
  'http_req_failed': ['rate<0.10'], // 10% tolerable (external services may fail)
  // rag_confidence: SKIPPED (requires Ollama embeddings)
  // ...other latency thresholds unchanged
}
```

### Load/Stress/Spike Tests (Strict Quality Gates)
Comprehensive testing with all external services:

```javascript
{
  'http_req_failed': ['rate<0.02'], // < 2% error rate (strict)
  'rag_confidence': ['avg>0.65'],   // 65% confidence (strict, requires Ollama)
  // ...other latency thresholds enforced
}
```

**Configuration**: Thresholds automatically adjust via `getThresholds(__ENV.TEST_TYPE)` in `config/thresholds.js`.

## Configuration

Edit `config/env.json`:

```json
{
  "apiBaseUrl": "http://localhost:8080",
  "testUser": {
    "email": "test@meepleai.dev",
    "password": "Test123!"
  },
  "testGameId": "your-game-uuid-here",
  "reportDir": "./reports"
}
```

## Reports

Test results are saved to `reports/` directory:

- `reports/summary.json` - Summary metrics
- `reports/detailed-{timestamp}.json` - Detailed run data
- `reports/html/{timestamp}.html` - HTML dashboard

Generate HTML report:
```bash
npm run report
```

## CI/CD Integration

**Updated (Issue #1629)**: Two-workflow strategy for different testing needs.

### Workflow 1: K6 Performance Tests (Smoke - Fast) ⚡
**File**: `.github/workflows/k6-performance.yml`

**Schedule**: Nightly at 2 AM UTC (automated on main branch)
**Services**: Postgres, Redis, Qdrant only
**Test Type**: Smoke (default), manual: smoke/load/stress/spike
**Duration**: ~2 minutes
**Thresholds**: Relaxed (10% error rate, no RAG confidence)

**Purpose**:
- ✅ Fast infrastructure validation
- ✅ Catch breaking changes early
- ✅ No external service dependencies (Ollama, n8n)
- ✅ Ideal for CI/CD fast feedback loop

**When to Use**:
- Nightly automated validation
- Quick pre-merge checks
- Infrastructure smoke tests

---

### Workflow 2: K6 Full Load Tests (Comprehensive) 🔴
**File**: `.github/workflows/k6-full-load.yml`

**Trigger**: Manual only (workflow_dispatch)
**Services**: Postgres, Redis, Qdrant, **Ollama**, **n8n**
**Test Type**: Load (default), manual: load/stress/spike
**Duration**: ~5-10 minutes (includes Ollama model download)
**Thresholds**: Strict (2% error rate, 65% RAG confidence)

**Purpose**:
- ✅ Comprehensive RAG testing with embeddings
- ✅ Workflow integration testing (n8n)
- ✅ Strict quality gates enforced
- ✅ Production-like environment

**When to Use**:
- Before major releases
- Capacity planning (stress tests)
- Pre-production validation
- RAG performance benchmarking

---

### Feature Comparison

| Feature | Smoke Workflow ⚡ | Full Load Workflow 🔴 |
|---------|-------------------|------------------------|
| **Ollama** | ❌ Not included | ✅ Included |
| **n8n** | ❌ Not included | ✅ Included |
| **RAG Confidence** | ⏭️ Skipped | ✅ Enforced (>65%) |
| **Error Rate** | 🟡 Relaxed (10%) | 🔴 Strict (2%) |
| **Duration** | ~2min | ~5-10min |
| **Schedule** | 🔄 Nightly automated | 🖱️ Manual only |
| **Use Case** | Infrastructure check | Comprehensive testing |

---

### Debug Features (Issue #1599, #1629)
Both workflows provide detailed debugging on failure:
- API logs output
- Health check status
- Running processes list
- Service connectivity verification
- Artifact uploads (reports, logs)

## Metrics Collected

- **HTTP Metrics**: Request duration, failure rate, throughput
- **Custom Metrics**:
  - `rag_confidence`: RAG response confidence scores
  - `cache_hit_rate`: Redis cache effectiveness
  - `db_query_time`: Database operation latency
  - `ws_message_rate`: WebSocket message throughput

## Troubleshooting

### "Connection refused"
- Ensure API is running: `curl http://localhost:8080/health`
- Check `config/env.json` has correct `apiBaseUrl`

### "Authentication failed"
- Verify test user credentials in `config/env.json`
- Create test user: `POST /api/v1/auth/register`

### "Thresholds exceeded"
- Review `reports/` for bottlenecks
- Check API logs for errors
- Verify database/Redis connectivity

## Architecture

```
tests/k6/
├── config/
│   ├── env.json              # Environment configuration
│   └── thresholds.js         # Performance thresholds
├── scenarios/
│   ├── all.js                # Run all scenarios
│   ├── rag-search.js         # RAG search tests
│   ├── chat.js               # Chat messaging tests
│   ├── games.js              # Game catalog tests
│   ├── sessions.js           # Session management tests
│   ├── database-stress.js    # Database load tests
│   ├── redis-cache.js        # Redis performance tests
│   └── websocket.js          # WebSocket load tests
├── utils/
│   ├── auth.js               # Authentication helpers
│   ├── common.js             # Common utilities
│   ├── metrics.js            # Custom metrics
│   └── generate-report.js    # HTML report generator
├── reports/                  # Generated reports (gitignored)
├── package.json
└── README.md
```

## Best Practices

1. **Run smoke tests first** to validate basic functionality
2. **Use realistic data** - vary queries, game IDs, etc.
3. **Monitor system resources** during tests (CPU, memory, disk)
4. **Ramp up gradually** - don't spike to max VUs immediately
5. **Set appropriate thresholds** - fail fast on violations
6. **Archive reports** - track performance trends over time

## Related Documentation

- [Performance Testing Guide](../../docs/02-development/testing/performance-testing-guide.md)
- [API Specification](../../docs/03-api/board-game-ai-api-specification.md)
- [CI/CD Pipeline](../../docs/03-deployment/ci-cd-pipeline.md)

---

**Issues**: #873 (Initial Implementation), #1599 (CI/CD Optimization), #1629 (Dynamic Thresholds & Hybrid Strategy)
**Maintained by**: Engineering Team
**Last Updated**: 2025-11-22
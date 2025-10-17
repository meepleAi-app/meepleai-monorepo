# Load Tests

This directory contains k6 load tests for the MeepleAI API.

## Overview

The load testing suite validates system performance under various load scenarios:
- **100 concurrent users**: Baseline performance
- **500 concurrent users**: Average expected load
- **1000 concurrent users**: Peak/stress load

## Test Files

- **`config.js`**: Shared configuration (base URL, thresholds, test data)
- **`utils.js`**: Utility functions (authentication, helpers)
- **`games-list-load-test.js`**: Tests `/api/v1/games` endpoint
- **`chat-load-test.js`**: Tests `/api/v1/chat` endpoint
- **`qa-agent-load-test.js`**: Tests `/api/v1/agents/qa` endpoint

## Prerequisites

1. **Install k6**:
   ```bash
   # Windows (Chocolatey)
   choco install k6

   # Windows (Scoop)
   scoop install k6

   # macOS
   brew install k6

   # Linux
   sudo gpg -k
   sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
   echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
   sudo apt-get update
   sudo apt-get install k6
   ```

2. **Start API services**:
   ```bash
   cd infra
   docker compose up -d postgres redis qdrant api
   ```

3. **Wait for services to be ready**:
   ```bash
   curl http://localhost:8080/health
   ```

## Running Tests Locally

### Basic Usage

```bash
cd tests/load

# Run with 100 concurrent users (default)
k6 run games-list-load-test.js

# Run with specific scenario
k6 run --env SCENARIO=users500 chat-load-test.js

# Run with custom API URL
k6 run --env API_BASE_URL=http://localhost:8080 qa-agent-load-test.js
```

### All Tests with Different Scenarios

```bash
# 100 concurrent users
k6 run --env SCENARIO=users100 games-list-load-test.js
k6 run --env SCENARIO=users100 chat-load-test.js
k6 run --env SCENARIO=users100 qa-agent-load-test.js

# 500 concurrent users
k6 run --env SCENARIO=users500 games-list-load-test.js
k6 run --env SCENARIO=users500 chat-load-test.js
k6 run --env SCENARIO=users500 qa-agent-load-test.js

# 1000 concurrent users (stress test)
k6 run --env SCENARIO=users1000 games-list-load-test.js
k6 run --env SCENARIO=users1000 chat-load-test.js
k6 run --env SCENARIO=users1000 qa-agent-load-test.js
```

### Generate JSON Reports

```bash
k6 run --out json=results.json --summary-export=summary.json games-list-load-test.js
```

## Performance Targets (TEST-04)

| Endpoint | 100 Users (p95) | 500 Users (p95) | Error Rate |
|----------|-----------------|-----------------|------------|
| `/api/v1/games` | <200ms | <500ms | <0.1% |
| `/api/v1/chat` | <300ms | <800ms | <1% |
| `/api/v1/agents/qa` | <500ms | <1s | <1% |

## CI/CD Integration

Load tests run via GitHub Actions workflow (`.github/workflows/load-test.yml`):

```bash
# Manual trigger via GitHub UI
# Go to Actions → Load Testing → Run workflow
# Select scenario: users100, users500, or users1000
```

## Interpreting Results

### Key Metrics

- **http_req_duration**: Response time distribution
  - `avg`: Average response time
  - `p(95)`: 95th percentile (target threshold)
  - `p(99)`: 99th percentile
- **http_req_failed**: Error rate percentage
- **checks**: Validation pass rate

### Threshold Failures

If thresholds fail, k6 exits with non-zero status:
```
✗ http_req_duration: p(95)<500
  ↳ p(95)=682.45ms
```

## Troubleshooting

### Authentication Failures

Ensure demo users exist (DB-02 seed data):
```bash
curl http://localhost:8080/api/v1/games
```

### High Error Rates

Check service logs:
```bash
docker compose -f infra/docker-compose.yml logs api
```

### Timeouts

Increase timeout for QA agent (involves LLM):
```javascript
const params = {
  headers: getAuthHeaders(sessionCookie),
  timeout: '60s', // Increase from default 30s
};
```

## Test Data

Tests use demo data from **DB-02 seed migration**:
- **Users**: `user@meepleai.dev` (password: `Demo123!`)
- **Games**: Chess, Tic-Tac-Toe
- **Queries**: Predefined questions in `config.js`

## Related Documentation

- [Load Testing Guide](../../docs/load-testing.md)
- [Performance Tuning](../../docs/performance-tuning.md)
- [CI/CD Guide](../../docs/ci-cd.md)
- [Issue #426 (TEST-04)](https://github.com/your-org/meepleai-monorepo/issues/426)

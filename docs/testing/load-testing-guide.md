# Load Testing Guide with k6

**Issue #2928** | **Last Updated**: 2026-02-01

## Overview

MeepleAI uses [k6](https://k6.io/) for load testing API endpoints. This guide covers setup, running tests, interpreting results, and CI integration.

## Quick Start

```bash
# Install k6
# Windows (Chocolatey)
choco install k6

# macOS (Homebrew)
brew install k6

# Linux (apt)
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update && sudo apt-get install k6

# Run smoke test
cd tests/k6
k6 run scenarios/user-dashboard-polling.js
```

## Test Scenarios

### 1. User Dashboard Polling (`user-dashboard-polling.js`)

**Purpose**: Simulates users with dashboard open, polling for updates every 30 seconds.

**Endpoints Tested**:
- `GET /api/v1/users/profile` - User profile data
- `GET /api/v1/notifications` - Notification list with pagination
- `GET /api/v1/users/me/activity` - Recent user activity
- `GET /api/v1/users/me/upload-quota` - Upload quota status
- `GET /api/v1/users/me/ai-usage` - AI usage statistics

**Test Modes**:
| Mode | VUs | Duration | Purpose |
|------|-----|----------|---------|
| Smoke | 5 | 1m | Quick validation |
| Load | 50 | 5m | Normal load simulation |
| Stress | 10→100→200→0 | 10m | Peak load testing |

```bash
# Run specific mode
k6 run --env TEST_TYPE=smoke scenarios/user-dashboard-polling.js
k6 run --env TEST_TYPE=load scenarios/user-dashboard-polling.js
k6 run --env TEST_TYPE=stress scenarios/user-dashboard-polling.js
```

### 2. Library Browsing (`library-browsing.js`)

**Purpose**: Simulates users browsing their game library with pagination and filtering.

**Endpoints Tested**:
- `GET /api/v1/library` - Paginated library list
- `GET /api/v1/library/stats` - Library statistics
- `GET /api/v1/library/quota` - Library quota
- `GET /api/v1/library/games/{id}` - Game detail view
- `GET /api/v1/library/games/{id}/status` - Game status

**Browsing Patterns**:
- **Quick Browse** (30%): Scan through multiple pages
- **Filtered Browse** (35%): Apply filters (favorites, state)
- **Detail View** (25%): Browse and view game details
- **Quota Check** (10%): Check library limits

```bash
k6 run scenarios/library-browsing.js
```

### 3. Catalog Search (`catalog-search.js`)

**Purpose**: Simulates users searching the shared game catalog with filters.

**Endpoints Tested**:
- `GET /api/v1/shared-games` - Paginated search
- `GET /api/v1/shared-games/{id}` - Game details
- `GET /api/v1/shared-games/stats` - Catalog statistics

**Search Patterns**:
- **Simple Search** (30%): Text search with pagination
- **Filtered Search** (25%): Multiple filters (players, time, complexity)
- **Browse and View** (25%): Browse catalog and view details
- **Discovery** (20%): Explore various categories
- **Anonymous Browse**: 30% of users browse without authentication

**Test Modes**:
| Mode | VUs | Duration | Purpose |
|------|-----|----------|---------|
| Smoke | 5 | 1m | Quick validation |
| Load | 75 | 5m | Normal catalog load |
| Stress | 75→150→300→0 | 10m | Peak search load |

```bash
k6 run scenarios/catalog-search.js
```

### 4. Admin Concurrent Actions (`admin-concurrent-actions.js`)

**Purpose**: Tests concurrent admin operations including read/write cycles.

**Endpoints Tested**:
- **Read Operations**:
  - `GET /api/v1/admin/llm/efficiency-report`
  - `GET /api/v1/admin/reports/system-health`
  - `GET /api/v1/admin/users`
  - `GET /api/v1/admin/audit-log`
- **Write Operations**:
  - `PUT /api/v1/admin/alert-configuration`
  - `PUT /api/v1/admin/feature-flags`
  - `POST /api/v1/admin/cache/invalidate`
  - `PUT /api/v1/admin/rate-limits/adjust`

**Action Patterns**:
- **Dashboard Refresh** (30%): Multiple concurrent reads
- **User Management** (25%): Read/write cycle
- **Configuration Update** (20%): Read-modify-write
- **Monitoring Review** (20%): Heavy read pattern
- **Rapid Config Changes** (5%): Stress test writes

**Test Modes**:
| Mode | VUs | Duration | Purpose |
|------|-----|----------|---------|
| Smoke | 3 | 1m | Quick validation |
| Load | 20 | 5m | Multiple concurrent admins |
| Stress | 0→50→0 | 10m | High concurrency |
| Concurrent Writes | 10×20 iter | 10m | Data integrity test |

```bash
k6 run scenarios/admin-concurrent-actions.js
k6 run --env TEST_TYPE=concurrent_writes scenarios/admin-concurrent-actions.js
```

### 5. Admin Polling (`admin-polling.js`)

**Purpose**: Tests admin dashboard with real-time updates (from Issue #2918).

**Endpoints Tested**:
- `GET /api/v1/admin/llm/efficiency-report`
- `GET /api/v1/admin/llm/monthly-report`
- `GET /api/v1/admin/reports`
- `GET /api/v1/admin/reports/system-health`
- `GET /api/v1/admin/ai-models`
- `GET /api/v1/admin/tier-routing`
- `GET /api/v1/admin/alert-configuration`

```bash
k6 run scenarios/admin-polling.js
```

## Configuration

### Environment Variables

```bash
# API Configuration
export API_BASE_URL="http://localhost:8080"

# Authentication
export TEST_USER_EMAIL="user@example.com"
export TEST_USER_PASSWORD="Password123!"
export TEST_ADMIN_EMAIL="admin@example.com"
export TEST_ADMIN_PASSWORD="AdminPassword123!"

# Test Options
export TEST_TYPE="load"  # smoke, load, stress
export HTTP_DEBUG="false"  # Enable HTTP debug output
```

### Shared Configuration (`utils/shared-config.js`)

All scenarios use shared utilities for consistency:

```javascript
import {
  config,
  authenticateUser,
  authenticateAdmin,
  authGet,
  authPost,
  validateResponse,
  standardThresholds,
  scenarioConfigs,
} from '../utils/shared-config.js';
```

**Key Components**:
- **config**: API base URL, credentials, timeouts
- **authenticateUser/Admin**: JWT token acquisition
- **authGet/authPost/authPut**: Authenticated HTTP helpers
- **validateResponse**: Response validation with checks
- **standardThresholds**: Common performance thresholds
- **scenarioConfigs**: Smoke/load/stress configurations

## Performance Thresholds

### Standard Thresholds (All Scenarios)

```javascript
thresholds: {
  'http_req_duration': ['p(95)<500', 'p(99)<1000'],
  'http_req_failed': ['rate<0.01'],
  'http_reqs': ['rate>10'],
  'checks': ['rate>0.99'],
}
```

### Scenario-Specific Thresholds

| Scenario | Metric | p95 | p99 | Error Rate |
|----------|--------|-----|-----|------------|
| Dashboard Polling | Response Time | <500ms | <1000ms | <1% |
| Library Browsing | Response Time | <500ms | <1000ms | <1% |
| Catalog Search | Search Latency | <500ms | <1000ms | <1% |
| Admin Reads | Response Time | <500ms | <1000ms | <1% |
| Admin Writes | Response Time | <750ms | <1500ms | <1% |

## Running Tests

### Local Development

```bash
cd tests/k6

# Run single scenario
k6 run scenarios/user-dashboard-polling.js

# Run with custom API URL
k6 run --env API_BASE_URL=http://localhost:5000 scenarios/library-browsing.js

# Run with output to file
k6 run --out json=results.json scenarios/catalog-search.js

# Run with detailed HTTP debugging
k6 run --env HTTP_DEBUG=true scenarios/admin-polling.js
```

### CI Integration

Tests run automatically via GitHub Actions on:
- Manual trigger with scenario selection
- Scheduled performance testing

```yaml
# .github/workflows/test-performance.yml
name: Performance Tests (k6)

on:
  workflow_dispatch:
    inputs:
      k6_scenario:
        description: 'K6 scenario to run'
        required: true
        default: 'all'
        type: choice
        options:
          - all
          - admin-polling
          - user-dashboard
          - library-browsing
          - catalog-search
          - admin-concurrent
```

**Running from GitHub Actions**:
1. Go to Actions → "Performance Tests (k6)"
2. Click "Run workflow"
3. Select scenario (or "all")
4. Click "Run workflow"

### Docker-Based Testing

```bash
# Run k6 in Docker
docker run --rm -v $(pwd)/tests/k6:/scripts \
  -e API_BASE_URL=http://host.docker.internal:8080 \
  grafana/k6 run /scripts/scenarios/user-dashboard-polling.js
```

## Interpreting Results

### Console Output

```
          /\      |‾‾| /‾‾/   /‾‾/
     /\  /  \     |  |/  /   /  /
    /  \/    \    |     (   /   ‾‾\
   /          \   |  |\  \ |  (‾)  |
  / __________ \  |__| \__\ \_____/ .io

  execution: local
     script: scenarios/user-dashboard-polling.js
     output: -

  scenarios: (100.00%) 1 scenario, 50 max VUs, 5m30s max duration
           * load: 50 looping VUs for 5m0s

     ✓ profile: status OK
     ✓ profile: has data
     ✓ notifications: status OK

     checks.........................: 99.23% ✓ 12456   ✗ 97
     data_received..................: 45 MB  150 kB/s
     data_sent......................: 12 MB  40 kB/s
     http_req_duration..............: avg=123ms min=45ms med=98ms max=1.2s p(95)=245ms p(99)=456ms
     http_reqs......................: 25000  83.33/s
     iteration_duration.............: avg=2.3s  min=1.2s med=2.1s max=8.5s p(95)=3.2s  p(99)=5.1s
     vus............................: 50     min=50    max=50
     vus_max........................: 50     min=50    max=50
```

### Key Metrics

| Metric | Description | Target |
|--------|-------------|--------|
| `http_req_duration` | Request response time | p95 < 500ms |
| `http_req_failed` | Failed request rate | < 1% |
| `http_reqs` | Request throughput | > 10 req/s |
| `checks` | Validation pass rate | > 99% |
| `iteration_duration` | Full scenario cycle time | Varies |

### JSON Output Analysis

```bash
# Generate JSON output
k6 run --out json=results.json scenarios/user-dashboard-polling.js

# Analyze with jq
cat results.json | jq 'select(.type=="Point" and .metric=="http_req_duration") | .data.value' | \
  awk '{sum+=$1; count++} END {print "Avg:", sum/count, "ms"}'
```

### Grafana Dashboard

The k6 results dashboard is available at `infra/monitoring/grafana/dashboards/k6-load-testing.json`.

**Panels**:
- **Response Time (p95/p99)**: Percentile response times
- **Error Rate**: Failed request percentage
- **Throughput**: Requests per second
- **Response Time Distribution**: Histogram of response times
- **Scenario Comparison**: Response times by scenario
- **Virtual Users**: Active VU count over time
- **Iterations**: Completed iterations

**Setup**:
1. Import dashboard JSON into Grafana
2. Configure Prometheus data source
3. Enable k6 Prometheus output: `k6 run --out prometheus scenarios/test.js`

## Writing Custom Scenarios

### Basic Structure

```javascript
import http from 'k6/http';
import { sleep, check } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import {
  config,
  authenticateUser,
  authGet,
  standardThresholds,
  standardSetup,
  standardTeardown,
} from '../utils/shared-config.js';

// Custom metrics
const myLatency = new Trend('my_endpoint_latency');

export const options = {
  scenarios: {
    smoke: {
      executor: 'constant-vus',
      vus: 5,
      duration: '1m',
      exec: 'myScenario',
    },
  },
  thresholds: {
    ...standardThresholds,
    'my_endpoint_latency': ['p(95)<500'],
  },
};

export function myScenario() {
  const token = authenticateUser();
  if (!token) return;

  const startTime = Date.now();
  const response = authGet(token, '/api/v1/my-endpoint');
  myLatency.add(Date.now() - startTime);

  check(response, {
    'status OK': (r) => r.status === 200,
    'has data': (r) => r.json().data !== undefined,
  });

  sleep(1);
}

export function setup() {
  return standardSetup('My Scenario', options);
}

export function teardown(data) {
  standardTeardown();
}
```

### Best Practices

1. **Use Shared Config**: Import from `utils/shared-config.js` for consistency
2. **Custom Metrics**: Define scenario-specific metrics for detailed analysis
3. **Realistic Patterns**: Model actual user behavior with weighted actions
4. **Proper Authentication**: Use `authenticateUser/Admin` for authenticated endpoints
5. **Sleep Between Actions**: Include realistic think time between requests
6. **Validation Checks**: Always validate response status and structure
7. **Error Tracking**: Use counters for error categorization

## Troubleshooting

### Common Issues

**Authentication Failures**:
```bash
# Check credentials
export TEST_USER_EMAIL="valid@email.com"
export TEST_USER_PASSWORD="ValidPassword123!"

# Verify API is running
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"Password123!"}'
```

**Connection Refused**:
```bash
# Check API is running
curl http://localhost:8080/health

# Check Docker network (if using Docker)
docker network inspect meepleai-network
```

**Threshold Failures**:
```
✗ http_req_duration.............: avg=856ms min=123ms med=678ms max=5.2s p(95)=1.8s p(99)=3.2s
  ✗ p(95)<500
```
- Check API performance bottlenecks
- Verify database indexes
- Review endpoint implementation
- Consider increasing resources

**High Error Rate**:
```
checks.........................: 85.23% ✓ 8523    ✗ 1477
```
- Check API logs for errors
- Verify endpoint availability
- Review rate limiting configuration
- Check database connection pool

### Debug Mode

```bash
# Enable verbose HTTP logging
k6 run --env HTTP_DEBUG=true scenarios/user-dashboard-polling.js

# Reduce VUs for debugging
k6 run --vus 1 --iterations 1 scenarios/library-browsing.js
```

## Performance Baselines

See [Performance Baselines](./performance-baselines.md) for established baseline metrics and targets.

## See Also

- [Performance Benchmarks](./performance-benchmarks.md) - Test suite performance
- [CI/CD Pipeline](./ci-cd-pipeline.md) - CI integration details
- [Grafana Dashboards](../../infra/monitoring/grafana/dashboards/) - Monitoring setup
- [k6 Documentation](https://k6.io/docs/) - Official k6 documentation

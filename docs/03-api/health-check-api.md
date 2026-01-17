# Health Check API Reference

> **Last Updated**: 2026-01-17
> **Related**: [Health Check System Overview](../04-deployment/health-checks.md)
> **Issue**: [#2511](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2511)

## Endpoint Overview

MeepleAI provides comprehensive health check endpoints for monitoring service availability and diagnosing issues.

**Endpoints**:
- `/api/v1/health` - Comprehensive health check (all services)
- `/health/ready` - Readiness probe (critical services only)
- `/health/live` - Liveness probe (application process)

---

## `/api/v1/health` - Comprehensive Health Check

### Request

**Method**: `GET`
**Authentication**: None (public endpoint)
**URL**: `http://localhost:8080/api/v1/health`

**Query Parameters**: None

**Headers**: None required

### Response

**Status Codes**:
- `200 OK` - Health check completed (check `overallStatus` for actual health)
- `503 Service Unavailable` - Health check system failure (rare)

**Content-Type**: `application/json`

#### Response Schema

```json
{
  "overallStatus": "Healthy",
  "checks": [
    {
      "serviceName": "postgres",
      "status": "Healthy",
      "description": "Connected to PostgreSQL",
      "isCritical": true,
      "timestamp": "2026-01-17T10:00:00Z"
    }
  ],
  "timestamp": "2026-01-17T10:00:00Z"
}
```

**Fields**:

| Field | Type | Description |
|-------|------|-------------|
| `overallStatus` | string | Overall system health: `Healthy`, `Degraded`, `Unhealthy` |
| `checks` | array | Individual service health checks |
| `checks[].serviceName` | string | Service identifier (lowercase, hyphenated) |
| `checks[].status` | string | Service status: `Healthy`, `Degraded`, `Unhealthy` |
| `checks[].description` | string | Human-readable status message |
| `checks[].isCritical` | boolean | Whether service is critical for core functionality |
| `checks[].timestamp` | string | ISO 8601 timestamp of check execution |
| `timestamp` | string | ISO 8601 timestamp of overall health check |

#### Overall Status Logic

| Overall Status | Condition |
|----------------|-----------|
| `Healthy` | All checks return `Healthy` |
| `Degraded` | At least one non-critical service is `Degraded` or `Unhealthy` |
| `Unhealthy` | At least one critical service is `Unhealthy` |

---

## Response Examples

### Example 1: All Services Healthy

**Request**:
```bash
curl http://localhost:8080/api/v1/health
```

**Response** (`200 OK`):
```json
{
  "overallStatus": "Healthy",
  "checks": [
    {
      "serviceName": "postgres",
      "status": "Healthy",
      "description": "Connected to PostgreSQL",
      "isCritical": true,
      "timestamp": "2026-01-17T10:00:00.123Z"
    },
    {
      "serviceName": "redis",
      "status": "Healthy",
      "description": "Connected to Redis",
      "isCritical": true,
      "timestamp": "2026-01-17T10:00:00.456Z"
    },
    {
      "serviceName": "qdrant",
      "status": "Healthy",
      "description": "Connected to Qdrant vector database",
      "isCritical": true,
      "timestamp": "2026-01-17T10:00:00.789Z"
    },
    {
      "serviceName": "embedding",
      "status": "Healthy",
      "description": "Embedding service ready (http://embedding-service:8000)",
      "isCritical": true,
      "timestamp": "2026-01-17T10:00:01.012Z"
    },
    {
      "serviceName": "openrouter",
      "status": "Healthy",
      "description": "OpenRouter API accessible",
      "isCritical": false,
      "timestamp": "2026-01-17T10:00:01.345Z"
    },
    {
      "serviceName": "bgg-api",
      "status": "Healthy",
      "description": "BoardGameGeek API accessible",
      "isCritical": false,
      "timestamp": "2026-01-17T10:00:01.678Z"
    }
  ],
  "timestamp": "2026-01-17T10:00:01.678Z"
}
```

### Example 2: Degraded (Non-Critical Service Down)

**Request**:
```bash
curl http://localhost:8080/api/v1/health
```

**Response** (`200 OK`):
```json
{
  "overallStatus": "Degraded",
  "checks": [
    {
      "serviceName": "postgres",
      "status": "Healthy",
      "description": "Connected to PostgreSQL",
      "isCritical": true,
      "timestamp": "2026-01-17T10:05:00Z"
    },
    {
      "serviceName": "redis",
      "status": "Healthy",
      "description": "Connected to Redis",
      "isCritical": true,
      "timestamp": "2026-01-17T10:05:00Z"
    },
    {
      "serviceName": "qdrant",
      "status": "Healthy",
      "description": "Connected to Qdrant vector database",
      "isCritical": true,
      "timestamp": "2026-01-17T10:05:00Z"
    },
    {
      "serviceName": "embedding",
      "status": "Healthy",
      "description": "Embedding service ready",
      "isCritical": true,
      "timestamp": "2026-01-17T10:05:00Z"
    },
    {
      "serviceName": "openrouter",
      "status": "Degraded",
      "description": "OpenRouter API key missing or invalid (using Ollama fallback)",
      "isCritical": false,
      "timestamp": "2026-01-17T10:05:00Z"
    },
    {
      "serviceName": "bgg-api",
      "status": "Unhealthy",
      "description": "BoardGameGeek API timeout (503 Service Unavailable)",
      "isCritical": false,
      "timestamp": "2026-01-17T10:05:00Z"
    }
  ],
  "timestamp": "2026-01-17T10:05:00Z"
}
```

**Interpretation**: Application is functional but with reduced features (no OpenRouter, BGG sync paused).

### Example 3: Unhealthy (Critical Service Down)

**Request**:
```bash
curl http://localhost:8080/api/v1/health
```

**Response** (`200 OK`):
```json
{
  "overallStatus": "Unhealthy",
  "checks": [
    {
      "serviceName": "postgres",
      "status": "Unhealthy",
      "description": "Failed to connect: Connection refused (localhost:5432)",
      "isCritical": true,
      "timestamp": "2026-01-17T10:10:00Z"
    },
    {
      "serviceName": "redis",
      "status": "Healthy",
      "description": "Connected to Redis",
      "isCritical": true,
      "timestamp": "2026-01-17T10:10:00Z"
    },
    {
      "serviceName": "qdrant",
      "status": "Healthy",
      "description": "Connected to Qdrant vector database",
      "isCritical": true,
      "timestamp": "2026-01-17T10:10:00Z"
    },
    {
      "serviceName": "embedding",
      "status": "Healthy",
      "description": "Embedding service ready",
      "isCritical": true,
      "timestamp": "2026-01-17T10:10:00Z"
    }
  ],
  "timestamp": "2026-01-17T10:10:00Z"
}
```

**Interpretation**: Core functionality broken (database unavailable). Immediate action required.

---

## Service Check Details

### Core Infrastructure Checks

#### PostgreSQL (`postgres`)

**Check Type**: Database connectivity with query execution
**Criticality**: 🔴 CRITICAL
**Timeout**: 5 seconds

**Healthy Criteria**:
- Connection established successfully
- `SELECT 1` query returns successfully
- Response time <100ms

**Example Responses**:
```json
{
  "serviceName": "postgres",
  "status": "Healthy",
  "description": "Connected to PostgreSQL",
  "isCritical": true
}
```

```json
{
  "serviceName": "postgres",
  "status": "Degraded",
  "description": "PostgreSQL responding slowly (response time: 4.2s)",
  "isCritical": true
}
```

```json
{
  "serviceName": "postgres",
  "status": "Unhealthy",
  "description": "Failed to connect: password authentication failed",
  "isCritical": true
}
```

#### Redis (`redis`)

**Check Type**: Cache connectivity with PING command
**Criticality**: 🔴 CRITICAL
**Timeout**: 5 seconds

**Healthy Criteria**:
- Connection established successfully
- `PING` command returns `PONG`
- Response time <50ms

**Example Responses**:
```json
{
  "serviceName": "redis",
  "status": "Healthy",
  "description": "Connected to Redis",
  "isCritical": true
}
```

```json
{
  "serviceName": "redis",
  "status": "Unhealthy",
  "description": "Failed to connect: Connection refused (localhost:6379)",
  "isCritical": true
}
```

#### Qdrant (`qdrant`)

**Check Type**: HTTP health endpoint
**Criticality**: 🔴 CRITICAL
**Timeout**: 5 seconds
**Endpoint**: `GET http://qdrant:6333/healthz`

**Healthy Criteria**:
- HTTP 200 response
- Response body: `{"status":"ok"}`
- Response time <100ms

**Example Responses**:
```json
{
  "serviceName": "qdrant",
  "status": "Healthy",
  "description": "Connected to Qdrant vector database",
  "isCritical": true
}
```

```json
{
  "serviceName": "qdrant",
  "status": "Unhealthy",
  "description": "Qdrant healthz endpoint returned 503 Service Unavailable",
  "isCritical": true
}
```

### AI Service Checks

#### Embedding Service (`embedding`)

**Check Type**: HTTP health endpoint
**Criticality**: 🔴 CRITICAL
**Timeout**: 5 seconds
**Endpoint**: `GET http://embedding-service:8000/health`

**Healthy Criteria**:
- HTTP 200 response
- Response body: `{"status":"healthy","model":"loaded"}`
- Response time <200ms

**Example Responses**:
```json
{
  "serviceName": "embedding",
  "status": "Healthy",
  "description": "Embedding service ready (http://embedding-service:8000)",
  "isCritical": true
}
```

```json
{
  "serviceName": "embedding",
  "status": "Degraded",
  "description": "Embedding service slow (response time: 3.5s)",
  "isCritical": true
}
```

```json
{
  "serviceName": "embedding",
  "status": "Unhealthy",
  "description": "Embedding service unavailable: Connection timeout",
  "isCritical": true
}
```

#### OpenRouter (`openrouter`)

**Check Type**: HTTP connectivity to `/api/v1/models`
**Criticality**: 🟡 IMPORTANT (has local fallback)
**Timeout**: 5 seconds
**Endpoint**: `GET https://openrouter.ai/api/v1/models`

**Healthy Criteria**:
- HTTP 200 response
- Valid API key configured
- Models list returned

**Example Responses**:
```json
{
  "serviceName": "openrouter",
  "status": "Healthy",
  "description": "OpenRouter API accessible",
  "isCritical": false
}
```

```json
{
  "serviceName": "openrouter",
  "status": "Degraded",
  "description": "OpenRouter API key missing (using Ollama fallback)",
  "isCritical": false
}
```

```json
{
  "serviceName": "openrouter",
  "status": "Unhealthy",
  "description": "OpenRouter API returned 401 Unauthorized (invalid API key)",
  "isCritical": false
}
```

#### Reranker Service (`reranker`)

**Check Type**: HTTP health endpoint
**Criticality**: 🟢 OPTIONAL
**Timeout**: 5 seconds
**Endpoint**: `GET http://reranker-service:8000/health`

**Example Responses**:
```json
{
  "serviceName": "reranker",
  "status": "Healthy",
  "description": "Reranker service ready",
  "isCritical": false
}
```

```json
{
  "serviceName": "reranker",
  "status": "Degraded",
  "description": "Reranker API key missing (reranking step skipped)",
  "isCritical": false
}
```

### External API Checks

#### BoardGameGeek API (`bgg-api`)

**Check Type**: HTTP connectivity
**Criticality**: 🟡 IMPORTANT
**Timeout**: 10 seconds (external API)
**Endpoint**: `GET https://boardgamegeek.com/xmlapi2/thing?id=1`

**Example Responses**:
```json
{
  "serviceName": "bgg-api",
  "status": "Healthy",
  "description": "BoardGameGeek API accessible",
  "isCritical": false
}
```

```json
{
  "serviceName": "bgg-api",
  "status": "Degraded",
  "description": "BGG credentials not configured (catalog sync disabled)",
  "isCritical": false
}
```

```json
{
  "serviceName": "bgg-api",
  "status": "Unhealthy",
  "description": "BGG API timeout after 10s (503 Service Unavailable)",
  "isCritical": false
}
```

---

## `/health/ready` - Readiness Probe

### Request

**Method**: `GET`
**URL**: `http://localhost:8080/health/ready`

**Purpose**: Kubernetes readiness probe - indicates if application can accept traffic.

### Response

**Status Codes**:
- `200 OK` - Application ready to accept traffic
- `503 Service Unavailable` - Application not ready (critical service down)

**Content-Type**: `text/plain`

**Response Body**:
```
Healthy
```

or

```
Unhealthy
```

**Check Criteria**: Only critical services checked (postgres, redis, qdrant, embedding).

### Example Usage (Kubernetes)

```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5
  timeoutSeconds: 3
  failureThreshold: 3
```

---

## `/health/live` - Liveness Probe

### Request

**Method**: `GET`
**URL**: `http://localhost:8080/health/live`

**Purpose**: Kubernetes liveness probe - indicates if application process is running.

### Response

**Status Codes**:
- `200 OK` - Application process alive
- No response - Application crashed (Kubernetes will restart)

**Content-Type**: `text/plain`

**Response Body**:
```
Healthy
```

**Check Criteria**: Only verifies application process is running (no external dependencies).

### Example Usage (Kubernetes)

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3
```

---

## Service List

### Complete Service Inventory

| Service | Criticality | Check Type | Timeout | Endpoint/Method |
|---------|-------------|------------|---------|-----------------|
| **postgres** | 🔴 CRITICAL | Database | 5s | `SELECT 1` query |
| **redis** | 🔴 CRITICAL | Cache | 5s | `PING` command |
| **qdrant** | 🔴 CRITICAL | HTTP | 5s | `GET /healthz` |
| **embedding** | 🔴 CRITICAL | HTTP | 5s | `GET /health` |
| **openrouter** | 🟡 IMPORTANT | HTTP | 5s | `GET /api/v1/models` |
| **bgg-api** | 🟡 IMPORTANT | HTTP | 10s | `GET /xmlapi2/thing?id=1` |
| **unstructured** | 🟡 IMPORTANT | HTTP | 5s | `GET /health` |
| **reranker** | 🟢 OPTIONAL | HTTP | 5s | `GET /health` |
| **smoldocling** | 🟢 OPTIONAL | HTTP | 5s | `GET /health` |
| **email-smtp** | 🟢 OPTIONAL | Config | 0s | Configuration check |
| **oauth-google** | 🟢 OPTIONAL | Config | 0s | Configuration check |
| **oauth-discord** | 🟢 OPTIONAL | Config | 0s | Configuration check |
| **grafana** | 🟢 OPTIONAL | HTTP | 5s | `GET /api/health` |
| **prometheus** | 🟢 OPTIONAL | HTTP | 5s | `GET /-/healthy` |
| **hyperdx** | 🟢 OPTIONAL | Config | 0s | Configuration check |

---

## Integration Examples

### Monitoring with Prometheus

**Scrape Configuration** (`prometheus.yml`):
```yaml
scrape_configs:
  - job_name: 'meepleai-health'
    metrics_path: '/api/v1/health'
    scheme: http
    static_configs:
      - targets: ['api:8080']
    metric_relabel_configs:
      - source_labels: [serviceName]
        target_label: service
      - source_labels: [status]
        target_label: health_status
```

**Alert Rules** (`alerts.yml`):
```yaml
groups:
  - name: health_checks
    interval: 30s
    rules:
      - alert: CriticalServiceDown
        expr: health_check_status{is_critical="true"} == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Critical service {{ $labels.service }} is unhealthy"
          description: "Service {{ $labels.service }} has been unhealthy for 2 minutes"

      - alert: DegradedStatus
        expr: health_check_status{is_critical="false"} == 0
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Non-critical service {{ $labels.service }} degraded"
```

### Dashboard Integration (Grafana)

**Panel Query** (Service Status Table):
```promql
# Show all service statuses
health_check_status
```

**Panel Query** (Overall Health Gauge):
```promql
# 2 = Healthy, 1 = Degraded, 0 = Unhealthy
(
  count(health_check_status{is_critical="true"} == 2) == bool count(health_check_status{is_critical="true"})
) * 2 or (
  count(health_check_status{is_critical="true"} == 0) > bool 0
) * 0 or 1
```

### Load Balancer Integration

**Nginx Health Check**:
```nginx
upstream meepleai_backend {
    server api1:8080 max_fails=3 fail_timeout=30s;
    server api2:8080 max_fails=3 fail_timeout=30s;
}

server {
    location /api {
        proxy_pass http://meepleai_backend;

        # Health check
        health_check uri=/health/ready interval=10s fails=3 passes=2;
    }
}
```

**HAProxy Health Check**:
```haproxy
backend meepleai_api
    option httpchk GET /health/ready
    http-check expect status 200
    server api1 10.0.0.1:8080 check inter 10s
    server api2 10.0.0.2:8080 check inter 10s
```

### CI/CD Validation

**GitHub Actions Workflow**:
```yaml
name: Health Check Validation

on:
  push:
    branches: [main, develop]

jobs:
  health-check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Start services
        run: |
          cd infra/secrets
          ./setup-secrets.ps1
          cd ../../infra
          docker compose up -d

      - name: Wait for startup
        run: sleep 30

      - name: Check health endpoint
        run: |
          RESPONSE=$(curl -s http://localhost:8080/api/v1/health)
          STATUS=$(echo $RESPONSE | jq -r '.overallStatus')

          if [[ "$STATUS" == "Unhealthy" ]]; then
            echo "❌ Health check failed!"
            echo "$RESPONSE" | jq
            exit 1
          fi

          echo "✅ Health check passed: $STATUS"
```

---

## Error Codes and Troubleshooting

### HTTP Status Codes

| Status Code | Meaning | Action |
|-------------|---------|--------|
| `200 OK` | Health check completed | Check `overallStatus` in response |
| `503 Service Unavailable` | Health check system failed | Restart application, check logs |
| `404 Not Found` | Wrong endpoint | Use `/api/v1/health`, not `/health` |

### Service Status Values

| Status | Meaning | Example |
|--------|---------|---------|
| `Healthy` | Service fully operational | All checks passing |
| `Degraded` | Service partially operational | API slow, rate limited, or using fallback |
| `Unhealthy` | Service non-operational | Connection refused, timeout, error response |

### Common Issues

#### All Services Show "Unhealthy"

**Cause**: Application not fully started or network isolation.

**Resolution**:
```bash
# Check if API is running
docker compose ps api

# Check API logs
docker compose logs api | tail -50

# Verify services are in same network
docker network inspect meepleai-network
```

#### Health Check Times Out

**Cause**: One or more external services not responding.

**Resolution**:
```bash
# Identify slow service
curl -w "@curl-format.txt" http://localhost:8080/api/v1/health

# curl-format.txt contents:
# time_total: %{time_total}s

# If timeout >10s, check external services (BGG, OpenRouter)
```

#### Degraded Despite All Secrets Configured

**Cause**: External API rate limiting or temporary unavailability.

**Resolution**:
```bash
# Check which services are degraded
curl -s http://localhost:8080/api/v1/health | \
    jq '.checks[] | select(.status != "Healthy")'

# Common causes:
# - OpenRouter: Rate limit exceeded (429 Too Many Requests)
# - BGG: API throttling (503 Service Unavailable)
# - SMTP: Authentication failed (invalid credentials)

# Wait for rate limit reset or configure alternative
```

---

## Admin Integration

### Admin Dashboard Health Display

**Future Feature** (Issue [#2464](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2464)): Admin dashboard will display real-time service health.

**Planned Features**:
- Visual status indicators (🟢 Healthy, 🟡 Degraded, 🔴 Unhealthy)
- Service dependency graph
- Historical uptime charts
- Alert configuration UI
- One-click service restart

**API Integration**:
```typescript
// apps/web/src/lib/api/health.ts
export async function getServiceHealth(): Promise<HealthCheckResponse> {
  const response = await fetch('/api/v1/health');
  return response.json();
}

// apps/web/src/app/admin/services/page.tsx
export default function ServicesPage() {
  const { data: health } = useQuery('health', getServiceHealth, {
    refetchInterval: 30000 // Poll every 30 seconds
  });

  return (
    <div>
      <h1>Service Status</h1>
      {health?.checks.map(check => (
        <ServiceCard key={check.serviceName} check={check} />
      ))}
    </div>
  );
}
```

---

## Testing

### Unit Tests (C#)

**Test Individual Service Checks**:
```csharp
// tests/Api.Tests/Infrastructure/Health/PostgresHealthCheckTests.cs
public class PostgresHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WithValidConnection_ReturnsHealthy()
    {
        // Arrange
        var healthCheck = new PostgresHealthCheck(_dbContext, _logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("Connected to PostgreSQL", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WithFailedConnection_ReturnsUnhealthy()
    {
        // Arrange
        var healthCheck = new PostgresHealthCheck(_faultyDbContext, _logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Failed to connect", result.Description);
    }
}
```

### Integration Tests

**Test Health Endpoint**:
```csharp
// tests/Api.Tests/Routing/HealthEndpointsTests.cs
public class HealthEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyWithAllServices()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var health = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        health.OverallStatus.Should().Be("Healthy");
        health.Checks.Should().HaveCountGreaterThan(10);
    }

    [Fact]
    public async Task ReadyEndpoint_ShouldReturn200WhenCriticalServicesHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Healthy");
    }
}
```

### E2E Tests (Playwright)

**Test Health Dashboard** (Future):
```typescript
// apps/web/e2e/admin/health-dashboard.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Admin Health Dashboard', () => {
  test('should display all service statuses', async ({ page }) => {
    await page.goto('/admin/services');

    // Check critical services are displayed
    await expect(page.locator('text=postgres')).toBeVisible();
    await expect(page.locator('text=redis')).toBeVisible();
    await expect(page.locator('text=qdrant')).toBeVisible();

    // Check status indicators
    const postgresStatus = page.locator('[data-service="postgres"] >> [data-testid="status-badge"]');
    await expect(postgresStatus).toHaveText(/Healthy|Degraded|Unhealthy/);
  });

  test('should refresh service status every 30 seconds', async ({ page }) => {
    await page.goto('/admin/services');

    const initialTimestamp = await page.locator('[data-testid="last-updated"]').textContent();

    await page.waitForTimeout(31000); // Wait 31 seconds

    const updatedTimestamp = await page.locator('[data-testid="last-updated"]').textContent();
    expect(updatedTimestamp).not.toBe(initialTimestamp);
  });
});
```

---

## Related Documentation

- **System Overview**: [Health Check System](../04-deployment/health-checks.md)
- **Auto-Configuration**: [Auto-Configuration Guide](../04-deployment/auto-configuration-guide.md)
- **Deployment Guide**: [docs/04-deployment/README.md](../04-deployment/README.md)
- **Admin Dashboard**: [docs/02-development/admin-dashboard-guide.md](../02-development/admin-dashboard-guide.md) (planned)

---

**Maintained by**: MeepleAI API Team
**Questions**: Open an issue on [GitHub](https://github.com/DegrassiAaron/meepleai-monorepo/issues)

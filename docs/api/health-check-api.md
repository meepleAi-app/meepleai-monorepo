# Health Check API Reference

> **Last Updated**: 2026-01-17
> **Related**: [Health Check System](../04-deployment/health-checks.md)
> **Issue**: [#2511](https://github.com/meepleAi-app/meepleai-monorepo/issues/2511)

---

## Endpoints

| Endpoint | Purpose | Auth |
|----------|---------|------|
| `/api/v1/health` | Comprehensive health check (all services) | None |
| `/health/ready` | Readiness probe (critical services only, Kubernetes) | None |
| `/health/live` | Liveness probe (application process, Kubernetes) | None |

---

## `/api/v1/health` - Comprehensive Check

### Request

```bash
GET http://localhost:8080/api/v1/health
```

### Response

**Status**: `200 OK` (always) or `503` (health system failure)

**Schema**:
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

| Field | Type | Values | Description |
|-------|------|--------|-------------|
| overallStatus | string | Healthy, Degraded, Unhealthy | System health summary |
| checks[] | array | - | Individual service checks |
| serviceName | string | postgres, redis, qdrant, etc. | Service identifier |
| status | string | Healthy, Degraded, Unhealthy | Service status |
| description | string | - | Human-readable message |
| isCritical | boolean | true/false | Core functionality dependency |
| timestamp | string | ISO 8601 | Check execution time |

**Overall Status Logic**:

| Status | Condition |
|--------|-----------|
| Healthy | All checks return Healthy |
| Degraded | ≥1 non-critical service is Degraded/Unhealthy |
| Unhealthy | ≥1 critical service is Unhealthy |

---

## Service Inventory

| Service | Criticality | Check Type | Timeout | Endpoint/Method |
|---------|-------------|------------|---------|-----------------|
| **postgres** | 🔴 CRITICAL | Database | 5s | `SELECT 1` |
| **redis** | 🔴 CRITICAL | Cache | 5s | `PING` |
| **qdrant** | 🔴 CRITICAL | HTTP | 5s | `GET /healthz` |
| **embedding** | 🔴 CRITICAL | HTTP | 5s | `GET /health` |
| **openrouter** | 🟡 IMPORTANT | HTTP | 5s | `GET /api/v1/models` |
| **bgg-api** | 🟡 IMPORTANT | HTTP | 10s | `GET /xmlapi2/thing?id=1` |
| **unstructured** | 🟡 IMPORTANT | HTTP | 5s | `GET /health` |
| **reranker** | 🟢 OPTIONAL | HTTP | 5s | `GET /health` |
| **smoldocling** | 🟢 OPTIONAL | HTTP | 5s | `GET /health` |
| **email-smtp** | 🟢 OPTIONAL | Config | 0s | Configuration check |
| **oauth-*** | 🟢 OPTIONAL | Config | 0s | Configuration check |
| **grafana** | 🟢 OPTIONAL | HTTP | 5s | `GET /api/health` |
| **prometheus** | 🟢 OPTIONAL | HTTP | 5s | `GET /-/healthy` |

---

## Response Examples

### All Services Healthy

```json
{
  "overallStatus": "Healthy",
  "checks": [
    {"serviceName": "postgres", "status": "Healthy", "description": "Connected to PostgreSQL", "isCritical": true},
    {"serviceName": "redis", "status": "Healthy", "description": "Connected to Redis", "isCritical": true},
    {"serviceName": "qdrant", "status": "Healthy", "description": "Connected to Qdrant vector database", "isCritical": true},
    {"serviceName": "embedding", "status": "Healthy", "description": "Embedding service ready", "isCritical": true},
    {"serviceName": "openrouter", "status": "Healthy", "description": "OpenRouter API accessible", "isCritical": false},
    {"serviceName": "bgg-api", "status": "Healthy", "description": "BoardGameGeek API accessible", "isCritical": false}
  ]
}
```

### Degraded (Non-Critical Down)

```json
{
  "overallStatus": "Degraded",
  "checks": [
    {"serviceName": "postgres", "status": "Healthy", "isCritical": true},
    {"serviceName": "redis", "status": "Healthy", "isCritical": true},
    {"serviceName": "openrouter", "status": "Degraded", "description": "API key missing (using Ollama fallback)", "isCritical": false},
    {"serviceName": "bgg-api", "status": "Unhealthy", "description": "BGG API timeout (503)", "isCritical": false}
  ]
}
```

**Interpretation**: Application functional with reduced features (no OpenRouter, BGG sync paused)

### Unhealthy (Critical Down)

```json
{
  "overallStatus": "Unhealthy",
  "checks": [
    {"serviceName": "postgres", "status": "Unhealthy", "description": "Failed to connect: Connection refused", "isCritical": true},
    {"serviceName": "redis", "status": "Healthy", "isCritical": true}
  ]
}
```

**Interpretation**: Core functionality broken (database unavailable) - immediate action required

---

## `/health/ready` - Readiness Probe

**Purpose**: Kubernetes readiness - can accept traffic?

**Request**: `GET http://localhost:8080/health/ready`

**Response**:
- `200 OK` + `"Healthy"` → Ready
- `503 Service Unavailable` + `"Unhealthy"` → Not ready

**Checks**: Critical services only (postgres, redis, qdrant, embedding)

**Kubernetes Config**:
```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5
  failureThreshold: 3
```

---

## `/health/live` - Liveness Probe

**Purpose**: Kubernetes liveness - is process running?

**Request**: `GET http://localhost:8080/health/live`

**Response**:
- `200 OK` + `"Healthy"` → Alive
- No response → Crashed (Kubernetes restarts)

**Checks**: Application process only (no external dependencies)

**Kubernetes Config**:
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
  failureThreshold: 3
```

---

## Integration Examples

### Prometheus Monitoring

**Scrape Config** (`prometheus.yml`):
```yaml
scrape_configs:
  - job_name: 'meepleai-health'
    metrics_path: '/api/v1/health'
    static_configs:
      - targets: ['api:8080']
```

**Alert Rules**:
```yaml
- alert: CriticalServiceDown
  expr: health_check_status{is_critical="true"} == 0
  for: 2m
  labels:
    severity: critical

- alert: DegradedStatus
  expr: health_check_status{is_critical="false"} == 0
  for: 10m
  labels:
    severity: warning
```

### Load Balancer (Nginx)

```nginx
upstream meepleai_backend {
    server api1:8080 max_fails=3 fail_timeout=30s;
    server api2:8080 max_fails=3 fail_timeout=30s;
}

location /api {
    proxy_pass http://meepleai_backend;
    health_check uri=/health/ready interval=10s fails=3 passes=2;
}
```

### CI/CD Validation

```yaml
# GitHub Actions
- name: Health Check Validation
  run: |
    RESPONSE=$(curl -s http://localhost:8080/api/v1/health)
    STATUS=$(echo $RESPONSE | jq -r '.overallStatus')

    if [[ "$STATUS" == "Unhealthy" ]]; then
      echo "❌ Health check failed!"
      exit 1
    fi

    echo "✅ Health check passed: $STATUS"
```

---

## Troubleshooting

### Common Issues

| Issue | Diagnosis | Resolution |
|-------|-----------|------------|
| **All Unhealthy** | App not started or network isolation | Check `docker compose ps api` and logs |
| **Timeout** | External service not responding | Identify slow service with `curl -w` timing |
| **Degraded (all secrets OK)** | External API rate limiting | Check specific service in response, wait for rate limit reset |

### Error Codes

| Code | Meaning | Action |
|------|---------|--------|
| 200 OK | Check completed | Review `overallStatus` in response body |
| 503 Service Unavailable | Health system failed | Restart app, check logs |
| 404 Not Found | Wrong endpoint | Use `/api/v1/health` not `/health` |

---

## Testing

**Unit Tests**: `tests/Api.Tests/Infrastructure/Health/PostgresHealthCheckTests.cs` (per-service)
**Integration Tests**: `tests/Api.Tests/Routing/HealthEndpointsTests.cs` (endpoint validation)
**E2E Tests** (Future): `apps/web/e2e/admin/health-dashboard.spec.ts` (admin UI)

**Example Test**:
```csharp
[Fact]
public async Task HealthEndpoint_ShouldReturnHealthyWithAllServices()
{
    var response = await _client.GetAsync("/api/v1/health");
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var health = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
    health.OverallStatus.Should().Be("Healthy");
    health.Checks.Should().HaveCountGreaterThan(10);
}
```

---

## Related Documentation

- [Health Check System Overview](../04-deployment/health-checks.md)
- [Auto-Configuration Guide](../04-deployment/auto-configuration-guide.md)
- [Deployment Guide](../04-deployment/README.md)

---

**Maintained by**: MeepleAI API Team
**Questions**: [GitHub Issues](https://github.com/meepleAi-app/meepleai-monorepo/issues)

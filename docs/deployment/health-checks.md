# Health Check System

> **Last Updated**: 2026-01-16
> **Issue**: [#2511](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2511)
> **Status**: ✅ Implemented

## Overview

MeepleAI implements a comprehensive health check system to monitor the status of all critical and non-critical services across Core Infrastructure, AI Services, External APIs, and Monitoring systems. The health check system provides real-time visibility into service availability and enables proactive incident detection.

## Endpoints

### `/api/v1/health` (Comprehensive Health Check)

- **Method**: GET
- **Authentication**: Public (no auth required)
- **Response Format**: JSON
- **Timeout**: 5 seconds per service (checks run in parallel)

#### Response Structure

```json
{
  "overallStatus": "Healthy|Degraded|Unhealthy",
  "checks": [
    {
      "serviceName": "postgres",
      "status": "Healthy",
      "description": "Connected to PostgreSQL",
      "isCritical": true,
      "timestamp": "2026-01-16T10:00:00Z"
    },
    {
      "serviceName": "openrouter",
      "status": "Degraded",
      "description": "OpenRouter API key missing (using fallback)",
      "isCritical": false,
      "timestamp": "2026-01-16T10:00:00Z"
    }
  ],
  "timestamp": "2026-01-16T10:00:00Z"
}
```

#### Overall Status Logic

| Overall Status | Condition |
|----------------|-----------|
| **Healthy** | All checks are Healthy |
| **Degraded** | At least one non-critical service is Degraded or Unhealthy |
| **Unhealthy** | At least one critical service is Unhealthy |

### `/health`, `/health/ready`, `/health/live` (Backwards Compatibility)

These endpoints are maintained for backwards compatibility with existing infrastructure and Kubernetes health probes:

- **`/health`**: Full health check (all services)
- **`/health/ready`**: Readiness probe (DB + Cache + Vector only)
- **`/health/live`**: Liveness probe (app process running)

## Service Categories

### Core Infrastructure (Critical)

Critical services required for basic application functionality. Failure of any core service results in overall status `Unhealthy`.

| Service | Check Type | Critical | Description |
|---------|------------|----------|-------------|
| **PostgreSQL** | Database connectivity | ✅ Yes | Primary relational database |
| **Redis** | Cache connectivity | ✅ Yes | Session storage and caching |
| **Qdrant** | Vector DB connectivity | ✅ Yes | Vector search for RAG |

### AI Services

Services powering AI-driven features. Embedding service is critical for RAG pipeline; others are non-critical with fallback capabilities.

| Service | Check Type | Critical | Description |
|---------|------------|----------|-------------|
| **OpenRouter** | HTTP connectivity | ❌ No | Cloud AI gateway (fallback available) |
| **Embedding Service** | HTTP health endpoint | ✅ Yes | Python microservice for embeddings |
| **Reranker** | HTTP health endpoint | ❌ No | Cross-encoder reranking for precision |
| **Unstructured API** | HTTP connectivity | ❌ No | PDF processing service |
| **SmolDocling** | HTTP health endpoint | ❌ No | Document intelligence extraction |

### External APIs

Third-party services for extended functionality. All are non-critical as they provide auxiliary features.

| Service | Check Type | Critical | Description |
|---------|------------|----------|-------------|
| **BGG API** | HTTP connectivity | ❌ No | BoardGameGeek catalog integration |
| **OAuth Providers** | Configuration check | ❌ No | Google/Discord OAuth credentials |
| **Email/SMTP** | SMTP connection | ❌ No | Transactional email service |

### Monitoring Services

Observability and monitoring infrastructure. All are non-critical as they support operations but don't block core features.

| Service | Check Type | Critical | Description |
|---------|------------|----------|-------------|
| **Grafana** | HTTP health endpoint | ❌ No | Dashboard and visualization |
| **Prometheus** | HTTP health endpoint | ❌ No | Metrics collection |

## Configuration

Health checks are automatically registered in `ObservabilityServiceExtensions.cs`. No additional configuration is required.

### Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `REDIS_URL` | `localhost:6379` | Redis connection string |
| `QDRANT_URL` | `http://localhost:6333` | Qdrant vector database URL |
| `LOCAL_EMBEDDING_URL` | `http://embedding-service:8000` | Embedding service URL |
| `RERANKER_URL` | *(required)* | Reranker service URL |
| `PdfProcessing:UnstructuredApiUrl` | *(optional)* | Unstructured API endpoint |
| `PdfProcessing:SmolDoclingApiUrl` | *(optional)* | SmolDocling API endpoint |
| `OPENROUTER_API_KEY` | *(required)* | OpenRouter authentication |
| `Bgg:BaseUrl` | `https://boardgamegeek.com` | BGG API base URL |
| `Authentication:Google:ClientId` | *(optional)* | Google OAuth client ID |
| `Authentication:Google:ClientSecret` | *(optional)* | Google OAuth secret |
| `Authentication:Discord:ClientId` | *(optional)* | Discord OAuth client ID |
| `Authentication:Discord:ClientSecret` | *(optional)* | Discord OAuth secret |
| `Email:SmtpServer` | *(optional)* | SMTP server hostname |
| `Email:Port` | *(optional)* | SMTP server port |
| `Monitoring:GrafanaUrl` | `http://grafana:3000` | Grafana instance URL |
| `Monitoring:PrometheusUrl` | `http://prometheus:9090` | Prometheus instance URL |

## Docker Compose Integration

Add health check configuration to your Docker Compose services:

```yaml
services:
  api:
    image: meepleai/api:latest
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/ready"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    environment:
      - REDIS_URL=redis:6379
      - QDRANT_URL=http://qdrant:6333
      - LOCAL_EMBEDDING_URL=http://embedding-service:8000
```

## Kubernetes Integration

### Liveness Probe

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

### Readiness Probe

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

## Startup Health Check

The application performs a startup health check for critical services before beginning to accept traffic. This check runs automatically during `Program.cs` initialization.

**Behavior**:
- **All critical services healthy**: Application starts normally with info log
- **One or more critical services unhealthy**: Application logs critical warning and starts in degraded mode (does not fail-fast)

**Rationale**: The application starts in degraded mode rather than failing fast to allow for temporary network issues or service startup delays. Kubernetes readiness probes will prevent traffic until critical services recover.

**Log Example**:
```
[10:00:00 INF] All critical services passed startup health check.
```

```
[10:00:00 CRI] Critical services failed startup health check: postgres, redis. Application starting in degraded mode.
```

## Troubleshooting

### Critical Service Failure

**Symptoms**: `/api/v1/health` returns `overallStatus: "Unhealthy"`

**Resolution**:

1. **Check service logs**:
   ```bash
   docker compose logs postgres
   docker compose logs redis
   docker compose logs qdrant
   ```

2. **Verify connection strings**:
   ```bash
   # Check environment variables
   docker compose exec api env | grep -E "REDIS_URL|QDRANT_URL|POSTGRES"
   ```

3. **Restart failed service**:
   ```bash
   docker compose restart postgres
   docker compose restart redis
   ```

4. **Verify network connectivity**:
   ```bash
   docker compose exec api ping postgres
   docker compose exec api curl http://qdrant:6333/healthz
   ```

### Degraded Status

**Symptoms**: `/api/v1/health` returns `overallStatus: "Degraded"`

**Explanation**: Non-critical services may be unavailable without impacting core functionality. The application will continue to operate normally for essential features.

**Common Causes**:
- OpenRouter API key not configured (falls back to local Ollama)
- BGG API rate limit exceeded (catalog updates paused)
- Email SMTP not configured (notifications disabled)
- Monitoring services offline (observability reduced)

**Resolution**: Address configuration issues for affected services or accept degraded functionality.

### Timeout Issues

**Symptoms**: Health check takes >10 seconds to complete

**Explanation**: Each health check has a 5-second timeout, but checks run in parallel. Total time should be <10 seconds even with all checks.

**Resolution**:
1. Check network latency to external services
2. Verify services are responding (not hanging)
3. Review logs for timeout warnings

### Embedding Service Critical Failure

**Symptoms**: `embedding` service shows `Unhealthy` status

**Impact**: RAG pipeline cannot generate embeddings, preventing:
- Document ingestion and vectorization
- Semantic search for rules and game content
- AI-powered game assistance features

**Resolution**:
1. **Check embedding service logs**:
   ```bash
   docker compose logs embedding-service
   ```

2. **Verify service is running**:
   ```bash
   docker compose ps embedding-service
   docker compose exec api curl http://embedding-service:8000/health
   ```

3. **Restart embedding service**:
   ```bash
   docker compose restart embedding-service
   ```

## Monitoring and Alerting

### Grafana Dashboard

Import the health check dashboard JSON from `infra/monitoring/grafana/dashboards/health-check.json` (TODO: Create dashboard).

**Panels**:
- Overall status gauge (Healthy/Degraded/Unhealthy)
- Service status table with criticality flags
- Health check latency graph per service
- Critical service failure alerts

### Prometheus Metrics

Health check metrics are exposed via OpenTelemetry:

```promql
# Service health status (0=Unhealthy, 1=Degraded, 2=Healthy)
health_check_status{service="postgres"} 2

# Health check duration in seconds
health_check_duration_seconds{service="postgres"} 0.012
```

### Alert Rules

Configure Prometheus alert rules for critical service failures:

```yaml
groups:
  - name: health_checks
    rules:
      - alert: CriticalServiceDown
        expr: health_check_status{is_critical="true"} == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Critical service {{ $labels.service }} is unhealthy"
```

## Testing

### Integration Tests

Run comprehensive health check tests:

```bash
cd tests/Api.Tests
dotnet test --filter "HealthCheckIntegrationTests"
```

### Unit Tests

Run unit tests for individual health checks:

```bash
dotnet test --filter "OpenRouterHealthCheckTests"
```

### Manual Testing

```bash
# Comprehensive health check
curl http://localhost:8080/api/v1/health | jq

# Readiness probe
curl http://localhost:8080/health/ready

# Liveness probe
curl http://localhost:8080/health/live
```

## Architecture

### Health Check Pipeline

```mermaid
graph LR
    A[HTTP Request] --> B[/api/v1/health]
    B --> C[HealthCheckService]
    C --> D1[Core Checks]
    C --> D2[AI Checks]
    C --> D3[External Checks]
    C --> D4[Monitoring Checks]
    D1 --> E[Aggregate Results]
    D2 --> E
    D3 --> E
    D4 --> E
    E --> F[Determine Overall Status]
    F --> G[JSON Response]
```

### Registration Flow

```csharp
// ObservabilityServiceExtensions.cs
services.AddHealthChecks()
    .AddNpgSql(..., tags: ["core", "critical"])  // PostgreSQL
    .AddRedis(..., tags: ["core", "critical"])   // Redis
    .AddUrlGroup(..., tags: ["core", "critical"]) // Qdrant
    .AddComprehensiveHealthChecks();             // 10+ additional checks
```

### Custom Health Checks

All health checks implement `IHealthCheck`:

```csharp
public class OpenRouterHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check service availability
            var response = await _httpClient.GetAsync("/api/v1/models", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("OpenRouter API is accessible")
                : HealthCheckResult.Degraded($"OpenRouter returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouter health check failed");
            return HealthCheckResult.Unhealthy("OpenRouter unavailable", ex);
        }
    }
}
```

## References

- **Issue**: [#2511 - Implement Comprehensive Health Check System](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2511)
- **Microsoft Docs**: [Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- **Source Code**:
  - `Infrastructure/Health/Checks/` - Individual health check implementations
  - `Infrastructure/Health/Extensions/HealthCheckServiceExtensions.cs` - DI registration
  - `Routing/HealthCheckEndpoints.cs` - Endpoint configuration
  - `Program.cs` - Startup health check integration

---

**Maintained by**: MeepleAI DevOps Team
**Questions**: Open an issue on GitHub

# Observability Guide (OPS-01)

This document describes the observability infrastructure in MeepleAI, including health checks, structured logging, and log aggregation.

## Overview

MeepleAI implements comprehensive observability patterns to enable monitoring, debugging, and operational insights:

- **Health Checks**: Monitor the status of critical dependencies (Postgres, Redis, Qdrant)
- **Structured Logging**: Serilog with correlation IDs for request tracing
- **Log Aggregation**: Seq dashboard for centralized log viewing and searching
- **Request Tracing**: X-Correlation-Id headers for distributed request tracking

## Health Checks

### Available Endpoints

#### `/health` - Detailed Health Status
Returns comprehensive health information for all dependencies with detailed metrics.

**Response Format**:
```json
{
  "status": "Healthy|Degraded|Unhealthy",
  "checks": [
    {
      "name": "postgres",
      "status": "Healthy",
      "description": "Database connection",
      "duration": 12.5,
      "tags": ["db", "sql"]
    },
    {
      "name": "redis",
      "status": "Healthy",
      "description": "Cache connection",
      "duration": 3.2,
      "tags": ["cache", "redis"]
    },
    {
      "name": "qdrant",
      "status": "Healthy",
      "description": "Vector DB HTTP endpoint",
      "duration": 15.8,
      "tags": ["vector", "qdrant"]
    },
    {
      "name": "qdrant-collection",
      "status": "Healthy",
      "description": "Qdrant collection is accessible",
      "duration": 45.3,
      "tags": ["vector", "qdrant", "collection"]
    }
  ],
  "totalDuration": 78.5
}
```json
#### `/health/ready` - Readiness Probe
Checks if the API is ready to serve traffic (all dependencies are healthy).

**Usage**: Kubernetes readiness probes, load balancer health checks

#### `/health/live` - Liveness Probe
Checks if the API process is alive and responding.

**Usage**: Kubernetes liveness probes

### Health Check Tags

Health checks are tagged for filtering:

- `db`, `sql` - Database dependencies
- `cache`, `redis` - Caching layer
- `vector`, `qdrant` - Vector search engine
- `collection` - Qdrant collection-specific checks

### Implementation Details

Health checks are configured in `Program.cs:277-291`:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: new[] { "db", "sql" })
    .AddRedis(redisConnectionString, name: "redis", tags: new[] { "cache", "redis" })
    .AddUrlGroup(new Uri($"{qdrantUrl}/healthz"), name: "qdrant", tags: new[] { "vector", "qdrant" })
    .AddCheck<QdrantHealthCheck>("qdrant-collection", tags: new[] { "vector", "qdrant", "collection" });
```json
Custom `QdrantHealthCheck` (`Infrastructure/QdrantHealthCheck.cs:11-43`) validates the Qdrant collection exists and is accessible.

## Structured Logging

### Serilog Configuration

MeepleAI uses Serilog for structured logging configured via `Api/Logging/LoggingConfiguration.cs`:

- **Minimum Level**: Information
- **Overrides**:
  - `Microsoft.AspNetCore`: Warning
  - `Microsoft.EntityFrameworkCore`: Warning
- **Enrichers**:
  - Machine Name
  - Environment Name
  - Log Context (includes correlation ID, user ID, request path, etc.)
- **Sinks**:
  - Console (for local development and container logs)
  - Seq (for centralized log aggregation)

### Correlation IDs

Every HTTP request is automatically assigned a correlation ID for end-to-end tracing:

1. **Request ID**: ASP.NET Core `TraceIdentifier` is used as the correlation ID
2. **Response Header**: `X-Correlation-Id` header is added to every response (see middleware in `Program.cs`)
3. **Log Enrichment**: Correlation ID is included in every log entry via `RequestId` property (via `CorrelationIdEnricher`)

### Request Logging

Comprehensive request logging is configured with Serilog enrichers (see `Api/Logging/LoggingEnrichers.cs`):

**Logged Properties**:
- `RequestId` - Correlation ID (TraceIdentifier)
- `RequestPath` - HTTP request path
- `RequestMethod` - HTTP method (GET, POST, etc.)
- `UserAgent` - Client user agent string
- `RemoteIp` - Client IP address
- `UserId` - Authenticated user ID (if authenticated)
- `UserEmail` - Authenticated user email (if authenticated)

**Example Log Entry**:
```
[14:23:45 INF] HTTP POST /agents/qa responded 200 in 1234.5678 ms
{
  "RequestId": "0HN6G8QJ9KL0M:00000001",
  "RequestPath": "/agents/qa",
  "RequestMethod": "POST",
  "UserAgent": "Mozilla/5.0...",
  "RemoteIp": "192.168.1.10",
  "UserId": "user-123",
  "UserEmail": "user@example.com"
}
```json
### Using Correlation IDs for Debugging

When debugging issues:

1. **Find the Correlation ID**: Check the `X-Correlation-Id` response header
2. **Search Logs**: Use Seq to search for all logs with that correlation ID
3. **Trace the Request**: Follow the complete request flow across services

**Seq Search Query Example**:
```
RequestId = "0HN6G8QJ9KL0M:00000001"
```json
## Seq Log Aggregation

### Accessing Seq

- **URL**: `http://localhost:8081` (when running via Docker Compose)
- **No authentication required** (for local development)

### Configuration

Seq is configured automatically when the `SEQ_URL` environment variable is set:

**Docker Compose** (`infra/docker-compose.yml:52-63`):
```yaml
seq:
  image: datalust/seq:2025.1
  ports:
    - "5341:5341"  # Ingestion port
    - "8081:80"    # Web UI port
  volumes:
    - seqdata:/data
```json
**API Configuration** (`infra/env/api.env.dev:10-11`):
```env
SEQ_URL=http://seq:5341
```

### Using Seq

#### Searching Logs

**By Correlation ID**:
```
RequestId = "0HN6G8QJ9KL0M:00000001"
```

**By User**:
```
UserId = "user-123"
```

**By Endpoint**:
```
RequestPath = "/agents/qa"
```

**By Time Range**:
Use the time picker in the Seq UI to filter by time range.

**By Log Level**:
```
@Level = "Error"
```

**Complex Queries**:
```
@Level = "Error" and RequestPath like "/agents/%"
```json
#### Creating Dashboards

Seq supports creating custom dashboards with charts and visualizations:

1. Navigate to **Dashboards** in Seq
2. Click **New Dashboard**
3. Add charts for:
   - Request rate over time
   - Error rate by endpoint
   - Average response time
   - Top slow requests

#### Setting Up Alerts

Seq can send alerts for critical errors:

1. Navigate to **Settings** → **Alerts**
2. Create alert rules for:
   - Error rate exceeds threshold
   - Slow requests (> 5 seconds)
   - Failed dependencies (health check failures)

### Log Retention

By default, Seq stores logs in the Docker volume `seqdata`. Configure retention policies in Seq settings to manage disk usage.

## Monitoring Best Practices

### For Developers

1. **Always include correlation IDs** when reporting bugs
2. **Use structured logging** with properties instead of string interpolation
3. **Log at appropriate levels**:
   - `Information`: Normal operation milestones
   - `Warning`: Recoverable errors, degraded performance
   - `Error`: Unhandled exceptions, critical failures
4. **Include context** in log messages (user ID, game ID, PDF ID, etc.)

### For Operations

1. **Monitor health checks** regularly (`/health` endpoint)
2. **Set up Seq alerts** for critical errors
3. **Review error logs daily** for patterns
4. **Track correlation IDs** for customer support issues
5. **Use Seq dashboards** to visualize:
   - Request rates and latency
   - Error rates by endpoint
   - Cache hit rates
   - AI token usage

### Example Structured Logging

**Good**:
```csharp
logger.LogInformation("PDF uploaded successfully: {PdfId}", result.Document.Id);
```

**Bad**:
```csharp
logger.LogInformation($"PDF uploaded successfully: {result.Document.Id}");
```sql
The structured approach allows Seq to index and search by `PdfId`.

## Troubleshooting

### Seq Not Receiving Logs

1. **Check Seq is running**:
   ```bash
   docker compose ps seq
   ```

2. **Verify SEQ_URL is set** in `infra/env/api.env.dev`:
   ```env
   SEQ_URL=http://seq:5341
   ```

3. **Check API logs** for Seq connection errors:
   ```bash
   docker compose logs api | grep -i seq
   ```

4. **Test Seq ingestion endpoint**:
   ```bash
   curl http://localhost:5341/api
   ```

### Health Checks Failing

1. **Check /health endpoint**:
   ```bash
   curl http://localhost:8080/health
   ```

2. **Verify dependencies are running**:
   ```bash
   docker compose ps
   ```

3. **Check individual service health**:
   - Postgres: `docker exec -it meepleai-postgres-1 pg_isready`
   - Redis: `docker exec -it meepleai-redis-1 redis-cli ping`
   - Qdrant: `curl http://localhost:6333/healthz`

4. **Review health check logs** in Seq:
   ```
   @Message like "%health check%"
   ```

### Correlation IDs Not Appearing

1. **Check response headers**:
   ```bash
   curl -I http://localhost:8080/
   ```
   Look for `X-Correlation-Id` header.

2. **Verify Serilog request logging** is enabled in `Api/Logging/LoggingConfiguration.cs`

3. **Check log enrichment** in Seq - ensure `RequestId` property is present

## Enhanced Logging (OPS-04)

### Sensitive Data Redaction

MeepleAI automatically redacts sensitive data from all logs to prevent secrets from appearing in Seq or console output.

**Automatically Redacted**:
- Passwords (`password`, `passwd`, `pwd`)
- API keys (`apikey`, `api_key`) - including MeepleAI format (`mpl_live_xxx`, `mpl_test_xxx`)
- Tokens (`token`, `bearer`, `authorization`, JWT tokens)
- Connection strings with passwords
- Private keys, encryption keys, client secrets
- Session IDs, cookies, CSRF tokens

**Example**:
```csharp
// Input
var loginData = new { Username = "admin", Password = "secret123", ApiKey = "mpl_live_abc..." };
logger.LogInformation("Login attempt: {@LoginData}", loginData);

// Output in logs
// Login attempt: { Username: "admin", Password: "[REDACTED]", ApiKey: "[REDACTED]" }
```

### Environment-Based Log Levels

Log levels are automatically configured based on environment:

| Environment  | Console Level | Seq Level | Default Level |
|--------------|---------------|-----------|---------------|
| Development  | Debug         | Debug     | Debug         |
| Staging      | Information   | Debug     | Information   |
| Production   | Warning       | Debug     | Information   |

**Override via Configuration**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "MyNamespace.MyService": "Debug"
    }
  }
}
```

### Structured Logging Best Practices

**Good** - Use structured logging with `@` destructuring:
```csharp
logger.LogInformation("PDF uploaded: {PdfId}, Game: {@Game}", pdfId, game);
```

**Bad** - String interpolation loses structure and bypasses redaction:
```csharp
logger.LogInformation($"PDF {pdfId} uploaded for game {game}");
```

**Good** - Log objects with sensitive data (auto-redacted):
```csharp
logger.LogInformation("User authentication: {@AuthData}", authData);
```

**Bad** - Manual string concatenation (secrets exposed):
```csharp
logger.LogInformation($"User {user.Email} password {user.Password}");
```

## References

- **Health Checks**: `Program.cs` (search for `AddHealthChecks`), `Infrastructure/QdrantHealthCheck.cs`
- **Serilog Configuration**: `Api/Logging/LoggingConfiguration.cs`
- **Sensitive Data Redaction**: `Api/Logging/SensitiveDataDestructuringPolicy.cs`
- **Log Enrichers**: `Api/Logging/LoggingEnrichers.cs`
- **Correlation ID Middleware**: `Program.cs` (search for `X-Correlation-Id`)
- **Seq Configuration**: `infra/docker-compose.yml:52-63`
- **Custom Health Check**: `Infrastructure/QdrantHealthCheck.cs`
- **OPS-04 Technical Design**: `docs/technic/ops-04-structured-logging-design.md`

## Next Steps (Future OPS Issues)

- ~~**OPS-02**: Add OpenTelemetry for distributed tracing with Jaeger/Tempo~~ (Completed)
- ~~**OPS-03**: Implement frontend error handling and error boundaries~~ (In Progress - #293)
- ~~**OPS-04**: Enhanced logging with structured error context~~ (Completed - #294)
- **OPS-05**: Error monitoring and alerting with PagerDuty/Slack integration
- **OPS-06**: Log sampling and distributed tracing improvements

## AI Quality Monitoring (AI-11.2)

### Overview

MeepleAI tracks AI response quality across multiple dimensions with comprehensive metrics, dashboards, and alerts to ensure high-quality user experience.

### Quality Dimensions

Every AI response is scored across four dimensions (0.0-1.0 scale):

1. **RAG Confidence**: Relevance of retrieved rulebook context from vector search
2. **LLM Confidence**: Language model's confidence in its generated response
3. **Citation Quality**: Accuracy and completeness of rulebook citations
4. **Overall Confidence**: Weighted average of all dimensions

**Quality Tiers**:
- **High**: ≥0.80 overall confidence (target: 80%+ of responses)
- **Medium**: 0.60-0.80 overall confidence
- **Low**: <0.60 overall confidence (flagged for investigation)

### Grafana Dashboard

Access the **AI Quality Monitoring** dashboard at http://localhost:3001/d/quality-metrics

**Dashboard Panels**:

1. **Overall Confidence Trend**: Time series showing overall confidence with quality tier thresholds
2. **Quality Tier Distribution**: Pie chart showing percentage of responses by tier
3. **Low-Quality Response Rate**: Gauge showing percentage of low-quality responses
4. **Dimensional Confidence Breakdown**: Bar gauge showing average confidence per dimension
5. **Confidence Percentiles**: Time series showing p50, p95, p99 confidence scores
6. **Response Volume by Agent Type & Quality Tier**: Stacked area chart
7. **Low-Quality Responses by Agent Type**: Bar chart

### Prometheus Alerts

Quality alerts are configured in `infra/prometheus-rules.yml` with recording rules and alerting rules.

**Critical Alerts** (Immediate action required):

| Alert Name | Threshold | Duration | Description |
|------------|-----------|----------|-------------|
| LowOverallConfidence | <0.60 | 1 hour | AI responses consistently low quality |
| HighLowQualityRate | >30% | 1 hour | >1 in 3 responses failing quality checks |
| LowRagConfidence | <0.50 | 30 min | RAG retrieval not finding relevant context |
| LowLlmConfidence | <0.50 | 30 min | LLM producing low-confidence responses |
| QualityMetricsUnavailable | No metrics | 15 min | Quality scoring pipeline broken |

**Warning Alerts** (Monitor & investigate):

| Alert Name | Threshold | Duration | Description |
|------------|-----------|----------|-------------|
| DegradedOverallConfidence | <0.70 | 30 min | Response quality degraded but not critical |
| ElevatedLowQualityRate | >15% | 30 min | Higher than normal rate of quality failures |

### Quality Metrics

Quality metrics are exposed via the `/metrics` endpoint using OpenTelemetry:

**Histogram**: `meepleai_quality_score`
- **Buckets**: 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0
- **Dimensions**: rag_confidence, llm_confidence, citation_quality, overall_confidence
- **Labels**: agent_type (qa, explain, setup), operation, quality_tier (high, medium, low)

**Counter**: `meepleai_quality_low_quality_responses_total`
- **Labels**: agent_type, operation
- **Increments**: When overall_confidence < 0.60

### Troubleshooting Quality Issues

When quality alerts fire, follow the [AI Quality Low Runbook](./runbooks/ai-quality-low.md) for detailed troubleshooting steps and escalation procedures.

**Implementation Details**:
- QualityMetrics class: `apps/api/src/Api/Observability/QualityMetrics.cs`
- Dashboard: `infra/dashboards/ai-quality-monitoring.json`
- Alert Rules: `infra/prometheus-rules.yml`
- Runbook: `docs/runbooks/ai-quality-low.md`

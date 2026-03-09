# Service Endpoints Reference

**Last Updated**: 2026-02-02

Comprehensive list of all service endpoints organized by type.

---

## Frontend & API

| Service | URL | Description | Credentials |
|---------|-----|-------------|-------------|
| **Web UI** | http://localhost:3000 | Next.js frontend application | - |
| **API Base** | http://localhost:8080 | ASP.NET Core API | - |
| **API Health** | http://localhost:8080/health | Health check endpoint | - |
| **API Documentation** | http://localhost:8080/scalar/v1 | Interactive API docs (Scalar UI) | - |
| **Swagger JSON** | http://localhost:8080/swagger/v1/swagger.json | OpenAPI specification | - |

---

## Database & Storage Services

### PostgreSQL (Relational Database)

**Connection**: `localhost:5432`
**Database**: `meepleai`
**User**: Check `infra/secrets/database.secret` (`POSTGRES_USER`)
**Password**: Check `infra/secrets/database.secret` (`POSTGRES_PASSWORD`)

**Test Command**:
```bash
# From host
psql -h localhost -p 5432 -U postgres -d meepleai -c "SELECT version();"

# From Docker
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c "SELECT version();"
```

### Redis (Cache & Session Store)

**Connection**: `localhost:6379`
**Password**: Check `infra/secrets/redis.secret` (`REDIS_PASSWORD`)

**Test Command**:
```bash
# Get password first
pwsh -c "cat infra/secrets/redis.secret | Select-String 'REDIS_PASSWORD' | ForEach-Object { $_.Line.Split('=')[1] }"

# Test connection (replace PASSWORD)
docker exec -it meepleai-redis redis-cli -a PASSWORD PING
# Expected: PONG
```

**Common Commands**:
```bash
# Inside Redis CLI
docker exec -it meepleai-redis redis-cli -a PASSWORD

# Once inside:
> PING
> INFO
> KEYS *
> GET key_name
> FLUSHDB  # ⚠️ Clears current database
```

### Qdrant (Vector Database)

| URL | Description |
|-----|-------------|
| http://localhost:6333 | HTTP REST API |
| http://localhost:6333/dashboard | Web UI (if enabled in config) |
| http://localhost:6333/collections | List all collections |
| http://localhost:6333/collections/{name} | Collection details |
| http://localhost:6333/collections/{name}/points | Vector points in collection |
| `grpc://localhost:6334` | gRPC API endpoint |

**Test Command**:
```bash
# List collections
curl http://localhost:6333/collections

# Get collection info
curl http://localhost:6333/collections/games

# Health check
curl http://localhost:6333/
```

---

## AI & ML Services

### Embedding Service (Multilingual)

**Base URL**: http://localhost:8000
**Model**: `intfloat/multilingual-e5-large`
**Dimensions**: 1024

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/embed` | POST | Generate embeddings |
| `/batch_embed` | POST | Batch embedding generation |
| `/model_info` | GET | Model information |

**Test Command**:
```bash
# Health
curl http://localhost:8000/health

# Embed text
curl -X POST http://localhost:8000/embed \
  -H "Content-Type: application/json" \
  -d '{"text": "Hello, world!"}'

# Batch embed
curl -X POST http://localhost:8000/batch_embed \
  -H "Content-Type: application/json" \
  -d '{"texts": ["Text 1", "Text 2", "Text 3"]}'
```

### Reranker Service (Cross-Encoder)

**Base URL**: http://localhost:8003
**Model**: `BAAI/bge-reranker-v2-m3`

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/rerank` | POST | Rerank query-document pairs |
| `/batch_rerank` | POST | Batch reranking |

**Test Command**:
```bash
# Health
curl http://localhost:8003/health

# Rerank
curl -X POST http://localhost:8003/rerank \
  -H "Content-Type: application/json" \
  -d '{
    "query": "board games",
    "documents": [
      "Catan is a strategy game",
      "Chess is an ancient game",
      "Monopoly is a property trading game"
    ]
  }'
```

### Unstructured Service (PDF Stage 1)

**Base URL**: http://localhost:8001
**Purpose**: Fast PDF text extraction

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/process-pdf` | POST | Extract text from PDF |
| `/metrics` | GET | Service metrics |

**Test Command**:
```bash
# Health
curl http://localhost:8001/health

# Process PDF (multipart form-data)
curl -X POST http://localhost:8001/process-pdf \
  -F "file=@path/to/document.pdf"
```

### SmolDocling Service (PDF Stage 2)

**Base URL**: http://localhost:8002
**Purpose**: Complex layout extraction with VLM
**Model**: `docling-project/SmolDocling-256M-preview`

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/extract` | POST | Extract from complex PDFs |
| `/batch_extract` | POST | Batch PDF processing |
| `/model_info` | GET | Model information |

**Test Command**:
```bash
# Health
curl http://localhost:8002/health

# Extract (multipart form-data)
curl -X POST http://localhost:8002/extract \
  -F "file=@path/to/complex-document.pdf"
```

**⚠️ Note**: First startup may take 2-5 minutes for model download (~500MB)

### Ollama (Local LLM)

**Base URL**: http://localhost:11434

| Endpoint | Description |
|----------|-------------|
| `/` | Ollama service info |
| `/api/tags` | List available models |
| `/api/version` | Ollama version |
| `/api/generate` | Text generation |
| `/api/chat` | Chat completion |
| `/api/embeddings` | Generate embeddings (if model supports) |

**Test Command**:
```bash
# List models
curl http://localhost:11434/api/tags

# Version
curl http://localhost:11434/api/version

# Generate (requires model pulled)
curl -X POST http://localhost:11434/api/generate \
  -H "Content-Type: application/json" \
  -d '{
    "model": "nomic-embed-text",
    "prompt": "Hello, world!",
    "stream": false
  }'
```

**Pull Model**:
```bash
# Pull model manually
docker exec -it meepleai-ollama ollama pull nomic-embed-text

# Or via API
curl -X POST http://localhost:11434/api/pull \
  -d '{"name": "nomic-embed-text"}'
```

---

## Monitoring & Observability

### Grafana (Dashboards & Visualization)

**URL**: http://localhost:3001
**Credentials**: `admin` / check `infra/secrets/monitoring.secret` (`GRAFANA_ADMIN_PASSWORD`)
**Default Dev**: `admin` / `admin` (change on first login)

**Features**:
- Pre-configured Prometheus datasource
- Pre-loaded dashboards: System Metrics, API Performance, Container Metrics
- Alerting rules

**Common URLs**:
- Home: http://localhost:3001
- Dashboards: http://localhost:3001/dashboards
- Datasources: http://localhost:3001/datasources
- Alerting: http://localhost:3001/alerting

### Prometheus (Metrics Collection)

**URL**: http://localhost:9090

| URL | Description |
|-----|-------------|
| `/` | Prometheus UI |
| `/graph` | Query and graph metrics |
| `/targets` | Scrape targets status |
| `/alerts` | Active alerts |
| `/config` | Current configuration |
| `/api/v1/query?query=up` | API query example |

**Test Queries**:
```bash
# Check all targets are up
curl 'http://localhost:9090/api/v1/query?query=up'

# Get API request rate
curl 'http://localhost:9090/api/v1/query?query=rate(http_requests_total[5m])'

# Memory usage
curl 'http://localhost:9090/api/v1/query?query=container_memory_usage_bytes'
```

**Common PromQL Queries**:
```promql
# Service health
up{job="meepleai-api"}

# Request rate (last 5 minutes)
rate(http_requests_total[5m])

# Error rate
rate(http_requests_total{status=~"5.."}[5m])

# CPU usage
container_cpu_usage_seconds_total{name="meepleai-api"}

# Memory usage
container_memory_usage_bytes{name="meepleai-api"}
```

### Alertmanager (Alert Management)

**URL**: http://localhost:9093

| URL | Description |
|-----|-------------|
| `/` | Alertmanager UI |
| `/api/v1/alerts` | List active alerts |
| `/api/v1/silences` | List silences |
| `/api/v1/status` | Alertmanager status |

**Test Commands**:
```bash
# View alerts
curl http://localhost:9093/api/v1/alerts

# Create silence (1 hour)
curl -X POST http://localhost:9093/api/v1/silences \
  -H "Content-Type: application/json" \
  -d '{
    "matchers": [
      {"name": "alertname", "value": "HighMemory", "isRegex": false}
    ],
    "startsAt": "2024-01-01T00:00:00Z",
    "endsAt": "2024-01-01T01:00:00Z",
    "createdBy": "admin",
    "comment": "Maintenance window"
  }'
```

### cAdvisor (Container Metrics)

**URL**: http://localhost:8082

**Features**:
- Real-time container resource usage
- Historical usage graphs
- Per-container CPU, memory, network, filesystem metrics

**Test Command**:
```bash
# Container stats
curl http://localhost:8082/api/v1.3/containers

# Docker container metrics
curl http://localhost:8082/api/v1.3/docker/
```

### Node Exporter (Host Metrics)

**URL**: http://localhost:9100/metrics

**Metrics Format**: Prometheus format

**Test Command**:
```bash
# All metrics
curl http://localhost:9100/metrics

# Filter CPU metrics
curl http://localhost:9100/metrics | grep node_cpu
```

### HyperDX (Unified Observability)

**URL**: http://localhost:8180
**OTLP gRPC**: `localhost:14317`
**OTLP HTTP**: `localhost:14318`

**Features**:
- Unified logs, traces, session replay
- Replaces Seq + Jaeger (Issue #1564)
- ClickHouse-based storage
- 30-day retention (configurable)

**Startup**:
```bash
cd infra
docker compose -f docker-compose.yml -f compose.hyperdx.yml --profile observability up -d
```

**Test Command**:
```bash
# Health
curl http://localhost:8180/health

# Send test OTLP trace (HTTP)
curl -X POST http://localhost:14318/v1/traces \
  -H "Content-Type: application/json" \
  -d '{
    "resourceSpans": [{
      "resource": {
        "attributes": [{"key": "service.name", "value": {"stringValue": "test-service"}}]
      },
      "scopeSpans": [{
        "spans": [{
          "traceId": "00000000000000000000000000000001",
          "spanId": "0000000000000001",
          "name": "test-span",
          "kind": 1,
          "startTimeUnixNano": "1609459200000000000",
          "endTimeUnixNano": "1609459201000000000"
        }]
      }]
    }]
  }'
```

**Configuration**:
- Data retention: 30 days (env: `HYPERDX_RETENTION_DAYS`)
- Max storage: 50 GB (env: `HYPERDX_MAX_STORAGE_GB`)
- Alert channels: Configure in UI (email, Slack)

---

## Development Tools

### Mailpit (Email Testing)

**SMTP Server**: `localhost:1025`
**Web UI**: http://localhost:8025
**API**: http://localhost:8025/api/v1/messages

**Features**:
- Catch-all SMTP server
- View sent emails in browser
- REST API for automation
- Supports attachments, HTML emails
- Search and filter

**SMTP Configuration** (for API):
```yaml
# In appsettings.json or docker-compose.yml
Email:
  Host: mailpit
  Port: 1025
  From: noreply@meepleai.dev
  Username: ""  # Not required
  Password: ""  # Not required
```

**Test Email**:
```bash
# Via telnet
telnet localhost 1025
# Then type:
HELO localhost
MAIL FROM: test@example.com
RCPT TO: user@meepleai.dev
DATA
Subject: Test Email
Content-Type: text/plain; charset=utf-8

This is a test email from MeepleAI.
.
QUIT

# Or using swaks (SMTP test tool)
swaks --to user@meepleai.dev \
  --from test@example.com \
  --server localhost:1025 \
  --body "Test email"
```

**API Usage**:
```bash
# Get all messages
curl http://localhost:8025/api/v1/messages

# Get specific message
curl http://localhost:8025/api/v1/message/{ID}

# Delete all messages
curl -X DELETE http://localhost:8025/api/v1/messages

# Search messages
curl "http://localhost:8025/api/v1/search?query=subject:Test"
```

### n8n (Workflow Automation)

**URL**: http://localhost:5678
**Credentials**: Check `infra/secrets/n8n.secret` (`N8N_BASIC_AUTH_USER`, `N8N_BASIC_AUTH_PASSWORD`)
**Default Dev**: `admin` / `n8nadmin`

**Features**:
- Visual workflow builder
- 300+ integrations
- Webhook triggers
- Scheduled tasks
- Database: PostgreSQL (shared with MeepleAI)

**Common Uses**:
- Automated BGG data sync
- Email notifications
- Scheduled reports
- Webhook integrations
- Data transformations

**Webhook Endpoints**:
```
# Webhook URL pattern
http://localhost:5678/webhook/{webhook-path}

# Test webhook (after creating in n8n)
curl -X POST http://localhost:5678/webhook/test \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello from curl"}'
```

---

## API Example Endpoints

### Authentication

**Register**:
```bash
curl -X POST http://localhost:8080/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@meepleai.dev",
    "username": "testuser",
    "password": "SecurePass123!"
  }'
```

**Login** (Get JWT):
```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@meepleai.dev",
    "password": "SecurePass123!"
  }'

# Response includes: { "token": "eyJ...", "expiresAt": "..." }
```

**Get Current User** (Requires JWT):
```bash
TOKEN="your-jwt-token-here"

curl http://localhost:8080/api/v1/users/me \
  -H "Authorization: Bearer $TOKEN"
```

### Game Management

**Search Games**:
```bash
# Search by name
curl "http://localhost:8080/api/v1/shared-games?search=catan&pageSize=10"

# Filter by complexity
curl "http://localhost:8080/api/v1/shared-games?minComplexity=2&maxComplexity=4"

# Filter by players
curl "http://localhost:8080/api/v1/shared-games?minPlayers=2&maxPlayers=4"

# Combined filters
curl "http://localhost:8080/api/v1/shared-games?search=strategy&minPlayers=2&maxPlayers=4&pageSize=20"
```

**Get Game Details**:
```bash
# By BGG ID
curl http://localhost:8080/api/v1/shared-games/11544

# Response includes: name, description, mechanics, categories, player count, etc.
```

**Add to User Library** (Requires JWT):
```bash
TOKEN="your-jwt-token-here"

curl -X POST http://localhost:8080/api/v1/user-library \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "gameId": 11544,
    "ownership": "Owned",
    "purchaseDate": "2024-01-15",
    "notes": "Great game for family nights!"
  }'
```

**Get User Library** (Requires JWT):
```bash
TOKEN="your-jwt-token-here"

curl "http://localhost:8080/api/v1/user-library?pageSize=20" \
  -H "Authorization: Bearer $TOKEN"
```

---

## Health Check Script

**Save as**: `scripts/check-services.ps1` (PowerShell) or `scripts/check-services.sh` (Bash)

### PowerShell Version

```powershell
# check-services.ps1
Write-Host "=== MeepleAI Services Health Check ===" -ForegroundColor Cyan
Write-Host ""

$services = @{
    "Frontend"       = "http://localhost:3000"
    "API Health"     = "http://localhost:8080/health"
    "API Docs"       = "http://localhost:8080/scalar/v1"
    "Grafana"        = "http://localhost:3001"
    "Prometheus"     = "http://localhost:9090/-/healthy"
    "Mailpit"        = "http://localhost:8025"
    "n8n"            = "http://localhost:5678"
    "Embedding"      = "http://localhost:8000/health"
    "Reranker"       = "http://localhost:8003/health"
    "Unstructured"   = "http://localhost:8001/health"
    "SmolDocling"    = "http://localhost:8002/health"
    "Qdrant"         = "http://localhost:6333/collections"
}

foreach ($service in $services.GetEnumerator()) {
    try {
        $response = Invoke-WebRequest -Uri $service.Value -Method Get -TimeoutSec 5 -UseBasicParsing
        $status = $response.StatusCode
        $color = if ($status -eq 200) { "Green" } else { "Yellow" }
        Write-Host ("{0,-20} {1}" -f $service.Key, $status) -ForegroundColor $color
    }
    catch {
        Write-Host ("{0,-20} DOWN" -f $service.Key) -ForegroundColor Red
    }
}

# Database checks (require docker exec)
Write-Host ""
Write-Host "=== Storage Services ===" -ForegroundColor Cyan

# PostgreSQL
try {
    $pgCheck = docker exec meepleai-postgres pg_isready -U postgres 2>&1
    if ($pgCheck -like "*accepting connections*") {
        Write-Host "PostgreSQL           ✅ OK" -ForegroundColor Green
    } else {
        Write-Host "PostgreSQL           ❌ DOWN" -ForegroundColor Red
    }
}
catch {
    Write-Host "PostgreSQL           ❌ DOWN" -ForegroundColor Red
}

# Redis
try {
    $redisPassword = (Get-Content infra/secrets/redis.secret | Select-String "REDIS_PASSWORD").Line.Split('=')[1]
    $redisCheck = docker exec meepleai-redis redis-cli -a $redisPassword PING 2>&1
    if ($redisCheck -eq "PONG") {
        Write-Host "Redis                ✅ OK" -ForegroundColor Green
    } else {
        Write-Host "Redis                ❌ DOWN" -ForegroundColor Red
    }
}
catch {
    Write-Host "Redis                ❌ DOWN" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Legend ===" -ForegroundColor Cyan
Write-Host "200 = OK | 404 = Not Found | DOWN = Service Unreachable"
```

**Usage**:
```powershell
pwsh scripts/check-services.ps1
```

---

## Quick Reference Table

| Service Category | Port(s) | Protocol | Access |
|------------------|---------|----------|--------|
| **Web UI** | 3000 | HTTP | Browser |
| **API** | 8080 | HTTP | REST |
| **PostgreSQL** | 5432 | TCP | psql/App |
| **Redis** | 6379 | TCP | redis-cli/App |
| **Qdrant** | 6333, 6334 | HTTP, gRPC | REST/gRPC |
| **Embedding** | 8000 | HTTP | REST |
| **Reranker** | 8003 | HTTP | REST |
| **Unstructured** | 8001 | HTTP | REST |
| **SmolDocling** | 8002 | HTTP | REST |
| **Ollama** | 11434 | HTTP | REST |
| **Grafana** | 3001 | HTTP | Browser |
| **Prometheus** | 9090 | HTTP | Browser |
| **Alertmanager** | 9093 | HTTP | Browser |
| **cAdvisor** | 8082 | HTTP | Browser |
| **Node Exporter** | 9100 | HTTP | Metrics |
| **HyperDX UI** | 8180 | HTTP | Browser |
| **HyperDX OTLP gRPC** | 14317 | gRPC | App |
| **HyperDX OTLP HTTP** | 14318 | HTTP | App |
| **Mailpit SMTP** | 1025 | SMTP | App |
| **Mailpit UI** | 8025 | HTTP | Browser |
| **n8n** | 5678 | HTTP | Browser |

---

## Additional Resources

- **Quick Start**: [quick-start.md](./quick-start.md)
- **Clean Builds**: [clean-builds.md](./clean-builds.md)
- **Common Commands**: [common-commands.md](./common-commands.md)
- **Troubleshooting**: [troubleshooting.md](./troubleshooting.md)
- **Full Guide**: [../local-environment-startup-guide.md](../local-environment-startup-guide.md)

---

**Last Updated**: 2026-02-02
**Maintainer**: MeepleAI Development Team

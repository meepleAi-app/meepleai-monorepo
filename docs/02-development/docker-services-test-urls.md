                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    # MeepleAI Docker Services - Test URLs

**Generated**: 2026-01-15
**Purpose**: Comprehensive list of URLs to test all Docker services

---

## Web Interfaces (Browser Access)

### Frontend & API

| Service         | URL                             | Description                   |
| --------------- | ------------------------------- | ----------------------------- |
| **Web App**     | http://localhost:3000           | Next.js frontend application  |
| **API Swagger** | http://localhost:8080/scalar/v1 | API documentation (Scalar UI) |
| **API Health**  | http://localhost:8080/health    | API health check endpoint     |

### Monitoring & Observability

| Service          | URL                   | Credentials   | Description                          |
| ---------------- | --------------------- | ------------- | ------------------------------------ |
| **Grafana**      | http://localhost:3001 | admin / admin | Dashboards and metrics visualization |
| **Prometheus**   | http://localhost:9090 | -             | Metrics collection and queries       |
| **Alertmanager** | http://localhost:9093 | -             | Alert management interface           |
| **cAdvisor**     | http://localhost:8082 | -             | Container resource metrics           |

### Development Tools

| Service     | URL                   | Credentials | Description                                |
| ----------- | --------------------- | ----------- | ------------------------------------------ |
| **Mailpit** | http://localhost:8025 | -           | Email testing interface (view sent emails) |
| **n8n**     | http://localhost:5678 | -           | Workflow automation interface              |

---

## AI Services Health Endpoints

### Python Microservices

| Service                  | Health URL                   | Test Endpoint                            | Description                          |
| ------------------------ | ---------------------------- | ---------------------------------------- | ------------------------------------ |
| **Embedding Service**    | http://localhost:8000/health | `POST http://localhost:8000/embed`       | Sentence transformers for embeddings |
| **Unstructured Service** | http://localhost:8001/health | `POST http://localhost:8001/process-pdf` | PDF processing and extraction        |
| **SmolDocling Service**  | http://localhost:8002/health | `POST http://localhost:8002/extract`     | Document intelligence extraction     |
| **Reranker Service**     | http://localhost:8003/health | `POST http://localhost:8003/rerank`      | Cross-encoder reranking              |

### Ollama (LLM)

| URL                                | Description           |
| ---------------------------------- | --------------------- |
| http://localhost:11434             | Ollama API base       |
| http://localhost:11434/api/tags    | List available models |
| http://localhost:11434/api/version | Ollama version        |

---

## Vector & Storage Services

### Qdrant (Vector Database)

| URL                               | Description                |
| --------------------------------- | -------------------------- |
| http://localhost:6333             | Qdrant HTTP API            |
| http://localhost:6333/collections | List collections           |
| http://localhost:6333/dashboard   | Qdrant Web UI (if enabled) |

### PostgreSQL (Database)

**Connection**: `localhost:5432`
**Test Command**:

```bash
docker exec -it meepleai-postgres psql -U meepleai -d meepleai -c "SELECT version();"
```

### Redis (Cache)

**Connection**: `localhost:6379`
**Test Command**:

```bash
# Get password from secrets
REDIS_PASSWORD=$(cat infra/secrets/redis-password.txt | tr -d '\n\r')
docker exec -it meepleai-redis redis-cli -a "$REDIS_PASSWORD" PING

# Alternative: Direct command (replace PASSWORD with value from secrets)
# docker exec -it meepleai-redis redis-cli -a PASSWORD PING
```

---

## API Endpoints - Authentication & Authorization

### Register New User

```bash
curl -X POST http://localhost:8080/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "username": "testuser",
    "password": "SecurePass123!"
  }'
```

### Login (Get JWT Token)

```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@meepleai.dev",
    "password": "Test123!"
  }'
```

### Get Current User (Requires Auth)

```bash
TOKEN="your-jwt-token-here"
curl http://localhost:8080/api/v1/users/me \
  -H "Authorization: Bearer $TOKEN"
```

---

## API Endpoints - Game Management

### Search Shared Games

```bash
curl "http://localhost:8080/api/v1/shared-games?search=catan&pageSize=10"
```

### Get Game Details

```bash
curl http://localhost:8080/api/v1/shared-games/11544
```

### Add Game to User Library (Requires Auth)

```bash
TOKEN="your-jwt-token-here"
curl -X POST http://localhost:8080/api/v1/user-library \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "gameId": 11544,
    "ownership": "Owned",
    "purchaseDate": "2024-01-15",
    "notes": "Great game!"
  }'
```

### Get User Library (Requires Auth)

```bash
TOKEN="your-jwt-token-here"
curl "http://localhost:8080/api/v1/user-library?pageSize=20" \
  -H "Authorization: Bearer $TOKEN"
```

---

## Monitoring & Metrics Endpoints

### Prometheus Metrics

| URL                                         | Description             |
| ------------------------------------------- | ----------------------- |
| http://localhost:9090/api/v1/query?query=up | Check service status    |
| http://localhost:9090/targets               | View all scrape targets |
| http://localhost:9090/alerts                | View active alerts      |

### Node Exporter (Host Metrics)

| URL                           | Description               |
| ----------------------------- | ------------------------- |
| http://localhost:9100/metrics | Prometheus format metrics |

### API Metrics (if exposed)

```bash
curl http://localhost:8080/metrics
```

---

## Alert Testing

### Alertmanager - View Alerts

```bash
curl http://localhost:9093/api/v1/alerts
```

### Alertmanager - Silence Alert

```bash
curl -X POST http://localhost:9093/api/v1/silences \
  -H "Content-Type: application/json" \
  -d '{
    "matchers": [{"name": "alertname", "value": "InstanceDown", "isRegex": false}],
    "startsAt": "2024-01-01T00:00:00Z",
    "endsAt": "2024-12-31T23:59:59Z",
    "createdBy": "admin",
    "comment": "Silencing for maintenance"
  }'
```

---

## Email Testing (Mailpit)

### Send Test Email via SMTP

```bash
# Requires mail command or telnet
telnet localhost 1025
# Then type:
# HELO localhost
# MAIL FROM: test@example.com
# RCPT TO: user@example.com
# DATA
# Subject: Test Email
#
# This is a test email
# .
# QUIT
```

### View Sent Emails

Open http://localhost:8025 in browser to see all sent emails.

---

## Health Check Summary Script

**Save as**: `scripts/test-services.sh`

```bash
#!/bin/bash

echo "=== MeepleAI Services Health Check ==="
echo ""

# Web services with UI
echo "Frontend:      $(curl -s -o /dev/null -w '%{http_code}' http://localhost:3000)"
echo "API Health:    $(curl -s -o /dev/null -w '%{http_code}' http://localhost:8080/health)"
echo "Grafana:       $(curl -s -o /dev/null -w '%{http_code}' http://localhost:3001)"
echo "Prometheus:    $(curl -s -o /dev/null -w '%{http_code}' http://localhost:9090/-/healthy)"
echo "Mailpit:       $(curl -s -o /dev/null -w '%{http_code}' http://localhost:8025)"
echo "n8n:           $(curl -s -o /dev/null -w '%{http_code}' http://localhost:5678)"

# AI services
echo ""
echo "=== AI Services ==="
echo "Embedding:     $(curl -s -o /dev/null -w '%{http_code}' http://localhost:8000/health)"
echo "Unstructured:  $(curl -s -o /dev/null -w '%{http_code}' http://localhost:8001/health)"
echo "SmolDocling:   $(curl -s -o /dev/null -w '%{http_code}' http://localhost:8002/health)"
echo "Reranker:      $(curl -s -o /dev/null -w '%{http_code}' http://localhost:8003/health)"
echo "Ollama:        $(curl -s -o /dev/null -w '%{http_code}' http://localhost:11434/api/tags)"

# Vector DB
echo ""
echo "=== Storage Services ==="
echo "Qdrant:        $(curl -s -o /dev/null -w '%{http_code}' http://localhost:6333/collections)"

# Database (requires docker exec)
POSTGRES_STATUS=$(docker exec meepleai-postgres pg_isready -U meepleai 2>&1)
if [[ $POSTGRES_STATUS == *"accepting connections"* ]]; then
  echo "PostgreSQL:    ✅ OK"
else
  echo "PostgreSQL:    ❌ DOWN"
fi

# Redis (requires docker exec)
REDIS_PASSWORD=$(cat infra/secrets/redis-password.txt 2>/dev/null | tr -d '\n\r')
REDIS_STATUS=$(docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" PING 2>&1)
if [[ $REDIS_STATUS == "PONG" ]]; then
  echo "Redis:         ✅ OK"
else
  echo "Redis:         ❌ DOWN"
fi

echo ""
echo "=== Legend ==="
echo "200 = OK | 404 = Not Found | 000 = Service Down"
```

**Usage**:

```bash
chmod +x scripts/test-services.sh
./scripts/test-services.sh
```

---

## Quick Testing Checklist

- [ ] **Frontend**: Open http://localhost:3000
- [ ] **API Docs**: Open http://localhost:8080/scalar/v1
- [ ] **Grafana**: Open http://localhost:3001 (login: admin/admin)
- [ ] **Mailpit**: Open http://localhost:8025 (view test emails)
- [ ] **Prometheus**: Open http://localhost:9090/targets (check all UP)
- [ ] **Register User**: `POST /api/v1/auth/register`
- [ ] **Login**: `POST /api/v1/auth/login` (get JWT)
- [ ] **Search Games**: `GET /api/v1/shared-games?search=catan`
- [ ] **Add to Library**: `POST /api/v1/user-library` (with JWT)
- [ ] **Check Alerts**: Open http://localhost:9093
- [ ] **Run Health Script**: `./scripts/test-services.sh`

---

## Troubleshooting

### Service Not Responding

```bash
# Check if container is running
docker ps | grep <service-name>

# Check logs
docker logs meepleai-<service-name>

# Restart service
docker restart meepleai-<service-name>
```

### Database Connection Issues

```bash
# Check PostgreSQL logs
docker logs meepleai-postgres

# Verify password in launchSettings.json matches secrets/postgres-password.txt
```

### Redis Connection Issues

```bash
# Test Redis connection (get password from secrets)
REDIS_PASSWORD=$(cat infra/secrets/redis-password.txt | tr -d '\n\r')
docker exec -it meepleai-redis redis-cli -a "$REDIS_PASSWORD" PING

# Should return: PONG
```

---

**Last Updated**: 2026-01-15
**Maintainer**: MeepleAI DevOps Team

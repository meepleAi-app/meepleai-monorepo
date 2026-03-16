# Log Generation & Observability Test Plan

**Purpose**: Comprehensive testing strategy for log generation, visualization, and distributed tracing across all user flows.

**Date**: 2026-01-12
**Status**: Draft
**Coverage Target**: All critical user journeys with observability validation

---

## Test Infrastructure

### Services Required
- ✅ **Backend API**: http://localhost:8080 (Serilog → Loki)
- ✅ **Frontend Web**: http://localhost:3000 (Next.js logging)
- ✅ **PostgreSQL**: localhost:5432 (query logs)
- ✅ **Redis**: localhost:6379 (operation logs)
- ✅ **Qdrant**: localhost:6333 (vector operations)
- ✅ **Prometheus**: http://localhost:9090 (metrics)
- ✅ **Grafana**: http://localhost:3001 (dashboards)
- ⏳ **n8n**: http://localhost:5678 (workflow logs)

### Observability Stack
```yaml
Logs:      Serilog → Console/File → Loki (via Fluent Bit)
Metrics:   OpenTelemetry → Prometheus → Grafana
Traces:    OpenTelemetry → Distributed tracing
Health:    /health endpoint with component checks
```

---

## Test Suites

### 1. Authentication Flow Logs
**Objective**: Validate comprehensive logging for auth operations with security context

#### 1.1 User Registration
```bash
# Test: POST /api/v1/auth/register
curl -X POST http://localhost:8080/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test-logs-1@example.com",
    "username": "testuser1",
    "password": "Test123!@#"
  }'
```

**Expected Logs**:
- `[INF] RegisterCommand received for email: test-logs-1@example.com`
- `[INF] User registered successfully: userId={guid}`
- `[DBG] Password hashed using PBKDF2`
- `[INF] Initial admin check performed`

**Validation**:
- ✅ No password in logs (security)
- ✅ User ID generated and logged
- ✅ Success/failure clearly indicated
- ✅ Timestamp accuracy

#### 1.2 Login Flow
```bash
# Test: POST /api/v1/auth/login
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test-logs-1@example.com",
    "password": "Test123!@#"
  }'
```

**Expected Logs**:
- `[INF] LoginCommand received for email: test-logs-1@example.com`
- `[DBG] Password verification started`
- `[INF] User authenticated successfully: userId={guid}`
- `[INF] Session created: sessionId={guid}`
- `[DBG] Cookie set with httpOnly, secure flags`

**Validation**:
- ✅ Password not logged
- ✅ Session ID tracked
- ✅ Security flags logged
- ✅ Failed attempts logged with rate limiting

#### 1.3 Logout Flow
```bash
# Test: POST /api/v1/auth/logout
curl -X POST http://localhost:8080/api/v1/auth/logout \
  -H "Cookie: session={session_cookie}"
```

**Expected Logs**:
- `[INF] LogoutCommand received for userId={guid}`
- `[INF] Session invalidated: sessionId={guid}`
- `[DBG] Cookie cleared`

---

### 2. Game Catalog Operations Logs
**Objective**: Track game browsing, search, and BGG integration logs

#### 2.1 Browse Games
```bash
# Test: GET /api/v1/games
curl http://localhost:8080/api/v1/games
```

**Expected Logs**:
- `[INF] GetAllGamesQuery executed`
- `[DBG] Retrieved {count} games from database`
- `[DBG] Query execution time: {ms}ms`
- `[DBG] Cache hit/miss status`

#### 2.2 BGG Search
```bash
# Test: BGG search via Next.js proxy
curl "http://localhost:3000/api/v1/bgg/search?query=Catan"
```

**Expected Logs**:
- `[INF] BGG search initiated: query=Catan`
- `[DBG] BGG API request with token`
- `[DBG] BGG API response: {count} results, {ms}ms`
- `[INF] BGG search completed: {results} games found`

---

### 3. PDF Processing Pipeline Logs
**Objective**: Validate 3-stage PDF extraction with fallback logging

#### 3.1 PDF Upload
```bash
# Test: POST /api/v1/pdfs/upload
curl -X POST http://localhost:8080/api/v1/pdfs/upload \
  -H "Authorization: Bearer {api_key}" \
  -F "file=@test-rulebook.pdf"
```

**Expected Logs**:
- `[INF] PDF upload started: filename=test-rulebook.pdf`
- `[DBG] File validated: PDF format confirmed`
- `[INF] PDF document created: documentId={guid}`
- `[INF] Starting PDF extraction: extractor=Orchestrator`
- `[DBG] Stage 1: Unstructured extraction, confidence={score}`
- `[INF] PDF extraction completed: {chunks} chunks generated`

---

### 4. RAG Chat Interaction Logs
**Objective**: Track retrieval, reranking, LLM streaming

#### 4.1 Chat Question
```bash
# Test: POST /api/v1/chat (SSE)
curl -N -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {api_key}" \
  -d '{
    "gameId": 1,
    "threadId": null,
    "question": "How do I win?"
  }'
```

**Expected Logs**:
- `[INF] AskQuestionCommand received: gameId=1`
- `[DBG] Vector search in Qdrant: {count} results`
- `[DBG] Keyword search in PostgreSQL: {count} results`
- `[INF] RRF fusion: {count} combined results`
- `[DBG] Reranking with reranker service`
- `[INF] LLM request: provider=OpenRouter, model=claude-3.5-sonnet`
- `[INF] SSE streaming started`
- `[INF] LLM response completed: tokens={count}, cost=${amount}`
- `[INF] Validation: confidence={score}, hallucination={rate}%`

---

### 5. Error Logging Validation
**Objective**: Ensure errors captured with full context

#### 5.1 Validation Error
```bash
# Test: Invalid input
curl -X POST http://localhost:8080/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email": "invalid", "password": "weak"}'
```

**Expected Logs**:
- `[WRN] Validation failed: RegisterCommand`
- `[DBG] Validation errors: email=Invalid, password=Too weak`
- `[INF] 400 Bad Request returned`

---

## Execution Commands

### Monitor API Logs
```bash
docker logs -f meepleai-api | grep -E "INF|WRN|ERR"
```

### Query Prometheus Metrics
```bash
# HTTP request rate
curl 'http://localhost:9090/api/v1/query?query=rate(http_requests_total[5m])'

# Database connection pool
curl 'http://localhost:9090/api/v1/query?query=npgsql_connection_pool_connections'
```

### Access Dashboards
- Grafana: http://localhost:3001 (admin/admin)
- Prometheus: http://localhost:9090
- n8n Workflows: http://localhost:5678

---

**Status**: Ready for execution
**Last Updated**: 2026-01-12
**Next Action**: Execute Phase 1 manual tests

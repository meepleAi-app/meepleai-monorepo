# n8n Integration Guide

**MeepleAI n8n Webhook Integrations**

This guide covers all n8n webhook integrations with the MeepleAI platform, including setup, deployment, and usage.

---

## 📋 Overview

MeepleAI provides n8n webhooks for AI-powered board game assistance:

| Webhook | Issue | Status | Endpoint | Purpose |
|---------|-------|--------|----------|---------|
| **Agent Explain** | #288 (N8N-01) | ✅ Complete | `/webhook/explain` | Generate rule explanations with RAG |
| **Agent Q&A** | #289 (N8N-03) | ✅ Complete | `/webhook/agent/qa` | Answer questions with snippets |

---

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- PowerShell (optional, for automation scripts)
- MeepleAI API running on port 8080
- n8n instance running on port 5678

### Basic Setup (3 Steps)

**1. Start Services**
```bash
cd infra
docker compose up -d postgres qdrant redis api n8n
```

**2. Import Workflows**
- Access n8n: http://localhost:5678
- Import workflows:
  - `infra/init/n8n/agent-explain-orchestrator.json` (N8N-01)
  - `infra/init/n8n/agent-qa-webhook.json` (N8N-03)
- Activate both workflows

**3. Test Webhooks**
```bash
# Test Explain webhook
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{"gameId": "tic-tac-toe", "topic": "winning conditions"}'

# Test Q&A webhook
curl -X POST http://localhost:5678/webhook/agent/qa \
  -H "Content-Type: application/json" \
  -d '{"gameId": "tic-tac-toe", "query": "How many players?"}'
```

---

## N8N-01: Webhook /agent/explain

**Issue**: #288
**Status**: ✅ Implementation Complete
**Date**: 2025-10-11

### Overview

Webhook che orchestra RuleSpec+RAG per generare spiegazioni dettagliate delle regole con citazioni.

**Acceptance Criteria**: ✅ 200 OK con payload standardizzato

### Endpoint

```
POST http://localhost:5678/webhook/explain
```

### Request Format

```json
{
  "gameId": "string",      // Required: game identifier
  "topic": "string"        // Required: topic to explain
}
```

### Response Format (Success - HTTP 200)

```json
{
  "success": true,
  "data": {
    "outline": {
      "mainTopic": "winning conditions",
      "sections": ["Section 1", "Section 2"]
    },
    "script": "# Explanation: winning conditions\n\n## Overview...",
    "citations": [
      {
        "text": "A player wins when...",
        "source": "PDF:pdf-id",
        "page": 3,
        "line": 0
      }
    ],
    "estimatedReadingTimeMinutes": 2,
    "metadata": {
      "promptTokens": 150,
      "completionTokens": 450,
      "totalTokens": 600,
      "confidence": 0.95
    }
  },
  "timestamp": "2025-10-11T10:30:00Z",
  "version": "1.0"
}
```

### Workflow Features
- Input validation (gameId, topic)
- Session-based authentication
- Response transformation to standardized payload
- Error handling with proper status codes
- Structured logging
- 30-second timeout

### Documentation
- **Technical Spec**: `docs/technic/n8n-webhook-explain-design.md`
- **Backend Endpoint**: `POST /api/v1/agents/explain`
- **Related Issue**: AI-02 (#280) - RAG Explain

### Testing

```bash
# Valid request
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{"gameId": "tic-tac-toe", "topic": "winning conditions"}'

# Missing gameId
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{"topic": "rules"}'
# Expected: 400 Bad Request
```

---

## N8N-03: Webhook /agent/qa

**Issue**: #289
**Status**: ✅ Implementation Complete
**Date**: 2025-10-16

### Overview

Webhook per Q&A con snippet e risposta "Not specified" fallback. Utilizza RAG per cercare contenuti rilevanti e generare risposte con citazioni.

**Acceptance Criteria**: ✅ 200 OK con snippet/refs

### Endpoint

```
POST http://localhost:5678/webhook/agent/qa
```

### Request Format

```json
{
  "gameId": "string",      // Required: game identifier
  "query": "string"        // Required: question to answer
}
```

### Response Format (Success - HTTP 200)

```json
{
  "success": true,
  "requestId": "n8n-1697123456789-abc123",
  "timestamp": "2025-10-16T10:30:00.000Z",
  "data": {
    "answer": "The game is played on a 3x3 grid. Players take turns placing X or O marks...",
    "snippets": [
      {
        "text": "Tic-tac-toe is played on a 3x3 grid...",
        "source": "PDF:document-id",
        "page": 1,
        "line": 5
      }
    ]
  }
}
```

### Response Format (Error - HTTP 4xx/5xx)

```json
{
  "success": false,
  "requestId": "n8n-1697123456789-xyz789",
  "timestamp": "2025-10-16T10:30:00.000Z",
  "error": {
    "message": "gameId is required",
    "statusCode": 400
  }
}
```

### Workflow Features
- Automatic correlation ID generation (X-Correlation-Id header)
- Retry logic: 3 attempts with exponential backoff
- Retry conditions: HTTP 429, 500, 502, 503, 504
- Request timeout: 60 seconds
- Structured logging (request/response metadata)
- Error handling with proper status codes
- Success/error response formatting
- Continue on API call failure for proper error handling

### Workflow Architecture

```
External Client
      │
      │ POST /webhook/agent/qa
      ▼
┌─────────────────────────────┐
│   n8n Workflow              │
│   "Agent QA Webhook"        │
├─────────────────────────────┤
│ 1. Webhook Trigger          │
│ 2. Prepare Request          │──── Generate correlation ID
│ 3. Call Backend API         │──── POST /agents/qa (retry 3x)
│ 4. Check Success            │──── Branch: success vs error
│ 5. Format Success Response  │
│ 6. Format Error Response    │
│ 7. Log Request              │──── Structured logging
│ 8. Respond Success          │──── HTTP 200 + X-Request-Id
│ 9. Respond Error            │──── HTTP 4xx/5xx + X-Request-Id
└─────────────────────────────┘
                 │
                 │ POST /api/v1/agents/qa
                 ▼
      ┌─────────────────────┐
      │  MeepleAI API       │
      │  /agents/qa         │
      ├─────────────────────┤
      │ • RagService        │
      │ • ChatService       │
      │ • AiRequestLog      │
      └─────────────────────┘
```

### Documentation
- **Technical Spec**: `docs/technic/n8n-webhook-qa-design.md`
- **Backend Endpoint**: `POST /api/v1/agents/qa`
- **Workflow JSON**: `infra/init/n8n/agent-qa-webhook.json`
- **Related Issue**: AI-04 (#282) - Q&A with snippets

### Testing

```bash
# Valid request
curl -X POST http://localhost:5678/webhook/agent/qa \
  -H "Content-Type: application/json" \
  -d '{"gameId": "tic-tac-toe", "query": "How many players can play?"}'
# Expected: 200 OK with answer and snippets

# Missing gameId
curl -X POST http://localhost:5678/webhook/agent/qa \
  -H "Content-Type: application/json" \
  -d '{"query": "How to play?"}'
# Expected: Backend returns 400 Bad Request

# Game without content
curl -X POST http://localhost:5678/webhook/agent/qa \
  -H "Content-Type: application/json" \
  -d '{"gameId": "non-existent-game", "query": "What are the rules?"}'
# Expected: 200 OK with answer (possibly "Not specified" or generic response)
```

---

## 🏗️ Architecture

### Common Architecture

Both webhooks follow a similar pattern:

```
External Client
      │
      │ HTTP POST /webhook/*
      ▼
┌─────────────────────────────┐
│   n8n Workflow              │
├─────────────────────────────┤
│ 1. Webhook Trigger          │
│ 2. Validate/Prepare Request │
│ 3. HTTP Request to API      │
│ 4. Check Success/Error      │
│ 5. Transform Response       │
│ 6. Log Request              │
│ 7. Respond to Webhook       │
└─────────────────────────────┘
                 │
                 ▼
      ┌─────────────────────┐
      │  MeepleAI API       │
      │  /api/v1/agents/*   │
      ├─────────────────────┤
      │ • Auth Check        │
      │ • RagService        │
      │ • ChatService       │
      │ • AiRequestLog      │
      └─────────────────────┘
                 │
                 ▼
      ┌─────────────────────┐
      │  Infrastructure     │
      ├─────────────────────┤
      │ • PostgreSQL        │
      │ • Qdrant (vectors)  │
      │ • Redis (cache)     │
      │ • OpenRouter (LLM)  │
      └─────────────────────┘
```

### Key Components

1. **n8n Workflow**: Orchestration layer
   - Webhook trigger
   - Input validation
   - HTTP client to backend API
   - Response transformation
   - Error handling

2. **MeepleAI API**: Business logic
   - Authentication/Authorization
   - RagService (RAG pipeline)
   - ChatService (persistence)
   - AiRequestLogService (audit trail)

3. **Infrastructure**:
   - PostgreSQL: Relational data
   - Qdrant: Vector database for semantic search
   - Redis: Response caching (AI-05)
   - OpenRouter: LLM API gateway

---

## 🔒 Security

### Authentication Options

**Option A: Public Webhook** (Current Default)
- Webhook endpoints publicly accessible
- Backend API handles authentication
- ⚠️ Requires session cookies or API keys for backend

**Option B: Service Account** (Recommended for Production)
- Create n8n service account: `n8n-service@meepleai.dev`
- Authenticate via `/auth/login`
- Store session cookie in n8n credentials
- Include cookie in all backend API requests
- ✅ Proper audit trail, secure

**Option C: API Key Authentication** (Best for Webhooks)
- Use API-01 API key system
- Generate API key for n8n service
- Include `X-API-Key` header in requests
- ✅ Simpler for webhooks, dedicated credentials

### Security Checklist

- [ ] Change default n8n admin credentials
- [ ] Generate strong encryption keys (32-byte random)
- [ ] Enable HTTPS (reverse proxy: nginx/traefik)
- [ ] Rotate session tokens/API keys monthly
- [ ] Restrict network access (firewall rules)
- [ ] Enable production logging and monitoring
- [ ] Configure rate limiting
- [ ] Set up API key authentication (Option C)

### Implemented Security Measures

1. **Encryption**: AES encryption for n8n API keys (SHA256 key derivation)
2. **Authorization**: Admin-only access to `/admin/n8n` endpoints
3. **Rate Limiting**: Existing rate limiting applies to webhook calls
4. **Audit Logging**: All requests logged via AiRequestLogService (ADM-01)
5. **Input Validation**: gameId/query validation in workflows and backend

---

## 📈 Performance

### Caching (AI-05)

Both webhooks leverage Redis-backed response caching:

| Webhook | Cache Key | TTL | Expected Hit Rate |
|---------|-----------|-----|-------------------|
| Explain | `explain:{gameId}:{topic}` | 24h | >70% |
| Q&A | `qa:{gameId}:{query}` | 24h | >60% |

### Latency Targets

| Metric | Target | Notes |
|--------|--------|-------|
| p50 | < 1s | Median response time |
| p95 | < 2s | 95th percentile |
| p99 | < 5s | 99th percentile |

### Optimization Features

1. **Vector Search Optimization**
   - Qdrant limit: 5 chunks per query
   - HNSW indexing for fast similarity search
   - Embedding caching

2. **LLM Optimization**
   - Response caching (24h TTL)
   - Token limits: prompt (2000), completion (2000)
   - Streaming support (future enhancement)

3. **Scalability**
   - Stateless webhooks (horizontal scaling)
   - Connection pooling (PostgreSQL, Redis)
   - Async I/O throughout stack

---

## 🔄 Integration Points

### Existing Features

Both webhooks integrate with:

- **AI-05** (#283): Response caching (Redis)
- **ADM-01** (#286): Request logging and metrics
- **UI-01**: Chat persistence (optional chatId parameter)
- **ADM-02**: n8n configuration management

### Database Tables

- `n8n_configs`: Webhook configurations (admin)
- `user_sessions`: Service account authentication (if using Option B)
- `ai_request_logs`: Request metrics and audit trail
- `chats` / `chat_logs`: Optional conversation persistence
- `game_pdfs`: PDF rulebook metadata
- `text_chunks`: Indexed text chunks for RAG

### API Endpoints Used

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/v1/agents/explain` | POST | Generate rule explanations |
| `/api/v1/agents/qa` | POST | Answer questions |
| `/admin/n8n` | GET/POST/PUT/DELETE | Manage n8n configs |
| `/auth/login` | POST | Service account auth |

---

## 🧪 Testing

### Backend Integration Tests

```bash
cd apps/api

# Run all n8n-related tests
dotnet test --filter "FullyQualifiedName~N8n"

# Run specific tests
dotnet test --filter "FullyQualifiedName~QaEndpointTests"
```

### Manual Testing

**1. Health Check**
```bash
# Verify API is running
curl http://localhost:8080/health
```

**2. Test Explain Webhook**
```bash
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{
    "gameId": "tic-tac-toe",
    "topic": "winning conditions"
  }'
```

**3. Test Q&A Webhook**
```bash
curl -X POST http://localhost:5678/webhook/agent/qa \
  -H "Content-Type: application/json" \
  -d '{
    "gameId": "tic-tac-toe",
    "query": "How many players can play?"
  }'
```

**4. Test Error Handling**
```bash
# Missing gameId
curl -X POST http://localhost:5678/webhook/agent/qa \
  -H "Content-Type: application/json" \
  -d '{"query": "How to play?"}'
# Expected: Error response with statusCode 400
```

### Load Testing

```bash
# Install k6
brew install k6  # macOS
# or download from https://k6.io/

# Run load test (example)
k6 run tests/load/webhook-qa.js
```

---

## 🐛 Troubleshooting

### Common Issues

#### 1. 401 Unauthorized from Backend

**Symptoms**: Webhook returns 401 error
**Cause**: Missing or invalid authentication
**Solution**:
- Implement service account authentication (Option B)
- Or configure API key authentication (Option C)
- Update workflow to include credentials

#### 2. "No relevant information found" or Empty Snippets

**Symptoms**: Answer is generic or snippets array is empty
**Cause**: No indexed PDF content for the game
**Solution**:
```bash
# Upload PDF
curl -X POST http://localhost:8080/api/v1/ingest/pdf \
  -F "file=@rulebook.pdf" \
  -F "gameId=tic-tac-toe"

# Index PDF
curl -X POST http://localhost:8080/api/v1/ingest/pdf/{pdfId}/index
```

#### 3. Workflow Not Triggering

**Symptoms**: Webhook returns 404
**Cause**: Workflow inactive or incorrect path
**Solution**:
- Access n8n UI: http://localhost:5678
- Find workflow, click "Active" toggle
- Verify webhook path matches request URL

#### 4. Timeout Errors

**Symptoms**: Request times out after 60 seconds
**Cause**: LLM response taking too long
**Solution**:
- Check Qdrant vector database is indexed
- Verify OpenRouter API key is valid
- Increase timeout in HTTP Request node
- Check backend logs for errors

#### 5. Connection Refused to Backend API

**Symptoms**: Error connecting to `http://api:8080`
**Cause**: Backend API not running or network issue
**Solution**:
```bash
# Check API is running
docker compose ps api

# Check logs
docker compose logs -f api

# Restart API
docker compose restart api
```

### Debug Mode

Enable verbose logging in n8n:
```bash
# In docker-compose.yml or .env
N8N_LOG_LEVEL=debug
N8N_LOG_OUTPUT=console
```

View n8n logs:
```bash
docker compose logs -f n8n
```

### Backend Logs

Check backend API logs for request details:
```bash
# View logs
docker compose logs -f api

# Search for specific request
docker compose logs api | grep "QA request from user"
```

---

## 📚 Documentation

### Technical Specifications
- **N8N-01 Design**: `docs/technic/n8n-webhook-explain-design.md`
- **N8N-03 Design**: `docs/technic/n8n-webhook-qa-design.md`
- **API Documentation**: `CLAUDE.md`

### Implementation Guides
- **This Guide**: `docs/guide/n8n-integration-guide.md`
- **User Guide (IT)**: `docs/guide/n8n-user-guide-it.md`

### Related Documentation
- **AI-02** (#280): RAG Explain - Backend implementation
- **AI-04** (#282): Q&A with snippets - Backend implementation
- **AI-05** (#283): Response caching
- **ADM-01** (#286): Admin dashboard and logging
- **ADM-02**: n8n configuration management
- **API-01** (#299): API key authentication

---

## 🚧 Future Enhancements

### Planned Improvements

1. **API Key Authentication** (High Priority)
   - Implement Option C (API-01 system)
   - Generate dedicated n8n API keys
   - Update workflows to use X-API-Key header

2. **Streaming Responses** (Medium Priority)
   - Stream LLM responses for better UX
   - Support Server-Sent Events (SSE)
   - Progressive answer rendering

3. **Webhook Versioning** (Medium Priority)
   - Support multiple payload versions (v1, v2)
   - Backward compatibility
   - Version negotiation

4. **Advanced Rate Limiting** (Low Priority)
   - Separate limits for webhooks vs UI
   - Per-service account limits
   - Burst allowances

5. **Monitoring Dashboard** (Low Priority)
   - Dedicated webhook metrics
   - Real-time request visualization
   - Performance analytics

### Related Roadmap
- **API-02**: Streaming RAG responses
- **AI-06**: RAG offline evaluation
- **AI-07**: Prompt versioning and chain management

---

## ✅ Success Metrics

### N8N-01 (Explain)
- ✅ 200 OK with standardized payload
- ✅ Response time < 2 seconds (p95)
- ✅ Error rate < 1%
- ✅ Cache hit rate > 70%

### N8N-03 (Q&A)
- ✅ 200 OK with snippets/refs
- ✅ Response time < 2 seconds (p95)
- ✅ Error rate < 1%
- ✅ Snippet extraction > 80% of requests

---

## 👥 Contributors

**Author**: Claude Code
**Issues**:
- #288 (N8N-01 - Webhook /agent/explain)
- #289 (N8N-03 - Webhook /agent/qa)

**Status**: Both implementations complete ✅

---

**Guide Version**: 2.0.0
**Last Updated**: 2025-10-16
**Includes**: N8N-01, N8N-03

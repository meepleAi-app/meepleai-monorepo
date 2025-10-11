# N8N-01: Webhook /agent/explain

**Issue**: #288
**Component**: n8n
**Type**: Feature
**Priority**: P0
**Effort**: 3
**Dependencies**: AI-02 (✅ CLOSED)
**Status**: Design Complete, Ready for Implementation

---

## Overview

Espone webhook che orchestra RuleSpec+RAG per explain.

**Acceptance Criteria**: 200 OK con payload standardizzato.

---

## Current System Review

### ✅ Dependency Status: AI-02 (CLOSED)

AI-02 è completamente implementato e production-ready con:
- 7 comprehensive BDD tests (100% passing)
- Production endpoint: `POST /agents/explain`
- Full integration with ChatService (UI-01) and AiRequestLogService (ADM-01)
- Merged PRs: #58, #338, #339

### 1. RagService.ExplainAsync()

**Location**: `apps/api/src/Api/Services/RagService.cs:153-236`

**Funzionalità**:
```csharp
public async Task<ExplainResponse> ExplainAsync(
    string gameId,
    string topic,
    CancellationToken cancellationToken = default)
```

**Processo**:
1. Genera embeddings per il topic usando EmbeddingService
2. Cerca nel database vettoriale Qdrant (limit: 5 chunks)
3. Costruisce outline (topic + sezioni)
4. Genera script in formato markdown con citazioni
5. Crea citations con page numbers
6. Calcola estimated reading time (200 words/minute)

**Output** (`ExplainResponse`):
- `outline`: `{ mainTopic: string, sections: string[] }`
- `script`: Markdown-formatted explanation
- `citations`: `Snippet[] { text, source, page, line }`
- `estimatedReadingTimeMinutes`: int
- Token counts: `promptTokens`, `completionTokens`, `totalTokens`
- `confidence`: double?

**Features**:
- ✅ AI-05 caching (24h TTL, Redis-backed)
- ✅ Error handling con fallback messages
- ✅ Logging strutturato

### 2. POST /agents/explain Endpoint

**Location**: `apps/api/src/Api/Program.cs:627-722`

**Endpoint**: `POST /agents/explain`

**Request Model**:
```csharp
public record ExplainRequest(
    string gameId,
    string topic,
    Guid? chatId  // Optional: for chat persistence (UI-01)
);
```

**Authentication**:
- Session-based cookie authentication
- Returns `401 Unauthorized` if not authenticated

**Validation**:
- `gameId` required (returns `400 Bad Request` if missing)
- `topic` can be empty (returns error message in script)

**Integrations**:
- **ChatService (UI-01)**: Persists user query and assistant response if `chatId` provided
- **AiRequestLogService (ADM-01)**: Logs all requests with metrics (latency, tokens, status)

**Response** (HTTP 200):
```json
{
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
  "promptTokens": 0,
  "completionTokens": 0,
  "totalTokens": 0,
  "confidence": 0.95
}
```

**Error Handling**:
- Comprehensive try-catch
- Errors logged to ChatService (if chatId provided)
- Errors logged to AiRequestLogService
- Exception re-thrown for middleware handling

### 3. n8n Configuration System (ADM-02)

**Entity**: `N8nConfigEntity`
**Location**: `apps/api/src/Api/Infrastructure/Entities/N8nConfigEntity.cs`

**Schema**:
```csharp
public class N8nConfigEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string BaseUrl { get; set; }           // n8n instance URL
    public string ApiKeyEncrypted { get; set; }   // AES encrypted
    public string? WebhookUrl { get; set; }       // Webhook URL
    public bool IsActive { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public string? LastTestResult { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedByUserId { get; set; }
}
```

**Service**: `N8nConfigService`
**Location**: `apps/api/src/Api/Services/N8nConfigService.cs`

**Features**:
- ✅ CRUD operations for n8n configurations
- ✅ Connection testing via `GET /api/v1/workflows`
- ✅ API key encryption/decryption (AES with SHA256 key derivation)
- ✅ Environment variable: `N8N_ENCRYPTION_KEY`

**Admin Endpoints** (`Program.cs:719-857`):
- `GET /admin/n8n` - List all configurations
- `GET /admin/n8n/{configId}` - Get specific configuration
- `POST /admin/n8n` - Create new configuration
- `PUT /admin/n8n/{configId}` - Update configuration
- `DELETE /admin/n8n/{configId}` - Delete configuration
- `POST /admin/n8n/{configId}/test` - Test connection

**Security**:
- Admin-only access (role check)
- API keys stored encrypted in database
- Encryption key must be set via environment variable

---

## Webhook Design

### Architecture Overview

```
┌─────────────┐
│  External   │
│   Client    │──┐
└─────────────┘  │
                 │  HTTP POST /webhook/explain
                 │  { gameId, topic, sessionToken? }
                 ▼
┌──────────────────────────────────────────────┐
│              n8n Workflow                    │
│       "Agent Explain Orchestrator"           │
├──────────────────────────────────────────────┤
│                                              │
│  [1] Webhook Trigger                         │
│      - Method: POST                          │
│      - Path: /webhook/explain                │
│      - Input: gameId, topic, sessionToken?   │
│                                              │
│  [2] Validation Node                         │
│      - Validate gameId (required)            │
│      - Validate topic (optional)             │
│      - Return 400 if invalid                 │
│                                              │
│  [3] HTTP Request to API                     │
│      - Method: POST                          │
│      - URL: http://api:8080/agents/explain   │
│      - Headers:                              │
│        * Cookie: session={{sessionToken}}    │
│        * Content-Type: application/json      │
│      - Body: { gameId, topic }               │
│                                              │
│  [4] Response Transformation                 │
│      - Map ExplainResponse to standard       │
│      - Add metadata (timestamp, version)     │
│      - Format citations                      │
│                                              │
│  [5] Respond to Webhook                      │
│      - HTTP 200 OK                           │
│      - Content-Type: application/json        │
│      - Body: Standardized payload            │
│                                              │
└──────────────────────────────────────────────┘
```

### Standardized Response Payload

**Success Response** (HTTP 200):
```json
{
  "success": true,
  "data": {
    "outline": {
      "mainTopic": "string",
      "sections": ["string"]
    },
    "script": "markdown string with full explanation",
    "citations": [
      {
        "text": "snippet text",
        "source": "PDF:document-id",
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

**Error Response** (HTTP 400/401/500):
```json
{
  "success": false,
  "error": {
    "code": "INVALID_REQUEST",
    "message": "gameId is required",
    "details": {}
  },
  "timestamp": "2025-10-11T10:30:00Z",
  "version": "1.0"
}
```

### Authentication Strategy

**Option A: Service Account + Session Cookie** (✅ RECOMMENDED)
- Create a dedicated service account in MeepleAI (e.g., `n8n-service@meepleai.dev`)
- Authenticate via `/auth/login` in workflow initialization
- Store session cookie in n8n credentials store
- Include cookie in all `/agents/explain` requests
- Pros: Uses existing authentication, secure, auditable
- Cons: Requires session management

**Option B: API Key Authentication** (Requires Implementation)
- Add new authentication method to API
- Generate API keys for n8n service accounts
- Validate via custom middleware
- Pros: Simpler for webhooks
- Cons: Requires new authentication code

**Option C: Webhook Passes User Session** (Most Flexible)
- External client provides session token in request
- n8n forwards token to API
- API validates session normally
- Pros: Preserves user context, proper audit trail
- Cons: Requires secure token handling

**Recommended for MVP**: Option A (Service Account)

---

## Implementation Plan

### Phase 1: n8n Workflow Setup

**1.1 Create Workflow in n8n**
- Name: "Agent Explain Orchestrator"
- Description: "Webhook that orchestrates RuleSpec+RAG for explain functionality"

**1.2 Configure Nodes**:

**Node 1: Webhook Trigger**
- Type: `Webhook`
- Method: `POST`
- Path: `explain`
- Response Mode: `When Last Node Finishes`
- Response Data: `First Entry JSON`

**Node 2: Validate Input**
- Type: `Function`
- Code:
```javascript
const gameId = $json.body.gameId;
const topic = $json.body.topic || "";

if (!gameId) {
  return [{
    json: {
      statusCode: 400,
      body: {
        success: false,
        error: {
          code: "INVALID_REQUEST",
          message: "gameId is required"
        },
        timestamp: new Date().toISOString(),
        version: "1.0"
      }
    }
  }];
}

return [{
  json: {
    gameId,
    topic,
    valid: true
  }
}];
```

**Node 3: HTTP Request to API**
- Type: `HTTP Request`
- Method: `POST`
- URL: `http://api:8080/agents/explain`
- Authentication: `Predefined Credential Type` → `Header Auth`
  - Name: `Cookie`
  - Value: `session={{$credentials.sessionToken}}`
- Headers:
  - `Content-Type`: `application/json`
- Body:
```json
{
  "gameId": "={{$json.gameId}}",
  "topic": "={{$json.topic}}"
}
```
- Options:
  - Timeout: `30000` (30 seconds)
  - Redirect: `Follow All`

**Node 4: Transform Response**
- Type: `Function`
- Code:
```javascript
const apiResponse = $json;

return [{
  json: {
    success: true,
    data: {
      outline: apiResponse.outline,
      script: apiResponse.script,
      citations: apiResponse.citations,
      estimatedReadingTimeMinutes: apiResponse.estimatedReadingTimeMinutes,
      metadata: {
        promptTokens: apiResponse.promptTokens || 0,
        completionTokens: apiResponse.completionTokens || 0,
        totalTokens: apiResponse.totalTokens || 0,
        confidence: apiResponse.confidence
      }
    },
    timestamp: new Date().toISOString(),
    version: "1.0"
  }
}];
```

**Node 5: Error Handler**
- Type: `Function`
- Trigger: `On Error`
- Code:
```javascript
const error = $json.error || {};
const statusCode = $json.statusCode || 500;

return [{
  json: {
    success: false,
    error: {
      code: error.code || "INTERNAL_ERROR",
      message: error.message || "An unexpected error occurred",
      details: error.details || {}
    },
    timestamp: new Date().toISOString(),
    version: "1.0"
  }
}];
```

**Node 6: Respond to Webhook**
- Type: `Respond to Webhook`
- Response Mode: `Using 'Respond to Webhook' Node`
- Response Data: `={{$json}}`

### Phase 2: Service Account Setup

**2.1 Create Service Account**
```bash
# Via API endpoint POST /auth/register
{
  "email": "n8n-service@meepleai.dev",
  "password": "<STRONG_PASSWORD>",
  "displayName": "n8n Service Account",
  "role": "User"  # Or create dedicated "Service" role
}
```

**2.2 Authenticate and Store Session**
```bash
# POST /auth/login
{
  "email": "n8n-service@meepleai.dev",
  "password": "<PASSWORD>"
}

# Extract session cookie from response
# Store in n8n credentials
```

**2.3 Configure n8n Credentials**
- Navigate to n8n Credentials
- Create new credential: "MeepleAI Session"
- Type: `Header Auth`
- Name: `Cookie`
- Value: `session=<SESSION_TOKEN>`

### Phase 3: Register Webhook in Database

**3.1 Create n8n Configuration via API**
```bash
POST /admin/n8n
Authorization: Cookie: session=<ADMIN_SESSION>

{
  "name": "Agent Explain Webhook",
  "baseUrl": "http://n8n:5678",
  "apiKey": "<N8N_API_KEY>",
  "webhookUrl": "http://n8n:5678/webhook/explain"
}
```

**3.2 Test Connection**
```bash
POST /admin/n8n/{configId}/test
Authorization: Cookie: session=<ADMIN_SESSION>

# Expected response:
{
  "success": true,
  "message": "Connection successful (XXms)",
  "latencyMs": XX
}
```

### Phase 4: Testing

**4.1 Unit Test - Valid Request**
```bash
POST http://n8n:5678/webhook/explain
Content-Type: application/json

{
  "gameId": "tic-tac-toe",
  "topic": "winning conditions"
}

# Expected: 200 OK with standardized payload
```

**4.2 Test - Missing gameId**
```bash
POST http://n8n:5678/webhook/explain
Content-Type: application/json

{
  "topic": "winning conditions"
}

# Expected: 400 Bad Request
```

**4.3 Test - No Indexed Content**
```bash
POST http://n8n:5678/webhook/explain
Content-Type: application/json

{
  "gameId": "non-existent-game",
  "topic": "rules"
}

# Expected: 200 OK with "No relevant information found"
```

**4.4 Integration Test**
- Upload PDF for game
- Index PDF via `/ingest/pdf/{pdfId}/index`
- Call webhook
- Verify citations reference correct pages

**4.5 Performance Test**
- Concurrent requests (10 users)
- Measure latency distribution
- Verify caching works (AI-05)

---

## Configuration Requirements

### Environment Variables

**API**:
```bash
# Existing
OPENROUTER_API_KEY=<key>
QDRANT_URL=http://qdrant:6333
REDIS_URL=redis:6379
ConnectionStrings__Postgres=<connection_string>

# Required for n8n
N8N_ENCRYPTION_KEY=<32-byte-secure-key>
```

**n8n**:
```bash
N8N_HOST=n8n
N8N_PORT=5678
N8N_PROTOCOL=http
WEBHOOK_URL=http://n8n:5678/
N8N_API_KEY=<api-key>
```

### Database Migration

No new migrations required. Uses existing `N8nConfigEntity` table.

---

## Deployment Checklist

- [ ] Create service account (`n8n-service@meepleai.dev`)
- [ ] Set `N8N_ENCRYPTION_KEY` environment variable
- [ ] Import n8n workflow JSON
- [ ] Configure n8n credentials (MeepleAI Session)
- [ ] Activate workflow in n8n
- [ ] Register webhook via `/admin/n8n` endpoint
- [ ] Test connection via `/admin/n8n/{configId}/test`
- [ ] Run integration tests
- [ ] Monitor logs for errors
- [ ] Document webhook URL for consumers

---

## Success Metrics

**Acceptance Criteria**: ✅ 200 OK con payload standardizzato

**Additional Metrics**:
- Response time < 2 seconds (p95)
- Error rate < 1%
- Cache hit rate > 70% (AI-05)
- Successful integration with external systems

---

## Open Questions

1. **Authentication**: Should we implement dedicated API key auth for webhooks?
2. **Rate Limiting**: Should webhooks have different rate limits than UI users?
3. **Versioning**: Should webhook payload include API version for future compatibility?
4. **Monitoring**: What metrics should be tracked specifically for webhooks?
5. **Documentation**: Where should external consumers find webhook documentation?

---

## References

### Code Locations
- RagService: `apps/api/src/Api/Services/RagService.cs:153-236`
- /agents/explain endpoint: `apps/api/src/Api/Program.cs:627-722`
- N8nConfigService: `apps/api/src/Api/Services/N8nConfigService.cs`
- N8nConfigEntity: `apps/api/src/Api/Infrastructure/Entities/N8nConfigEntity.cs`
- Tests: `apps/api/tests/Api.Tests/ExplainEndpointTests.cs`

### Related Issues
- AI-02 (#280): RAG Explain - CLOSED ✅
- AI-05 (#283): Caching risposte per gioco
- ADM-01 (#286): Dashboard admin e log richieste
- UI-01: Chat management

### Related PRs
- PR #58: AI-02 initial implementation - MERGED ✅
- PR #338: BDD integration tests - MERGED ✅
- PR #339: Final test integration - MERGED ✅

---

**Document Version**: 1.0
**Last Updated**: 2025-10-11
**Author**: Claude Code
**Status**: Design Complete, Ready for Implementation

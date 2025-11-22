# MeepleAI Postman Test Collections

Comprehensive Postman test collections for MeepleAI API, organized by bounded context following Domain-Driven Design (DDD) principles.

## 📁 Structure

```
tests/postman/
├── collections/
│   ├── 01-health/
│   │   └── HealthCheck.postman_collection.json
│   ├── 02-authentication/
│   │   └── Authentication.postman_collection.json
│   ├── 03-game-management/
│   │   └── GameManagement.postman_collection.json
│   ├── 04-knowledge-base/
│   │   └── KnowledgeBase.postman_collection.json
│   ├── 05-administration/
│   │   └── Administration.postman_collection.json
│   └── 06-boardgamegeek/
│       └── BoardGameGeek.postman_collection.json
├── environments/
│   ├── local.postman_environment.json
│   └── production.postman_environment.json
└── README.md (this file)
```

## 🎯 Collections Overview

### 01. Health Check
**File**: `collections/01-health/HealthCheck.postman_collection.json`

Basic health check and readiness endpoints.

**Endpoints**:
- `GET /health` - API health status

**No authentication required**

---

### 02. Authentication
**File**: `collections/02-authentication/Authentication.postman_collection.json`

Authentication bounded context - user registration, login, sessions, API keys, 2FA.

**Endpoints**:
- **User Authentication**
  - `POST /api/v1/auth/register` - Register new user
  - `POST /api/v1/auth/login` - Login with email/password
  - `GET /api/v1/auth/me` - Get current user
  - `POST /api/v1/auth/logout` - Logout user

- **Session Management**
  - `GET /api/v1/auth/session/status` - Get session status
  - `POST /api/v1/auth/session/extend` - Extend session
  - `GET /api/v1/users/me/sessions` - Get all user sessions

- **API Key Authentication**
  - `POST /api/v1/auth/apikey/login` - Login with API key

- **Two-Factor Authentication (2FA)**
  - `POST /api/v1/auth/2fa/setup` - Setup TOTP
  - `POST /api/v1/auth/2fa/enable` - Enable 2FA
  - `GET /api/v1/users/me/2fa/status` - Get 2FA status
  - `POST /api/v1/auth/2fa/disable` - Disable 2FA

**Demo Users**:
```
user@meepleai.dev     (User role)
editor@meepleai.dev   (Editor role)
admin@meepleai.dev    (Admin role)
Password: Demo123!
```

---

### 03. Game Management
**File**: `collections/03-game-management/GameManagement.postman_collection.json`

GameManagement bounded context - game catalog and play sessions.

**Endpoints**:
- **Game Catalog**
  - `GET /api/v1/games` - Get all games
  - `GET /api/v1/games/{id}` - Get game by ID
  - `GET /api/v1/games/{id}/details` - Get game details
  - `GET /api/v1/games/{id}/rules` - Get game rules
  - `POST /api/v1/games` - Create game (Admin/Editor)

- **Game Sessions**
  - `POST /api/v1/sessions` - Start session
  - `GET /api/v1/sessions/{id}` - Get session
  - `POST /api/v1/sessions/{id}/players` - Add player
  - `POST /api/v1/sessions/{id}/pause` - Pause session
  - `POST /api/v1/sessions/{id}/resume` - Resume session
  - `POST /api/v1/sessions/{id}/complete` - Complete session
  - `GET /api/v1/sessions/active` - Get active sessions
  - `GET /api/v1/sessions/statistics` - Get statistics

**Domain Events**: GameCreated, SessionStarted, SessionPaused, SessionResumed, SessionCompleted, PlayerAdded

---

### 04. Knowledge Base
**File**: `collections/04-knowledge-base/KnowledgeBase.postman_collection.json`

KnowledgeBase bounded context - AI, RAG, search, and specialized agents.

**Endpoints**:
- **Search & Retrieval**
  - `POST /api/v1/knowledge-base/search` - Hybrid search (vector + keyword RRF)

- **Q&A Agent**
  - `POST /api/v1/agents/qa` - Ask question (non-streaming)
  - `POST /api/v1/agents/qa/stream` - Ask question (streaming SSE)

- **Other AI Agents**
  - `POST /api/v1/agents/explain` - Generate explanation
  - `POST /api/v1/agents/setup` - Setup guide (streaming)
  - `POST /api/v1/agents/chess` - Chess agent

- **Agent Feedback**
  - `POST /api/v1/agents/feedback` - Provide feedback (thumbs up/down)

**RAG Pipeline**: Hybrid Search (70% vector + 30% keyword) → Multi-model LLM → 5-layer validation

**Search Modes**:
- `hybrid` - Vector + keyword fusion via RRF (default, recommended)
- `vector` - Pure semantic similarity (cosine)
- `keyword` - PostgreSQL full-text search

**Quality Metrics**:
- `searchConfidence` - Retrieval quality (0-1)
- `llmConfidence` - Generation confidence (0-1)
- `overallConfidence` - Weighted average (70/30)

---

### 05. Administration
**File**: `collections/05-administration/Administration.postman_collection.json`

Administration bounded context - user management, logs, monitoring.

**Endpoints**:
- **Logs & Monitoring**
  - `GET /api/v1/logs` - Get AI request logs (Admin)

- **User Management**
  - `GET /api/v1/users/search` - Search users
  - `GET /api/v1/users/{id}` - Get user by ID (Admin)

**Authorization**: Most endpoints require **Admin role**

---

### 06. BoardGameGeek API
**File**: `collections/06-boardgamegeek/BoardGameGeek.postman_collection.json`

BoardGameGeek API integration for game metadata and ratings.

**Endpoints**:
- `GET /api/v1/bgg/search` - Search BGG games
- `GET /api/v1/bgg/games/{bggId}` - Get BGG game details

**Common BGG IDs**:
- `13` - Catan
- `174430` - Gloomhaven
- `167791` - Terraforming Mars
- `173346` - 7 Wonders Duel
- `182028` - Through the Ages

---

## 🌍 Environments

### Local Development
**File**: `environments/local.postman_environment.json`

```json
{
  "base_url": "http://localhost:5080",
  "api_key": "",
  "user_id": "",
  "game_id": "",
  "session_id": "",
  "totp_secret": "",
  "timestamp": ""
}
```

### Production
**File**: `environments/production.postman_environment.json`

Set `base_url` to your production API endpoint.

---

## 🚀 Quick Start

### 1. Import Collections

**Import all collections** into Postman:
```bash
# Import all collections at once
File → Import → Select all .json files in collections/
```

Or use Postman CLI:
```bash
postman collection import collections/01-health/HealthCheck.postman_collection.json
postman collection import collections/02-authentication/Authentication.postman_collection.json
postman collection import collections/03-game-management/GameManagement.postman_collection.json
postman collection import collections/04-knowledge-base/KnowledgeBase.postman_collection.json
postman collection import collections/05-administration/Administration.postman_collection.json
postman collection import collections/06-boardgamegeek/BoardGameGeek.postman_collection.json
```

### 2. Import Environment

```bash
postman environment import environments/local.postman_environment.json
```

Select "MeepleAI - Local Development" environment in Postman.

### 3. Start Local API

```bash
cd apps/api/src/Api
dotnet run
```

Or use Docker Compose:
```bash
cd infra
docker compose up meepleai-api
```

### 4. Run Tests

**Typical workflow**:

1. **Health Check** → Verify API is running
2. **Authentication** → Login to get session cookie
   - Use `POST /api/v1/auth/login` with `user@meepleai.dev` / `Demo123!`
   - Session cookie (`meepleai_session`) is automatically saved
3. **Game Management** → Get game list
   - Use `GET /api/v1/games` to save `game_id` to environment
4. **Knowledge Base** → Test RAG and Q&A
   - Use `POST /api/v1/agents/qa` with saved `game_id`
5. **Other collections** as needed

---

## 🔐 Authentication Patterns

### Cookie-Based (Browser/Postman)

1. Login: `POST /api/v1/auth/login`
2. Session cookie (`meepleai_session`) is automatically set (httpOnly, secure)
3. Subsequent requests use cookie automatically

### API Key (Programmatic)

**Format**: `mpl_{env}_{base64}`

**Option 1: Header-based** (recommended for scripts):
```
Authorization: ApiKey mpl_dev_abc123...
```

**Option 2: Login endpoint** (browser-friendly):
```bash
POST /api/v1/auth/apikey/login
{
  "apiKey": "mpl_dev_abc123..."
}
```

### Priority

Cookie > API Key Header

---

## 📊 Test Execution with Newman

Run collections programmatically using Newman (Postman CLI):

### Install Newman

```bash
npm install -g newman
```

### Run Single Collection

```bash
newman run collections/01-health/HealthCheck.postman_collection.json \
  --environment environments/local.postman_environment.json
```

### Run All Collections (Smoke Test)

```bash
#!/bin/bash
# Run all collections in order

newman run collections/01-health/HealthCheck.postman_collection.json \
  --environment environments/local.postman_environment.json \
  --reporters cli,json \
  --reporter-json-export results/health.json

newman run collections/02-authentication/Authentication.postman_collection.json \
  --environment environments/local.postman_environment.json \
  --reporters cli,json \
  --reporter-json-export results/authentication.json

newman run collections/03-game-management/GameManagement.postman_collection.json \
  --environment environments/local.postman_environment.json \
  --reporters cli,json \
  --reporter-json-export results/game-management.json

newman run collections/04-knowledge-base/KnowledgeBase.postman_collection.json \
  --environment environments/local.postman_environment.json \
  --reporters cli,json \
  --reporter-json-export results/knowledge-base.json

newman run collections/05-administration/Administration.postman_collection.json \
  --environment environments/local.postman_environment.json \
  --reporters cli,json \
  --reporter-json-export results/administration.json

newman run collections/06-boardgamegeek/BoardGameGeek.postman_collection.json \
  --environment environments/local.postman_environment.json \
  --reporters cli,json \
  --reporter-json-export results/boardgamegeek.json

echo "✅ All collections executed successfully!"
```

### CI/CD Integration

**GitHub Actions** example:
```yaml
- name: Run Postman Tests
  run: |
    npm install -g newman
    newman run tests/postman/collections/01-health/HealthCheck.postman_collection.json \
      --environment tests/postman/environments/local.postman_environment.json \
      --reporters cli,junit \
      --reporter-junit-export test-results/postman.xml
```

---

## 🧪 Test Coverage

| Collection | Requests | Coverage |
|-----------|----------|----------|
| Health Check | 1 | API health |
| Authentication | 13 | Auth, 2FA, Sessions, API Keys |
| Game Management | 13 | Games, Sessions, Players |
| Knowledge Base | 9 | RAG, Q&A, AI Agents |
| Administration | 3 | Logs, User Management |
| BoardGameGeek | 2 | BGG Search, Metadata |
| **Total** | **41** | **100% API coverage** |

---

## 📚 Architecture Reference

### Bounded Contexts (DDD)

```
apps/api/src/Api/BoundedContexts/
├── Authentication/         Auth, sessions, API keys, OAuth, 2FA
├── GameManagement/         Games catalog, play sessions
├── KnowledgeBase/          RAG, vectors, chat (Streaming CQRS)
├── DocumentProcessing/     PDF upload, extraction, validation
├── WorkflowIntegration/    n8n workflows, error logging
├── SystemConfiguration/    Runtime config, feature flags
└── Administration/         Users, alerts, audit, analytics
```

### CQRS Pattern

All endpoints use **MediatR** with Command/Query handlers:

```
HTTP Request → Endpoint → IMediator.Send() → Handler → Response
```

**Example**:
```csharp
// Endpoint
app.MapPost("/api/v1/auth/login", async (LoginCommand command, IMediator mediator) =>
{
    var result = await mediator.Send(command);
    return Results.Ok(result);
});

// Handler
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Domain logic here
    }
}
```

### Domain Events

**41 domain events** + **40 handlers** across all contexts.

Examples:
- `UserRegistered` (Authentication)
- `GameCreated` (GameManagement)
- `SessionStarted` (GameManagement)
- `AgentMessageGenerated` (KnowledgeBase)

---

## 🔧 Troubleshooting

### Common Issues

**1. "Unauthorized" (401) errors**

- Ensure you've logged in first: `POST /api/v1/auth/login`
- Check that session cookie is saved
- Verify environment is selected in Postman

**2. "Game not found" errors**

- Run `GET /api/v1/games` first to populate `game_id`
- Ensure demo data is seeded (migration: `20251009140700_SeedDemoData`)

**3. Connection refused**

- Check API is running: `curl http://localhost:5080/health`
- Verify port `5080` is correct (check `appsettings.json`)

**4. CORS errors (browser)**

- CORS is configured for `http://localhost:3000` (Next.js frontend)
- If using different origin, update `appsettings.json`:
  ```json
  "AllowedOrigins": ["http://localhost:3000", "your-origin"]
  ```

---

## 📖 Additional Resources

- **API Specification**: `docs/03-api/board-game-ai-api-specification.md`
- **Architecture Overview**: `docs/01-architecture/overview/system-architecture.md`
- **ADR Hybrid RAG**: `docs/01-architecture/adr/adr-001-hybrid-rag.md`
- **Testing Guide**: `docs/02-development/testing/README.md`
- **CLAUDE.md**: Project overview and commands

---

## 🔄 Version History

| Version | Date | Changes |
|---------|------|---------|
| 2.0.0 | 2025-11-20 | **Reorganized by bounded contexts** (DDD architecture) |
| 1.0.0 | 2025-11-19 | Legacy monolithic collection |

---

## 📝 Contributing

When adding new endpoints:

1. Identify the **bounded context**
2. Add request to appropriate collection
3. Include CQRS handler references in description
4. Add comprehensive test scripts
5. Update this README

---

**Last Updated**: 2025-11-20
**Maintained by**: Engineering Lead
**Architecture**: DDD/CQRS with 7 bounded contexts, 224 CQRS handlers

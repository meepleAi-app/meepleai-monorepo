# Agent Configuration Guide

> **Last Updated**: 2025-10-18
> **Status**: Research Complete
> **Confidence**: 0.95 (verified from codebase analysis)

## Overview

MeepleAI agents are AI-powered assistants specialized for different board game tasks. This guide explains how agents are configured, where their behavior is defined, and how to modify them.

## Agent Architecture

### What is an Agent?

An agent in MeepleAI is a specialized AI service that combines:
1. **Database metadata** (stored in `agents` table) - agent identity and game association
2. **Service implementation** (C# code) - business logic and RAG pipeline
3. **LLM prompts** (hardcoded in services) - behavior instructions for the AI model
4. **Configuration** (appsettings.json) - LLM model settings, rate limits, etc.

### Agent Types (Kind)

MeepleAI supports three primary agent types:

| Kind | Purpose | Endpoint | Service |
|------|---------|----------|---------|
| `qa` | Answer questions about game rules | `POST /api/v1/agents/qa` | `RagService.AskAsync()` |
| `explain` | Generate detailed rule explanations | `POST /api/v1/agents/explain` | `RagService.ExplainAsync()` |
| `setup` | Generate step-by-step setup guides | `POST /api/v1/agents/setup` | `SetupGuideService.GenerateSetupGuideAsync()` |
| `chess` | Specialized chess conversational agent | `POST /api/v1/agents/chess` | `ChessAgentService.AskAsync()` |

**Note**: The `Kind` field is currently for organizational purposes only. The actual behavior is determined by which endpoint/service is called, not by the agent's Kind value in the database.

---

## Database Configuration

### Agent Entity Schema

**Location**: `D:\Repositories\meepleai-monorepo\apps\api\src\Api\Infrastructure\Entities\AgentEntity.cs`

```csharp
public class AgentEntity
{
    public string Id { get; set; }           // Agent identifier (e.g., "agent-ttt-qa")
    public string GameId { get; set; }       // Associated game ID (e.g., "tic-tac-toe")
    public string Name { get; set; }         // Display name (e.g., "Tic-Tac-Toe Q&A")
    public string Kind { get; set; }         // Agent type: "qa", "explain", "setup", "chess"
    public DateTime CreatedAt { get; set; }  // Creation timestamp

    // Navigation properties
    public GameEntity Game { get; set; }
    public ICollection<ChatEntity> Chats { get; set; }
}
```

### Database Table: `agents`

**Table Name**: `agents`

**Columns**:
- `Id` (varchar(64), PK) - Unique agent identifier
- `GameId` (varchar(64), FK → games) - Associated game
- `Name` (varchar(128)) - Human-readable agent name
- `Kind` (varchar(32)) - Agent type (qa/explain/setup/chess)
- `CreatedAt` (timestamptz) - Creation timestamp

**Indexes**:
- Primary key on `Id`
- Composite index on `(GameId, Name)`

**Relationships**:
- Many-to-One → `games` (CASCADE delete)
- One-to-Many → `chats` (CASCADE delete)
- One-to-Many → `agent_feedback` (CASCADE delete)

### Seeded Demo Agents

**Location**: `D:\Repositories\meepleai-monorepo\apps\api\src\Api\Migrations\20251009140700_SeedDemoData.cs`

The system seeds 4 demo agents on database initialization:

```sql
-- Tic-Tac-Toe agents
INSERT INTO agents (Id, GameId, Name, Kind, CreatedAt)
VALUES ('agent-ttt-explain', 'tic-tac-toe', 'Tic-Tac-Toe Explainer', 'explain', NOW());

INSERT INTO agents (Id, GameId, Name, Kind, CreatedAt)
VALUES ('agent-ttt-qa', 'tic-tac-toe', 'Tic-Tac-Toe Q&A', 'qa', NOW());

-- Chess agents
INSERT INTO agents (Id, GameId, Name, Kind, CreatedAt)
VALUES ('agent-chess-explain', 'chess', 'Chess Explainer', 'explain', NOW());

INSERT INTO agents (Id, GameId, Name, Kind, CreatedAt)
VALUES ('agent-chess-qa', 'chess', 'Chess Q&A', 'qa', NOW());
```

### Querying Agents

**Get agents for a game**:
```http
GET /api/v1/games/{gameId}/agents
Authorization: Cookie (session required)
```json
**Implementation**: `D:\Repositories\meepleai-monorepo\apps\api\src\Api\Program.cs:3658-3675`

```csharp
v1Api.MapGet("/games/{gameId}/agents", async (string gameId, HttpContext context, ChatService chatService, CancellationToken ct) =>
{
    var agents = await chatService.GetAgentsForGameAsync(gameId, ct);
    return Results.Json(agents.Select(a => new AgentDto(
        a.Id,
        a.GameId,
        a.Name,
        a.Kind,
        a.CreatedAt
    )));
});
```sql
---

## Agent Behavior Configuration

### Where Agent Behavior is Defined

Agent behavior is primarily controlled by **hardcoded system prompts** in service classes. There is currently **NO database-driven prompt management** - all prompts are defined directly in C# code.

### 1. Q&A Agent (Kind: "qa")

**Service**: `RagService.AskAsync()`
**Location**: `D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RagService.cs:111-119`

**System Prompt**:
```plaintext
You are a board game rules assistant. Your job is to answer questions about board game rules based ONLY on the provided context from the rulebook.

CRITICAL INSTRUCTIONS:
- If the answer to the question is clearly found in the provided context, answer it concisely and accurately.
- If the answer is NOT in the provided context or you're uncertain, respond with EXACTLY: "Not specified"
- Do NOT make assumptions or use external knowledge about the game.
- Do NOT hallucinate or invent information.
- Keep your answers brief and to the point (2-3 sentences maximum).
- Reference page numbers when relevant.
```sql
**Key Characteristics**:
- Anti-hallucination focus ("Not specified" fallback)
- RAG-based (retrieves 5 most relevant chunks from Qdrant)
- Response caching (24h TTL via AI-05)
- Maximum 2-3 sentence answers
- Page number citations

**Configuration Parameters** (not currently exposed):
- Search limit: 5 chunks (hardcoded in `RagService.cs:99`)
- Cache TTL: 86400 seconds (24 hours) (hardcoded in `RagService.cs:186`)

### 2. Explain Agent (Kind: "explain")

**Service**: `RagService.ExplainAsync()`
**Location**: `D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RagService.cs:195-325`

**Behavior**:
- Does NOT use LLM prompts (unlike Q&A agent)
- Generates explanations by concatenating retrieved RAG chunks
- Creates outline and script structure from vector search results
- Retrieves 5 most relevant chunks (hardcoded limit)
- Builds markdown-formatted explanation script

**Response Structure**:
```json
{
  "outline": {
    "mainTopic": "topic name",
    "sections": ["section 1", "section 2", ...]
  },
  "script": "# Explanation: topic\n\n## Overview\n...",
  "citations": [...],
  "estimatedReadingTimeMinutes": 2
}
```sql
**Key Implementation Details**:
- No LLM generation (pure RAG retrieval + formatting)
- Word count / 200 = estimated reading time
- Section titles extracted from first sentence of each chunk
- Citations include page numbers

### 3. Setup Agent (Kind: "setup")

**Service**: `SetupGuideService.GenerateSetupGuideAsync()`
**Location**: `D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\SetupGuideService.cs`

**Behavior**:
- Uses RAG to retrieve 10 most relevant chunks (higher than Q&A/Explain)
- LLM synthesizes structured setup steps from RAG context
- Supports optional steps detection
- Distributes citations across generated steps
- Fallback to default steps if LLM/RAG unavailable

**Configuration**: (not currently documented in detail)

### 4. Chess Agent (Kind: "chess")

**Service**: `ChessAgentService.AskAsync()`
**Location**: `D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\ChessAgentService.cs:137-181`

**System Prompt**:
```plaintext
You are a specialized chess AI assistant with deep knowledge of chess rules, openings, tactics, and strategies.

YOUR ROLE:
- Answer chess questions based ONLY on the provided context from the chess knowledge base
- Explain chess concepts clearly and concisely
- Suggest tactical and strategic moves when relevant
- Reference specific sources when providing information

CRITICAL INSTRUCTIONS:
- If the answer is clearly found in the provided context, answer it accurately
- If the answer is NOT in the context or you're uncertain, respond with: "Not specified in chess knowledge base"
- Do NOT hallucinate or invent information
- Keep answers concise (3-5 sentences for explanations, 2-3 sentences for direct questions)
- When suggesting moves, always explain WHY the move is good

[Additional FEN position analysis instructions if position provided...]

RESPONSE FORMAT:
- Start with a direct answer to the question
- If analyzing a position, provide a brief evaluation
- List suggested moves in format: "1. Move notation: Explanation"
- End with "Sources: [1] [2]..." to cite your sources
```sql
**Key Features**:
- FEN position validation and analysis
- Chess-specific knowledge base search
- Move suggestion with explanations
- Position evaluation (material, pawn structure, king safety, piece activity)
- Regex-based move extraction from LLM response

---

## Modifying Agent Behavior

### Current Approach (Hardcoded Prompts)

To modify agent behavior, you must:

1. **Edit the service class source code**
2. **Locate the system prompt** (search for `var systemPrompt = @"...`)
3. **Modify the prompt text**
4. **Rebuild the application**
5. **Redeploy**

**Example: Making Q&A agent more verbose**

**File**: `D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RagService.cs`

```csharp
// BEFORE (line 118)
- Keep your answers brief and to the point (2-3 sentences maximum).

// AFTER
- Provide detailed answers with examples when helpful (5-7 sentences).
```

### Limitations of Current Approach

1. **No Runtime Configuration**: Cannot change prompts without code deployment
2. **No Versioning**: No history of prompt changes
3. **No A/B Testing**: Cannot test different prompts in production
4. **No Admin UI**: Must edit source code directly
5. **Tight Coupling**: Prompts embedded in service logic

### Future: Prompt Template System (Not Yet Implemented)

The codebase includes database infrastructure for a **prompt template system** that is NOT currently wired up:

**Tables**:
- `prompt_templates` - Template definitions
- `prompt_versions` - Versioned prompt content
- `prompt_audit_logs` - Change history

**Schema** (`PromptTemplateEntity.cs`):
```csharp
public class PromptTemplateEntity
{
    public string Id { get; set; }
    public string Name { get; set; }           // e.g., "qa-system-prompt"
    public string Description { get; set; }
    public string Category { get; set; }       // e.g., "qa", "explain", "setup"
    public string CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<PromptVersionEntity> Versions { get; set; }
    public ICollection<PromptAuditLogEntity> AuditLogs { get; set; }
}
```sql
**Status**: Database tables exist, but no service implementation or endpoints exist yet.

**What would be needed to implement**:
1. `PromptTemplateService` - CRUD operations for templates
2. Admin endpoints for managing prompts
3. Refactor `RagService`, `ChessAgentService`, etc. to load prompts from database
4. Version activation mechanism
5. Audit logging integration
6. Admin UI (Next.js frontend)

---

## Configuration Files

### appsettings.json

**Location**: `D:\Repositories\meepleai-monorepo\apps\api\src\Api\appsettings.json`

**Relevant Sections**:

```json
{
  "LlmProvider": "OpenRouter",  // or "Ollama"

  "OpenRouter": {
    "ApiKey": "${OPENROUTER_API_KEY}",
    "BaseUrl": "https://openrouter.ai/api/v1",
    "Model": "anthropic/claude-3-haiku"  // Default model for all agents
  },

  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.1:8b"
  },

  "RateLimiting": {
    "PermitLimit": 100,
    "Window": "00:01:00",
    "QueueLimit": 0
  },

  "Caching": {
    "Redis": {
      "ConnectionString": "localhost:6379",
      "DefaultTtlSeconds": 86400  // 24 hours for AI response cache
    }
  },

  "Qdrant": {
    "Url": "http://localhost:6333",
    "CollectionNamePrefix": "meepleai-"
  }
}
```

**To Change LLM Model for ALL Agents**:

Edit `appsettings.json` or set environment variable:

```bash
# Use different OpenRouter model
export OPENROUTER_MODEL="anthropic/claude-3.5-sonnet"

# Or use Ollama instead
export LLM_PROVIDER="Ollama"
export OLLAMA_MODEL="llama3.1:8b"
```json
### Environment Variables

**Key Variables**:
- `OPENROUTER_API_KEY` - OpenRouter API key
- `OPENROUTER_MODEL` - LLM model to use (default: anthropic/claude-3-haiku)
- `LLM_PROVIDER` - "OpenRouter" or "Ollama"
- `OLLAMA_BASE_URL` - Ollama instance URL
- `QDRANT_URL` - Qdrant vector database URL
- `REDIS_URL` - Redis cache URL
- `ConnectionStrings__Postgres` - PostgreSQL connection string

---

## n8n Workflow Integration

### n8n Webhooks as Agent Orchestrators

MeepleAI exposes agents via **n8n webhooks** that act as orchestration layers:

**N8N-01: Explain Webhook**
- Webhook: `POST http://localhost:5678/webhook/explain`
- Backend: `POST /api/v1/agents/explain`
- Workflow: `infra/init/n8n/agent-explain-orchestrator.json`

**N8N-03: Q&A Webhook**
- Webhook: `POST http://localhost:5678/webhook/agent/qa`
- Backend: `POST /api/v1/agents/qa`
- Workflow: `infra/init/n8n/agent-qa-webhook.json`

**How n8n Workflows Configure Agents**:

n8n workflows can:
1. **Route requests** to different backend agents based on input
2. **Transform payloads** to standardize request/response formats
3. **Add retry logic** (3 attempts with exponential backoff)
4. **Log requests** with correlation IDs
5. **Chain agents** together (e.g., Q&A → Explain for follow-ups)
6. **A/B test** different agent configurations (future)

**Documentation**:
- `docs/guide/n8n-integration-guide.md` - Complete n8n integration guide
- `docs/technic/n8n-webhook-qa-design.md` - Q&A webhook technical spec
- `docs/technic/n8n-webhook-explain-design.md` - Explain webhook technical spec

---

## Admin Endpoints (Currently Missing)

There are **NO admin endpoints** for managing agents in the current codebase.

**What's missing**:
- `POST /admin/agents` - Create new agent
- `PUT /admin/agents/{id}` - Update agent metadata
- `DELETE /admin/agents/{id}` - Delete agent
- `GET /admin/agents` - List all agents with filters

**Workaround**: Manually insert agents via SQL or EF Core migrations:

```sql
INSERT INTO agents (Id, GameId, Name, Kind, CreatedAt)
VALUES ('agent-catan-qa', 'catan', 'Catan Q&A Assistant', 'qa', NOW());
```

---

## Testing Agent Configuration

### Unit Tests

**Location**: `D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests`

**Key Test Files**:
- `Ai04ComprehensiveTests.cs` - Q&A agent behavior tests
- `ChessAgentServiceTests.cs` - Chess agent tests
- `SetupGuideServiceComprehensiveTests.cs` - Setup agent tests
- `ChatServiceTests.cs` - Agent-chat integration tests

**Testing Prompt Changes**:

```csharp
[Fact]
public async Task QaAgent_Uses_AntiHallucination_Prompt()
{
    // Given: Mock LLM service that captures system prompt
    string? capturedSystemPrompt = null;

    _llmServiceMock
        .Setup(x => x.GenerateCompletionAsync(
            It.IsAny<string>(),  // Capture this
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
        .Callback<string, string, CancellationToken>((sys, user, ct) =>
        {
            capturedSystemPrompt = sys;
        })
        .ReturnsAsync(new LlmResult(...));

    // When: Call Q&A agent
    await _ragService.AskAsync("tic-tac-toe", "How to win?");

    // Then: Verify prompt contains anti-hallucination instructions
    Assert.Contains("board game rules assistant", capturedSystemPrompt, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("Do NOT hallucinate", capturedSystemPrompt, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("Not specified", capturedSystemPrompt);
}
```

### Integration Tests

**Location**: `D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\Integration`

**Testing Full Agent Flow**:

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~Ai04IntegrationTests"
```

---

## Best Practices

### 1. Agent Naming Conventions

Follow this pattern for consistency:

```
agent-{game-abbrev}-{kind}

Examples:
- agent-ttt-qa (Tic-Tac-Toe Q&A)
- agent-chess-explain (Chess Explainer)
- agent-catan-setup (Catan Setup Guide)
```json
### 2. Prompt Engineering Guidelines

When modifying agent prompts:

1. **Be Explicit**: State what the agent should and should NOT do
2. **Use Examples**: Show desired output format
3. **Anti-Hallucination**: Always include "Not specified" fallback
4. **Context Boundaries**: Emphasize "based ONLY on provided context"
5. **Tone & Length**: Specify conciseness expectations (e.g., "2-3 sentences")
6. **Citations**: Instruct agent to reference sources/page numbers

### 3. Testing Prompt Changes

Before deploying prompt changes:

1. **Unit Test**: Verify prompt structure with mock LLM
2. **Integration Test**: Test with real LLM against test dataset
3. **RAG Evaluation**: Run offline eval (`AI-06-rag-evaluation.md`)
4. **Manual QA**: Test edge cases (missing context, ambiguous questions)
5. **Monitor Metrics**: Track confidence scores, cache hit rates, latency

### 4. Configuration Management

**Future-Proof Approach**:

1. **Externalize Prompts**: Move to database (prompt_templates) when implemented
2. **Version Control**: Track prompt changes in git (for now) or database (future)
3. **Environment-Specific**: Consider dev/staging/prod prompt variants
4. **Feature Flags**: Use feature flags for prompt A/B testing
5. **Audit Trail**: Log all prompt changes (prompt_audit_logs table ready)

---

## Quick Reference

### Agent Types Summary

| Kind | Purpose | Prompt? | RAG? | LLM? | Chunks | Cache |
|------|---------|---------|------|------|--------|-------|
| `qa` | Answer questions | ✅ Yes | ✅ Yes | ✅ Yes | 5 | 24h |
| `explain` | Generate explanations | ❌ No | ✅ Yes | ❌ No | 5 | 24h |
| `setup` | Setup guides | ✅ Yes | ✅ Yes | ✅ Yes | 10 | 24h |
| `chess` | Chess analysis | ✅ Yes | ✅ Yes | ✅ Yes | 5 | 24h |

### Key Files

| Component | Location |
|-----------|----------|
| Agent Entity | `apps/api/src/Api/Infrastructure/Entities/AgentEntity.cs` |
| Q&A Service | `apps/api/src/Api/Services/RagService.cs` |
| Chess Service | `apps/api/src/Api/Services/ChessAgentService.cs` |
| Setup Service | `apps/api/src/Api/Services/SetupGuideService.cs` |
| Agent Endpoints | `apps/api/src/Api/Program.cs` (lines 988-1811) |
| Demo Seed Data | `apps/api/src/Api/Migrations/20251009140700_SeedDemoData.cs` |
| n8n Integration Guide | `docs/guide/n8n-integration-guide.md` |

### Common Tasks

**Create New Agent** (manual SQL):
```sql
INSERT INTO agents (Id, GameId, Name, Kind, CreatedAt)
VALUES ('agent-{game}-{kind}', '{game-id}', '{Display Name}', '{kind}', NOW());
```json
**Change Q&A Prompt**:
Edit `apps/api/src/Api/Services/RagService.cs:111-119`

**Change LLM Model**:
Edit `appsettings.json` → `OpenRouter.Model` or set `OPENROUTER_MODEL` env var

**Test Agent Behavior**:
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~Ai04IntegrationTests"
```

**Deploy Changes**:
```bash
cd apps/api/src/Api
dotnet build
docker compose up -d --build api
```json
---

## Related Documentation

- **Database Schema**: `docs/database-schema.md` - Complete schema reference
- **n8n Integration**: `docs/guide/n8n-integration-guide.md` - Webhook orchestration
- **RAG Evaluation**: `docs/ai-06-rag-evaluation.md` - Testing agent quality
- **API Documentation**: `CLAUDE.md` - General API overview
- **Streaming Responses**: `docs/issue/chat-01-streaming-sse-implementation.md` - SSE streaming

---

## Future Enhancements

### Planned Features (Not Yet Implemented)

1. **Database-Driven Prompts** (High Priority)
   - Implement `PromptTemplateService`
   - Admin UI for prompt management
   - Prompt versioning and rollback
   - A/B testing framework

2. **Agent Configuration API** (Medium Priority)
   - `POST /admin/agents` - Create agents
   - `PUT /admin/agents/{id}` - Update metadata
   - `PATCH /admin/agents/{id}/prompt` - Update prompt reference
   - `GET /admin/agents/{id}/metrics` - Performance metrics

3. **Per-Agent LLM Settings** (Low Priority)
   - Different models per agent type
   - Agent-specific temperature/top_p settings
   - Per-agent rate limits

4. **Advanced Routing** (Future)
   - Intelligent agent selection based on query type
   - Multi-agent collaboration (Q&A → Explain chaining)
   - Fallback agent chains

---

## Support & Troubleshooting

**Common Issues**:

1. **Agent returns generic answers**: Check if PDF rulebook is indexed in Qdrant
2. **"Not specified" fallback too frequent**: Increase RAG search limit (edit service code)
3. **High latency**: Enable caching (AI-05), check Redis connection
4. **Inconsistent behavior**: Verify LLM model configuration in appsettings.json
5. **No agents returned**: Check database seed data migration applied

**Debugging Tips**:

```bash
# Check agents in database
psql -d meepleai -c "SELECT * FROM agents;"

# View agent endpoint logs
docker compose logs -f api | grep "QA request from user"

# Test RAG pipeline
curl -X POST http://localhost:8080/api/v1/agents/qa \
  -H "Content-Type: application/json" \
  -d '{"gameId": "tic-tac-toe", "query": "How to win?"}'
```

---

**Document Version**: 1.0.0
**Last Updated**: 2025-10-18
**Author**: Claude Code (Research)
**Confidence Score**: 0.95 (verified from source code)

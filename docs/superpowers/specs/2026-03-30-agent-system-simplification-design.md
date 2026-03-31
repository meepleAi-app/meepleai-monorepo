# Agent System Simplification

**Date**: 2026-03-30
**Status**: Approved
**Scope**: KnowledgeBase bounded context — domain model, strategies, execution paths

---

## Problem

The agent system has three overlapping concepts with no clear relationship:

- `AgentTypology` — session config (name, base prompt, parameters)
- `Agent` — runtime entity (gameId, invocation tracking)
- `AgentDefinition` — admin template (prompts, tools, strategy, lifecycle)

Five to six execution paths, two of which have a broken vector search since the Qdrant→pgvector migration (PR #165). Thirteen strategies defined but nine with no executor, most taken from academic benchmarks that do not apply to deterministic rulebook text.

---

## Goals

1. Single "agent" concept in the domain
2. Four strategies with real implementations
3. Three execution paths, all working
4. Delete legacy code and tests completely — no deprecation wrappers

---

## Domain Model

### AgentDefinition as the single concept

`AgentDefinition` absorbs `Agent` and `AgentTypology`. Additions to the existing schema:

```csharp
bool IsSystemDefined   // seeded by the system, not editable by users
string? TypologySlug   // "arbitro" | "game-master" | "chat" (fast lookup)
Guid? GameId           // optional game association (from Agent)
int InvocationCount    // from Agent
DateTime? LastInvokedAt // from Agent
```

Everything else (`Name`, `Description`, `Config`, `Strategy`, `Prompts`, `Tools`, `Status`, `IsActive`, `KbCardIds`, `ChatLanguage`) stays unchanged.

### AgentTierLimits and AgentDefaults

Move from the `Agent` scope to `AgentDefinition` scope. No logic change.

### Deleted entities

- `Agent` + `AgentConfiguration` + `AgentGameStateSnapshot`
- `AgentTypology`
- All repositories, EF configurations, and domain events for the above

---

## Strategies

Reduced from 13 to 4:

| Strategy | Default params | When to use |
|----------|---------------|-------------|
| `HybridSearch` | vectorWeight=0.7, topK=10, minScore=0.55 | Default for all agents |
| `RetrievalOnly` | topK=10, minScore=0.55 | Debug, zero-cost inspection |
| `SentenceWindowRAG` | windowSize=3, topK=5, minScore=0.6 | When adjacent sentences add context (exceptions, clarifications) |
| `ColBERTReranking` | topK=5, rerankTopN=20, minScore=0.6 | When retrieval precision matters; uses existing `CrossEncoderRerankerClient` |

Default for all new `AgentDefinition` instances: `HybridSearch`.

### Deleted strategies

`MultiModelConsensus`, `MultiAgentRAG`, `RAGFusion`, `IterativeRAG`, `StepBackPrompting`, `QueryExpansion`, `QueryDecomposition`, `CitationValidation`, `ConfidenceScoring`, `VectorOnly`, `SingleModel`

---

## Execution Paths

### Path 1 — ChatWithSessionAgentCommand (production, SSE streaming)

No behavioral change. The only wiring change: replace `AgentTypologyId` lookup with `AgentDefinitionId` lookup using `TypologySlug` for system agents.

Handles: session-based chat with full RAG pipeline, AgentMemory context, conversation summarization, copyright tier resolution.

### Path 2 — PlaygroundChatCommand (admin lab, SSE streaming)

Already uses `AgentDefinition` correctly. No change needed.

Absorbs `InvokeAgentCommand`: requesting `RetrievalOnly` strategy in the playground is equivalent to the old invoke — vector search, no LLM, returns chunks.

### Path 3 — AskArbiterCommand (structured verdict, non-streaming)

Kept because the frontend requires a structured `ArbiterVerdictDto` (Verdict, Confidence, IsConclusive, Citations) that cannot be produced by a streaming path.

**Fix**: replace the hardcoded empty search result with `IHybridSearchService.SearchAsync()`.

**Input change**: accepts `AgentDefinitionId` instead of `AgentId`. The arbiter `AgentDefinition` is a system-defined entry with `TypologySlug = "arbitro"`.

### Deleted paths

| Command | Reason |
|---------|--------|
| `AskAgentQuestionCommand` | Broken retrieval, redundant with PlaygroundChat |
| `InvokeAgentCommand` | Absorbed by PlaygroundChat with RetrievalOnly strategy |
| `InvokeChessAgentCommand` | Chess domain removed entirely |

---

## AgentTypology — Full Removal

`AgentTypology` is not only absorbed into `AgentDefinition` — the entire typology subsystem is deleted, including its proposal/approval workflow and phase-model configuration feature.

### Application layer — delete entirely

**Lifecycle commands** (Propose/Approve/Reject/Create/Update/Delete):
- `ProposeAgentTypologyCommand` + Handler + Validator
- `ApproveAgentTypologyCommand` + Handler + Validator
- `RejectAgentTypologyCommand` + Handler + Validator
- `CreateAgentTypologyCommand` + Handler + Validator
- `UpdateAgentTypologyCommand` + Handler + Validator
- `DeleteAgentTypologyCommand` + Handler + Validator
- `CreateAgentTypologyWithPhaseModelsCommand` + Handler + Validator

**Phase-model infrastructure** (tied exclusively to multi-phase strategies being deleted):
- `StrategyPhaseModelsDto`, `PhaseModelConfigurationDto`, `AgentTypologyWithCostDto`
- Cost estimation logic in `CreateAgentTypologyWithPhaseModelsCommandHandler`

**Testing command**:
- `TestAgentTypologyCommand` + Handler + Validator — absorbed by PlaygroundChat

**Queries and DTOs**:
- `GetTypologyByIdQuery` + Handler
- All `GetAll*AgentTypologies*` queries + handlers
- `AgentTypologyDto`

**Domain events**:
- `TypologyApprovedEvent`, `TypologyRejectedEvent`, `AgentSessionTypologyChangedEvent`

### Domain layer — delete entirely
- `AgentTypology` aggregate root
- `TypologyPromptTemplate` entity
- `TypologyStatus` value object, `TypologyProfile` value object
- `IAgentTypologyRepository`

### Infrastructure layer — delete entirely
- `AgentTypologyRepository`
- `AgentTypologyEntity` + `AgentTypologyEntityConfiguration`
- `TypologyPromptTemplateEntity` + `TypologyPromptTemplateEntityConfiguration`
- `MeepleAiDbContext.AgentTypologies` and `MeepleAiDbContext.TypologyPromptTemplates` DbSets

### Routing — delete entirely
- `AdminAgentTypologyEndpoints.cs`
- `AgentTypologyEndpoints.cs`

---

## Files to Update (Cross-BC and Session Dependencies)

These files are **not deleted** but require targeted changes because they reference `AgentTypology` or `Agent`.

### 1. `AgentSession` domain entity

`AgentSession.TypologyId` → `AgentDefinitionId`. The field rename follows the FK migration.

```csharp
// Before
public Guid TypologyId { get; private set; }
public void UpdateTypology(Guid newTypologyId) { ... }

// After
public Guid AgentDefinitionId { get; private set; }
public void UpdateAgentDefinition(Guid newAgentDefinitionId) { ... }
```

Domain event `AgentSessionTypologyChangedEvent` → rename to `AgentSessionDefinitionChangedEvent`.

### 2. `UpdateAgentSessionTypologyCommand` + Handler

Rename to `UpdateAgentSessionDefinitionCommand`. Replace `IAgentTypologyRepository` with `IAgentDefinitionRepository`. Check `agentDefinition.IsActive` instead of `typology.Status == Approved`.

### 3. `UserLibrary/SaveAgentConfigCommandHandler`

Cross-BC dependency on `IAgentTypologyRepository` (imported from KnowledgeBase). Replace with `IAgentDefinitionRepository`. Change validation from `TypologyStatus.Approved` to `definition.IsActive && definition.Status == AgentDefinitionStatus.Published`.

The `SaveAgentConfigCommand.TypologyId` parameter → rename to `AgentDefinitionId`.

### 4. `AutoCreateAgentOnPdfReadyHandler`

Replace `IAgentTypologyRepository` with `IAgentDefinitionRepository`. Change lookup from "first approved typology" to "first system-defined, active AgentDefinition":

```csharp
// Before
var defaultTypology = typologies.FirstOrDefault(t => t.Status == TypologyStatus.Approved);

// After
var defaultDefinition = definitions.FirstOrDefault(d => d.IsSystemDefined && d.IsActive);
```

Pass `AgentDefinitionId` to `CreateGameAgentCommand` instead of `TypologyId`.

### 5. `CreateGameAgentCommand` + Handler

`TypologyId` parameter → `AgentDefinitionId`. Handler uses `IAgentDefinitionRepository` instead of `IAgentTypologyRepository`.

### 6. `AgentSeeder` (`Infrastructure/Seeders/Catalog/AgentSeeder.cs`)

The seeder currently creates `AgentTypologyEntity` + `AgentEntity` + `AgentConfigurationEntity`. After simplification:

- Remove: `EnsureAgentTypologyAsync`, all `AgentTypologyEntity` and `TypologyPromptTemplateEntity` creation
- Remove: `AgentEntity` / `AgentConfigurationEntity` creation (those tables are dropped)
- Replace with: ensure a system-defined `AgentDefinitionEntity` with `TypologySlug = "game-master"` exists, seeded once idempotently

System-defined agent definitions are better seeded in the EF migration or a dedicated `AgentDefinitionSeeder`; the `AgentSeeder` reduces to a no-op or is removed.

### 7. `DatabaseSync/ListTablesHandler`

Remove `"typology_prompt_templates"` from the table-to-BC mapping dictionary.

---

## Chess Agent — Full Removal

All chess-specific code is deleted:

**Backend**: `InvokeChessAgentCommand`, `InvokeChessAgentCommandHandler`, `SearchChessKnowledgeQuery`, chess DTOs (`ChessAgentResponse`, `ChessAnalysis`, `ParsedChessResponse`), FEN validation logic, move parsing regex.

**Frontend**: chess agent pages, components, hooks, API types.

No migration of chess data — chess was not a user-generated content feature.

---

## Database Migration

```sql
-- 1. Extend agent_definitions
ALTER TABLE agent_definitions
  ADD COLUMN is_system_defined  BOOLEAN     NOT NULL DEFAULT FALSE,
  ADD COLUMN typology_slug      VARCHAR(50),
  ADD COLUMN game_id            UUID        REFERENCES games(id),
  ADD COLUMN invocation_count   INT         NOT NULL DEFAULT 0,
  ADD COLUMN last_invoked_at    TIMESTAMPTZ;

-- 2. Migrate agent_typologies → agent_definitions
-- Preserve the original typology ID so agent_sessions FK update is a direct copy
INSERT INTO agent_definitions (
  id, name, description, type_value, type_description,
  model, temperature, max_tokens,
  is_system_defined, typology_slug,
  status, is_active, created_at
)
SELECT
  id, name, description, 'system', name,
  default_model, default_temperature, default_max_tokens,
  TRUE, slug,
  'Published', TRUE, created_at
FROM agent_typologies;

-- 3. Migrate agent_sessions FK (direct copy — IDs are preserved)
ALTER TABLE agent_sessions
  ADD COLUMN agent_definition_id UUID REFERENCES agent_definitions(id);

UPDATE agent_sessions
SET agent_definition_id = typology_id;

-- Verify no nulls before proceeding
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM agent_sessions WHERE agent_definition_id IS NULL) THEN
    RAISE EXCEPTION 'Migration incomplete: agent_sessions rows with null agent_definition_id';
  END IF;
END $$;

ALTER TABLE agent_sessions DROP COLUMN typology_id;

-- 4. Drop legacy tables (order matters: child tables first)
DROP TABLE typology_prompt_templates;   -- FK child of agent_typologies
DROP TABLE agents;
DROP TABLE agent_configurations;
DROP TABLE agent_game_state_snapshots;
DROP TABLE agent_typologies;
```

---

## Frontend Changes

**Delete**:
- `/components/admin/agent-typologies/*`
- `/hooks/agent/useAgentTypology*`
- `/lib/agent/typology*`
- All chess-related pages and components (`/app/(authenticated)/...chess*`, chess hooks, chess API types)

**Update**:
- Admin agent management pages: point to `agent_definitions` instead of `agent_typologies`
- `AgentSession` types: replace `typologyId` with `agentDefinitionId`
- API client types: remove deleted endpoints, update arbiter endpoint signature
- `SaveAgentConfig` API call: `typologyId` → `agentDefinitionId`
- `UpdateAgentSessionTypology` API call: `newTypologyId` → `newAgentDefinitionId`

---

## API Surface Changes

```
REMOVE  POST /api/v1/agents/chat/ask                        ← AskAgentQuestion
REMOVE  POST /api/v1/agents/{id}/invoke                     ← InvokeAgent
REMOVE  POST /api/v1/chess/ask                              ← Chess agent
REMOVE  GET|POST /api/v1/agent-typologies/*                 ← AgentTypology CRUD (user-facing)
REMOVE  GET|POST /api/v1/admin/agent-typologies/*           ← AgentTypology CRUD (admin) + Propose/Approve/Reject

KEEP    POST /api/v1/agents/sessions/{id}/chat              ← ChatWithSession (wired to AgentDefinition)
KEEP    POST /api/v1/agents/definitions/{id}/playground     ← Playground
FIX     POST /api/v1/agents/{id}/arbiter                    ← input: AgentDefinitionId, fix retrieval
RENAME  PATCH /api/v1/agents/sessions/{id}/typology         ← becomes .../definition (UpdateAgentSessionDefinition)
```

---

## What Does Not Change

- `AgentSession` lifecycle logic (launch, end, state update) — only the FK field and the update-typology command are renamed
- `RagPromptAssemblyService` logic
- `AgentMemoryContextBuilder`
- `ConversationSummarizer`
- `CopyrightTierResolver`
- `HybridSearchService` and `PgVectorStoreAdapter`
- `LlmProviderFactory` / `OpenRouterLlmClient` / `OllamaLlmClient`
- All document processing and embedding pipeline

---

## Risk

**Medium**: `AgentSession` is the most used table in production. The FK migration (`typology_id` → `agent_definition_id`) must run before the old tables are dropped, and must be verified with a row-count check.

**Medium**: `SaveAgentConfigCommandHandler` (UserLibrary BC) and `AutoCreateAgentOnPdfReadyHandler` have cross-BC dependencies on `IAgentTypologyRepository`. Both must be updated before `AgentTypology` is removed or the build will fail. These are internal-only features (not user-visible in production UI), so functional risk is low.

**Low**: removing chess and the dead command paths has no production user impact — these paths were either broken (empty retrieval) or not surfaced in the production UI.

**Low**: `AgentSeeder` rewrite affects dev and staging seed data only. Production does not depend on the seeder.

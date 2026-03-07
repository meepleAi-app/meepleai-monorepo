# RAG Mock Removal - Implementation Plan

**Epic**: #5309
**Date**: 2026-03-06
**Base Branch**: main-dev
**PR Target**: main-dev

---

## Issues

| Phase | Issue | Title | Effort | Type |
|-------|-------|-------|--------|------|
| 1 | #5310 | KB Tab PDF state fix | 30min | FE only |
| 2 | #5311 | RAG Config backend + frontend | 2-3h | Fullstack |
| 3 | #5312 | Pipeline save endpoint + frontend | 1-2h | Fullstack |
| 4 | #5313 | Session Agent LLM wiring | 3-4h | BE only |
| 5 | #5314 | Admin Strategy CRUD | 2-3h | BE only |

---

## Phase 1: KB Tab PDF State (#5310)

**Branch**: `fix/issue-5310-kb-pdf-state`
**Scope**: 1 file change, ~15 lines

### Steps

1. **Read** `KnowledgeBaseTab.tsx` and `knowledgeBaseClient.ts`
2. **Replace** hardcoded `pdfState = 'ready'` with:
   - Call `api.knowledgeBase.getEmbeddingStatus(gameId)` via `useQuery`
   - Map `KnowledgeBaseStatus.embeddingState` to `PdfState` enum
3. **Remove** TODO comment on line 175
4. **Test**: Update existing tests, verify status changes render correctly
5. **PR** → code review → merge

### Key Files
- `apps/web/src/components/shared-games/KnowledgeBaseTab.tsx` (edit)
- `apps/web/src/lib/api/clients/knowledgeBaseClient.ts` (read-only, already has endpoint)

---

## Phase 2: RAG Config Endpoints (#5311)

**Branch**: `feat/issue-5311-rag-config-api`
**Scope**: ~12 new files (BE) + 1 edit (FE)

### Backend Steps

1. **Domain Entity** — `RagUserConfig.cs`
   - Fields: UserId, GenerationParams (JSON), RetrievalParams (JSON), RerankerConfig (JSON), ActiveStrategy, ModelConfig (JSON)
   - Factory method: `RagUserConfig.Create(userId)`
   - Update method: `UpdateConfig(params)`

2. **EF Configuration** — `RagUserConfigConfiguration.cs`
   - Table: `RagUserConfigs`
   - JSONB columns for nested config objects
   - Unique index on UserId

3. **Migration** — `AddRagUserConfigTable`

4. **Repository** — `IRagConfigRepository` + `RagConfigRepository`
   - `GetByUserIdAsync(userId)`
   - `UpsertAsync(config)`

5. **CQRS Commands/Queries**:
   - `GetRagConfigQuery(UserId)` → `GetRagConfigQueryHandler` → returns DTO or defaults
   - `SaveRagConfigCommand(UserId, ConfigDto)` → `SaveRagConfigCommandHandler` → upsert
   - `SaveRagConfigCommandValidator` (FluentValidation)

6. **DTOs** — `RagConfigDto.cs` matching frontend store shape

7. **Endpoints** in `KnowledgeBaseEndpoints.cs`:
   - `GET /api/v1/rag/config` → GetRagConfigQuery (uses auth userId)
   - `PUT /api/v1/rag/config` → SaveRagConfigCommand (uses auth userId)

8. **DI Registration** in `KnowledgeBaseServiceExtensions.cs`

### Frontend Steps

9. **Edit** `ragConfigStore.ts`:
   - `saveConfig()`: Replace setTimeout with `httpClient.put('/api/v1/rag/config', config)`
   - `loadUserConfig()`: Replace setTimeout with `httpClient.get('/api/v1/rag/config')`
   - Remove TODO comments

10. **Tests**: Backend handler tests + verify frontend store calls

### Key Architecture Decision
- Store config as JSONB (flexible schema, matches frontend store shape)
- One config per user (not per agent — agent-specific config lives in AgentDefinition)
- Return sensible defaults when no saved config exists

---

## Phase 3: Pipeline Save Endpoint (#5312)

**Branch**: `feat/issue-5312-pipeline-save`
**Scope**: ~8 new files (BE) + 1 edit (FE)

### Backend Steps

1. **Check existing** `ICustomRagPipelineRepository` — already registered, verify interface methods

2. **EF Entity** — `CustomRagPipelineEntity.cs` (if not exists)
   - Id, Name, Description, SchemaVersion, DefinitionJson (JSONB), UserId, CreatedAt, UpdatedAt

3. **Migration** (if table doesn't exist)

4. **CQRS**:
   - `SavePipelineCommand(UserId, PipelineDefinitionDto)` → validate schema → persist
   - `GetPipelinesQuery(UserId)` → list user pipelines
   - `GetPipelineByIdQuery(Id)` → single pipeline
   - `DeletePipelineCommand(Id, UserId)` → soft delete

5. **Validation**: Use existing `PipelineSchemaValidator` to validate before save

6. **Endpoints**:
   - `POST /api/v1/rag/pipelines` → SavePipelineCommand
   - `GET /api/v1/rag/pipelines` → GetPipelinesQuery
   - `GET /api/v1/rag/pipelines/{id}` → GetPipelineByIdQuery
   - `DELETE /api/v1/rag/pipelines/{id}` → DeletePipelineCommand

### Frontend Steps

7. **Edit** `pipelineBuilderStore.ts`:
   - `savePipeline()`: Replace setTimeout with real POST call
   - Remove commented-out fetch code
   - Remove TODO

8. **Tests**: Backend handlers + frontend integration

### Key Architecture Decision
- Store full PipelineDefinition as JSONB (flexible, matches DAG model)
- Use existing PipelineSchemaValidator for validation
- Scoped to user (userId from auth)

---

## Phase 4: Session Agent LLM Wiring (#5313)

**Branch**: `feat/issue-5313-session-agent-llm`
**Scope**: 2 handler edits (BE), most complex phase

### Analysis First

1. **Read fully**:
   - `ChatWithSessionAgentCommandHandler.cs` — understand current flow, what context it builds
   - `ChatCommandHandlers.cs` (AskSessionAgent) — understand current flow
   - `HybridLlmService.cs` — understand GenerateCompletionStreamAsync API
   - `IHybridSearchEngine.cs` — understand search API for RAG
   - `IAgentPromptBuilder.cs` — understand prompt building

### Handler 1: ChatWithSessionAgentCommandHandler

2. **Inject dependencies**:
   - `ILlmService` (HybridLlmService)
   - `IAgentPromptBuilder` (for prompt construction)
   - Game repository (for game context)

3. **Replace placeholder** (lines 147-162):
   - Resolve game title from repository
   - Build system prompt from agent definition + game context
   - Call `GenerateCompletionStreamAsync(systemPrompt, userQuestion, userId)`
   - Map `StreamChunk` → `RagStreamingEvent(Token, ...)`
   - Yield streaming events

4. **Error handling**: Wrap LLM call in try/catch, yield Error event on failure

### Handler 2: AskSessionAgentCommandHandler

5. **Inject dependencies**:
   - `IHybridSearchEngine` (for RAG retrieval)
   - `ILlmService` (for answer generation)

6. **Replace stub** (lines 134-139):
   - Search: `IHybridSearchEngine.SearchAsync(question, gameId)`
   - Build prompt: question + retrieved context chunks
   - Generate: `ILlmService.GenerateCompletionAsync(prompt, userId)`
   - Return real answer with confidence score

7. **Tests**: Unit tests with mocked ILlmService + IHybridSearchEngine

### Key Architecture Decision
- Use existing `IAgentPromptBuilder` for prompt construction (not manual string building)
- Streaming handler yields Token events (matches frontend SSE consumer)
- Non-streaming handler (AskSessionAgent) uses synchronous completion
- Both handlers must handle LLM unavailability gracefully

---

## Phase 5: Admin Strategy CRUD (#5314)

**Branch**: `feat/issue-5314-admin-strategy-crud`
**Scope**: ~10 new files (BE)

### Steps

1. **Analyze existing**:
   - `AdminStrategyEndpoints.cs` — current placeholder routes
   - `ITierStrategyAccessRepository` — may already have what we need
   - `IStrategyModelMappingRepository` — related functionality
   - `AgentStrategy` value object — predefined strategies

2. **Domain**: Determine if we need a new aggregate or can use existing repos
   - If TierStrategyAccess already covers CRUD → wire to existing
   - If not → create `AdminStrategyConfig` entity

3. **CQRS**:
   - `ListStrategiesQuery` → handler returns all strategies with configs
   - `GetStrategyByIdQuery(Id)` → single strategy detail
   - `CreateStrategyCommand(Name, Config)` → create custom strategy
   - `UpdateStrategyCommand(Id, Config)` → update strategy
   - `DeleteStrategyCommand(Id)` → soft delete

4. **Validators**: FluentValidation for Create/Update

5. **Wire endpoints**: Replace lambda placeholders with `IMediator.Send()`

6. **Tests**: Handler tests for all 5 operations

### Key Architecture Decision
- Check if this is about TierStrategyAccess (tier→strategy mapping) or custom strategy definitions
- Follow pattern from other admin CQRS (AdminKnowledgeBaseEndpoints)
- Use RequireAdminSession() for auth

---

## Execution Strategy

### Per-phase workflow (following /implementa)

```
For each phase:
1. git checkout main-dev && git pull
2. git checkout -b <branch>
3. Implement (backend first, then frontend)
4. dotnet build && dotnet test (backend)
5. pnpm test && pnpm typecheck (frontend, if applicable)
6. git commit + push
7. gh pr create --base main-dev
8. Code review → fix → merge
9. Cleanup branch
```

### Parallelization Opportunities

- Phase 1 is independent (quick fix)
- Phases 2 and 3 are independent (different endpoints, different stores)
- Phase 4 depends on nothing (existing services)
- Phase 5 depends on nothing

**Optimal order**: 1 → (2 || 3) → 4 → 5
**Or sequential**: 1 → 2 → 3 → 4 → 5

### Risk Assessment

| Phase | Risk | Mitigation |
|-------|------|------------|
| 1 | Low | Simple 1-file change |
| 2 | Medium | New DB table + migration | Test with existing config shape |
| 3 | Low-Medium | Repository exists, needs wiring | Use PipelineSchemaValidator |
| 4 | High | LLM integration, streaming | HybridLlmService proven; test with Ollama |
| 5 | Medium | Unclear scope | Analyze existing repos first |

### MCP/Tool Selection

| Phase | Primary Tools |
|-------|---------------|
| 1 | Read, Edit (frontend only) |
| 2 | Edit, Write (fullstack), Context7 (EF patterns) |
| 3 | Edit, Write (fullstack) |
| 4 | Read (deep analysis), Edit, Sequential (complex wiring) |
| 5 | Read (analysis), Edit, Write |

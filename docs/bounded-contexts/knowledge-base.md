# KnowledgeBase Bounded Context

**RAG System (Production) + AgentTypology POC (6-month lifecycle)**

> ⚠️ **Two Systems**: (1) RAG - Hybrid search, Chat, Q&A | (2) AgentTypology POC → Future: LangGraph Multi-Agent (Issue #3780)

---

## 📋 Responsibilities

| System | Features |
|--------|----------|
| **RAG (Production)** | Hybrid search (Vector+Keyword RRF) • Multi-model LLM consensus • Chat threads • 5-layer confidence validation |
| **AgentTypology POC** | Template-based agents • Approval workflow (Draft→Approved) • Phase-model config • Session management • Runtime config |

---

## 🏗️ Domain Model

### RAG Entities

| Entity | Key Fields | Domain Methods |
|--------|-----------|----------------|
| **ChatThread** | Id, UserId, GameId, AgentId, Title, Status (Active\|Closed), Messages[] | AddMessage(), UpdateMessage(), DeleteMessage(), Close(), Reopen(), Export(json\|markdown) |
| **ChatMessage** | Id, ThreadId, Content, Role (User\|Assistant\|System), ConfidenceScore, Sources[], CreatedAt | Update(), Delete() |
| **EmbeddingChunk** | Id, DocumentId, Content, PageNumber, ChunkIndex, Embedding (1024-dim BGE-M3), CreatedAt | N/A (read-only) |

### AgentTypology POC Entities

| Entity | Key Fields | Domain Methods |
|--------|-----------|----------------|
| **AgentTypology** | Id, Name, Description, BasePrompt, DefaultStrategy, Status (Draft\|Approved\|Rejected), PhaseModels, CreatedBy, ApprovedBy | Approve(), Reject(), Archive(), UpdatePhaseModels() |
| **AgentSession** | Id, AgentId, GameSessionId, TypologyId, Config, CurrentGameState, StartedAt, IsActive | UpdateGameState(), UpdateTypology(), UpdateConfig(), End() |
| **AgentTestResult** | Id, TypologyId, TestQuery, Success, Response, ConfidenceScore, ErrorMessage, TestedAt | N/A (audit trail) |

### Value Objects

| Value Object | Values | Usage |
|--------------|--------|-------|
| **AgentStrategyType** | Fast (~2K tok), Balanced (~2.8K), Precise (~22K), Expert, Consensus, Custom | Cost/accuracy tradeoff |
| **PhaseModelConfiguration** | RetrievalModel, AnalysisModel, SynthesisModel, ValidationModel, CragEvalModel, WebSearchModel, ConsensusVoter1-3 | Per-phase LLM selection |
| **AgentConfig** | ModelType, Temperature (0.0-2.0), MaxTokens, RagStrategy, RagParams | Runtime config |

---

## 📡 Endpoints (45 Total)

### RAG System (17 Endpoints)

| Operation | Method | Endpoint | Auth | Key Features |
|-----------|--------|----------|------|--------------|
| **Search** | POST | `/api/v1/knowledge-base/search` | Session | Hybrid (Vector+Keyword), topK, minScore, language |
| **Ask Question** | POST | `/api/v1/knowledge-base/ask` | Session | Full RAG pipeline, confidence scoring, citations |
| **Create Thread** | POST | `/api/v1/chat-threads` | Session | Auto-add initialMessage if provided |
| **Get Thread** | GET | `/api/v1/chat-threads/{id}` | Owner | Full thread with messages |
| **List Threads** | GET | `/api/v1/chat-threads?gameId={id}` | Session | Filter by game |
| **My Chat History** | GET | `/api/v1/knowledge-base/my-chats` | Session | Paginated, skip/take |
| **Delete Thread** | DELETE | `/api/v1/chat-threads/{id}` | Owner | 204 No Content |
| **Add Message** | POST | `/api/v1/chat-threads/{id}/messages` | Owner | Updates LastMessageAt |
| **Update Message** | PUT | `/api/v1/chat-threads/{id}/messages/{msgId}` | Session | Edit existing |
| **Delete Message** | DELETE | `/api/v1/chat-threads/{id}/messages/{msgId}` | Session | 204 No Content |
| **Close Thread** | POST | `/api/v1/chat-threads/{id}/close` | Owner | Sets Status=Closed |
| **Reopen Thread** | POST | `/api/v1/chat-threads/{id}/reopen` | Owner | Sets Status=Active |
| **Update Title** | PATCH | `/api/v1/chat-threads/{id}` | Owner | 1-200 chars |
| **Export Chat** | GET | `/api/v1/chat-threads/{id}/export?format={json\|markdown}` | Owner | Full export |

**Search Request**:
```json
{"gameId": "guid", "query": "How do I score?", "topK": 5, "minScore": 0.6, "searchMode": "Hybrid", "language": "it"}
```

**Ask Question Response**:
```json
{"answer": "...", "searchConfidence": 0.82, "llmConfidence": 0.91, "finalConfidence": 0.865, "isLowQuality": false, "sources": [...], "citations": ["rulebook.pdf:p12"], "tokensUsed": 2845, "latencyMs": 1250}
```

**Confidence Thresholds** (ADR-005):
- Search: ≥0.70 | LLM: ≥0.70 | Low-quality: <0.60 triggers warning

### AgentTypology POC (24 Endpoints)

| Operation | Method | Endpoint | Auth | Purpose |
|-----------|--------|----------|------|---------|
| **List Typologies** | GET | `/api/v1/agent-typologies` | Role-based | User: Approved only • Editor: +own Draft • Admin: ALL |
| **Get by ID** | GET | `/api/v1/agent-typologies/{id}` | Session | Single typology |
| **Get Pending** | GET | `/api/v1/agent-typologies/pending` | Admin | Approval queue |
| **Get My Proposals** | GET | `/api/v1/agent-typologies/my-proposals` | Editor/Admin | Own drafts |
| **Propose Typology** | POST | `/api/v1/agent-typologies/propose` | Editor/Admin | Creates Draft, notifies admins |
| **Test Typology** | POST | `/api/v1/agent-typologies/{id}/test` | Editor/Admin | Sandbox test, no user charge |
| **Create (Admin)** | POST | `/api/v1/admin/agent-typologies` | Admin | Full config + auto-approve option |
| **Update** | PUT | `/api/v1/admin/agent-typologies/{id}/phase-models` | Admin | Edit config |
| **Delete** | DELETE | `/api/v1/admin/agent-typologies/{id}` | Admin | 204 No Content |
| **Approve** | PUT | `/api/v1/admin/agent-typologies/{id}/approve` | Admin | Notify proposer, activate |
| **Reject** | PUT | `/api/v1/admin/agent-typologies/{id}/reject` | Admin | Notify proposer, reason required |
| **Launch Agent** | POST | `/api/v1/game-sessions/{id}/agent/launch` | Session | Create AgentSession |
| **Chat with Agent** | POST | `/api/v1/game-sessions/{id}/agent/chat` | Session | **SSE Stream** |
| **End Agent** | DELETE | `/api/v1/game-sessions/{id}/agent` | Session | Preserve chat log |
| **Update State** | PUT | `/api/v1/game-sessions/{id}/agent/state` | Session | Sync game board |
| **Switch Typology** | PATCH | `/api/v1/game-sessions/{id}/agent/typology` | Session | Change mid-session |
| **Update Config** | PATCH | `/api/v1/game-sessions/{id}/agent/config` | Session | Runtime tuning |
| **Get Metrics** | GET | `/api/v1/admin/agents/metrics` | Admin | Aggregated stats |
| **Get Agent Metrics** | GET | `/api/v1/admin/agents/metrics/{id}` | Admin | Single agent stats |
| **Top Agents** | GET | `/api/v1/admin/agents/metrics/top` | Admin | Ranked by invocations\|cost\|confidence |
| **Cost Estimate** | GET | `/api/v1/admin/agent-typologies/{id}/cost-estimate` | Admin | Projected costs |

**Strategy Presets** (Issue #3245):

| Strategy | Tokens/Query | Cost/Query | Use Case |
|----------|--------------|------------|----------|
| Fast | ~2,060 | $0.008 | High volume, quick answers |
| Balanced | ~2,820 | $0.012 | Standard accuracy |
| Precise | ~22,396 | $0.089 | High-stakes rules |
| Expert | ~30,000+ | $0.15+ | Research, multi-hop |
| Consensus | ~8,400 | $0.036 | Vote-based validation |

**SSE Chat Response**:
```
event: token
data: {"token": "You"}

event: complete
data: {"fullResponse": "...", "confidence": 0.82, "sources": [...], "tokensUsed": 2340}
```

### Context Engineering (2 Endpoints)

| Operation | Method | Endpoint | Purpose |
|-----------|--------|----------|---------|
| **Assemble Context** | POST | `/api/v1/context-engineering/assemble` | Multi-source context assembly (StaticKnowledge, ConversationMemory, AgentState, ToolMetadata) |
| **Get Sources** | GET | `/api/v1/context-engineering/sources` | List available sources |

**Assemble Request**:
```json
{"query": "...", "gameId": "guid", "maxTotalTokens": 8000, "minRelevance": 0.5, "sourcePriorities": {"gameState": 10, "rules": 8, "conversation": 6}}
```

---

## 🔐 Authorization Matrix

| Endpoint Pattern | Anonymous | User | Editor | Admin |
|------------------|-----------|------|--------|-------|
| `/knowledge-base/search` | ❌ | ✅ | ✅ | ✅ |
| `/knowledge-base/ask` | ❌ | ✅ | ✅ | ✅ |
| `/chat-threads` (GET) | ❌ | ✅ (own) | ✅ (own) | ✅ (all) |
| `/agent-typologies/propose` | ❌ | ❌ | ✅ | ✅ |
| `/agent-typologies/{id}/test` | ❌ | ❌ | ✅ (own) | ✅ (all) |
| `/admin/agent-typologies/{id}/approve` | ❌ | ❌ | ❌ | ✅ |
| `/admin/agents/metrics` | ❌ | ❌ | ❌ | ✅ |

**Rate Limits**:

| Endpoint | Limit | Window | Scope |
|----------|-------|--------|-------|
| `/knowledge-base/ask` | 30 req | 1 min | Per user |
| `/agent-typologies/propose` | 5 req | 1 hour | Per editor |
| `/agent-typologies/{id}/test` | 10 req | 1 hour | Per typology |
| `/game-sessions/{id}/agent/chat` | 60 msg | 1 min | Per session |

---

## 🔄 Domain Events

| Event | When | Payload | Subscribers |
|-------|------|---------|-------------|
| ChatThreadCreatedEvent | Thread created | ThreadId, UserId, GameId | Administration (audit) |
| MessageAddedEvent | Message added | ThreadId, MessageId, Role | SessionTracking (activity) |
| ThreadClosedEvent | Thread closed | ThreadId, ClosedBy | Administration |
| ChunksEmbeddedEvent | Document chunked | DocumentId, ChunkCount | KnowledgeBase (Qdrant index) |
| TypologyProposedEvent | Editor proposes | TypologyId, ProposedBy, Name | UserNotifications (admins) |
| TypologyApprovedEvent | Admin approves | TypologyId, ApprovedBy | UserNotifications (proposer) |
| TypologyRejectedEvent | Admin rejects | TypologyId, RejectedBy, Reason | UserNotifications (proposer) |
| AgentSessionLaunchedEvent | Session started | AgentSessionId, UserId, TypologyId | Administration (tracking) |
| AgentSessionEndedEvent | Session ended | AgentSessionId, Duration, MessageCount | Administration (analytics) |
| AgentInvokedEvent | Agent query | AgentId, UserId, Tokens, Cost | Administration (billing) |

---

## 🔗 Integration Points

**Inbound**:
- GameManagement → Fetch game metadata
- DocumentProcessing → Receive chunked documents (`DocumentChunkedEvent`)
- UserLibrary → Link chat sessions to library entries

**Outbound**:
- Qdrant → Vector storage & similarity search
- Redis → 3-tier caching (memory → Redis → Qdrant)
- OpenRouter → GPT-4, Claude 3, DeepSeek (multi-model consensus)
- Embedding Service → sentence-transformers/all-MiniLM-L6-v2 (384-dim)
- Reranker Service → cross-encoder/ms-marco-MiniLM-L-6-v2

---

## 📊 Performance

### Caching (3-Tier)

| Query | Memory | Redis | Qdrant | TTL | Invalidation Trigger |
|-------|--------|-------|--------|-----|---------------------|
| SearchQuery | ✅ | ✅ | Fallback | 5min | DocumentChunkedEvent |
| AskQuestionQuery | ✅ | ✅ | N/A | 10min | ThreadUpdatedEvent |
| GetChatThreadById | ❌ | ✅ | N/A | 2min | MessageAddedEvent |
| GetAgentTypologies | ❌ | ✅ | N/A | 30min | TypologyApprovedEvent |
| GetAgentMetrics | ❌ | ✅ | N/A | 1hour | AgentInvokedEvent |

**Cache Hit Target**: >80%

### Indexes

```sql
-- Chat threads
CREATE INDEX idx_chatthreads_user_game ON ChatThreads(UserId, GameId) WHERE NOT IsDeleted;
CREATE INDEX idx_chatthreads_status ON ChatThreads(Status, LastMessageAt DESC);

-- Messages
CREATE INDEX idx_chatmessages_thread ON ChatMessages(ChatThreadId, CreatedAt);

-- Chunks (Qdrant handles vectors)
CREATE INDEX idx_chunks_document ON EmbeddingChunks(DocumentId, ChunkIndex);

-- Typologies
CREATE INDEX idx_typologies_status ON AgentTypologies(Status) WHERE NOT IsDeleted;
CREATE INDEX idx_typologies_created_by ON AgentTypologies(CreatedBy, Status);

-- Sessions
CREATE INDEX idx_agentsessions_game_active ON AgentSessions(GameSessionId, IsActive);
CREATE INDEX idx_agentsessions_user ON AgentSessions(UserId, StartedAt DESC);
CREATE INDEX idx_agentsessions_typology ON AgentSessions(TypologyId) WHERE IsActive;
```

### Targets

| Operation | Latency (P95) | Cache Hit |
|-----------|---------------|-----------|
| Hybrid search | <200ms | >80% |
| RAG Q&A (cached) | <500ms | >75% |
| RAG Q&A (uncached) | <2s | N/A |
| Chat message add | <50ms | N/A |
| Agent invocation | <3s | >70% |
| Typology list | <20ms | >90% |

---

## 🧪 Testing

### Coverage Target
- RAG System: **90%+** ✅
- AgentTypology POC: **0%** ⚠️ MISSING (Issue #3794 blocker)

### Unit Tests

**Location**: `tests/Api.Tests/KnowledgeBase/`

| Test Suite | Focus |
|------------|-------|
| ChatThread_Tests.cs | AddMessage, Export, Close/Reopen |
| HybridSearchService_Tests.cs | Vector+Keyword fusion, RRF logic |
| ConfidenceValidator_Tests.cs | 5-layer validation (ADR-005) |
| MultiModelConsensus_Tests.cs | 3-voter ensemble (ADR-007) |
| AgentTypology_Tests.cs | **MISSING** - Approve, Reject, Archive |
| AgentSession_Tests.cs | **MISSING** - Launch, UpdateState, End |
| ProposeTypologyValidator_Tests.cs | **MISSING** - Proposal validation |
| TestTypologyHandler_Tests.cs | **MISSING** - Sandbox testing logic |

### Integration Tests

**Tools**: Testcontainers (PostgreSQL, Redis, Qdrant)

**Scenarios**:
1. End-to-End RAG: Upload PDF → Extract → Embed → Search → Ask
2. Typology Lifecycle: Propose → Test → Approve → Invoke
3. Agent Session: Launch → Chat (5 turns) → Update state → End
4. Multi-Model Consensus: 3 voters, voting logic validation

### E2E Tests (Playwright)

**Location**: `apps/web/__tests__/e2e/knowledge-base/`

**Flows**:
1. RAG Chat: Navigate → Type question → See SSE stream → Verify citations
2. Typology Proposal (Editor): Navigate → Fill form → Test sandbox → Submit
3. Typology Approval (Admin): Review pending → View tests → Approve/Reject
4. Agent Session: Start game → Launch agent → Chat with SSE → Update state → End

---

## 📂 Code Structure

```
BoundedContexts/KnowledgeBase/
├── Domain/
│   ├── Entities/
│   │   ├── ChatThread.cs, ChatMessage.cs, EmbeddingChunk.cs  # RAG
│   │   └── AgentTypology.cs, AgentSession.cs, AgentTestResult.cs  # POC
│   ├── ValueObjects/
│   │   ├── MessageRole.cs, ThreadStatus.cs, AgentStrategy.cs
│   │   └── PhaseModelConfiguration.cs, AgentConfig.cs
│   ├── Repositories/ (Interfaces)
│   ├── Services/ (Interfaces)
│   │   ├── IHybridSearchService.cs, IMultiModelConsensusService.cs
│   │   ├── IConfidenceValidationService.cs, IContextEngineeringService.cs
│   │   └── AgentOrchestration/
│   └── Events/ (15+ events)
├── Application/
│   ├── Commands/ (25+)
│   ├── Queries/ (20+)
│   ├── Handlers/ (45+)
│   ├── DTOs/ (40+)
│   └── Validators/ (25+ FluentValidation)
└── Infrastructure/
    ├── Persistence/ (Repositories)
    ├── Services/ (Implementations)
    │   ├── QdrantVectorSearchService.cs, HybridSearchService.cs
    │   ├── OpenRouterLlmService.cs, ConsensusVotingService.cs
    │   └── ContextEngineeringService.cs
    └── DependencyInjection/
```

**Routing**: `Api/Routing/KnowledgeBaseEndpoints.cs` (RAG) + `AgentEndpoints.cs` (POC)

---

## 📈 KPIs

### RAG System

| Metric | Target | Description |
|--------|--------|-------------|
| Search Accuracy | >90% | Relevant chunks in top-5 |
| Answer Accuracy | >85% | Correct answers with confidence >0.7 |
| Low-Quality Rate | <10% | Answers below threshold |
| Avg Latency | <2s (P95) | End-to-end pipeline |
| Cache Hit Rate | >80% | Redis effectiveness |

### AgentTypology POC

| Metric | Target | Description |
|--------|--------|-------------|
| Approval Rate | >70% | Editor proposals approved |
| Avg Test Confidence | >0.75 | Sandbox results |
| Active Sessions | 50+ | Concurrent peak |
| Avg Session Duration | 15-30min | Typical interaction |
| Cost Per Session | <$0.50 | Fast/Balanced strategies |

---

## 🚨 Known Issues

### RAG System
- **Issue #3231** (BLOCKER): ResponseEnded crash in AskQuestionQueryHandler (0/20 tests passing)
- Language support: Italian + English only
- Context window: 8K max tokens
- Reranking latency: +100-200ms
- Consensus cost: 3x for ~5-8% accuracy gain

### AgentTypology POC
- **Test Coverage**: 0% (violates 90%+ target)
- **Documentation Gap** (Issue #3795): No POC architecture doc
- Session state limit: 64KB JSON (PostgreSQL text column)
- No concurrent session limits per user
- No typology versioning (updates overwrite)
- Test results retained forever (no cleanup)

---

## 📋 Future

**RAG Enhancements**:
- GraphRAG (entity relationships)
- Multi-document reasoning
- Temporal RAG (time-aware retrieval)
- Fine-tuned embeddings

**AgentTypology → LangGraph Migration** (6+ months):
1. Current (0-6mo): AgentTypology POC in production
2. Transition (6-7mo): LangGraph implementation (Tutor/Arbitro/Decisore)
3. Coexistence (7-8mo): Both systems, gradual migration
4. Completion (9mo): POC deprecated

**LangGraph Features** (Issue #3780):
- Specialized agents: Tutor (onboarding), Arbitro (rules), Decisore (strategy)
- Event-driven orchestration
- Advanced context engineering
- MCTS engine for strategic AI

---

## 🔗 Related Docs

**Architecture**:
- [ADR-001: Hybrid RAG](../01-architecture/adr/adr-001-hybrid-rag.md)
- [ADR-005: Cosine Similarity Consensus](../01-architecture/adr/adr-005-cosine-similarity-consensus.md)
- [ADR-007: Hybrid LLM](../01-architecture/adr/adr-007-hybrid-llm.md)
- [ADR-008: Streaming CQRS](../01-architecture/adr/adr-008-streaming-cqrs-migration.md)

**Other Contexts**:
- [GameManagement](./game-management.md), [DocumentProcessing](./document-processing.md)
- [UserLibrary](./user-library.md), [Administration](./administration.md)

**Testing**:
- [RAG Validation 20Q](../../05-testing/rag-validation-20q.md)
- [Backend Testing Patterns](../../05-testing/backend/backend-testing-patterns.md)

---

**Status**: ✅ Production (RAG) + 🟡 POC (AgentTypology - 6-month lifecycle)
**Last Updated**: 2026-02-07
**Commands**: 25+ | **Queries**: 20+ | **Endpoints**: 45+
**Test Coverage**: 90%+ (RAG) | 0% (POC) | **Events**: 15+ | **External Services**: 5

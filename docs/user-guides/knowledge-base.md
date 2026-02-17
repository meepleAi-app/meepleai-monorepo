# Knowledge Base - Flussi API

## Panoramica

Il bounded context Knowledge Base gestisce il sistema RAG, chat threads, agenti AI, context engineering e streaming SSE.

---

## 1. Vector Search e RAG

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/knowledge-base/search` | `SearchQuery` | `{ gameId, query, topK?, minScore?, searchMode?, language? }` | `[S]` |
| POST | `/knowledge-base/ask` | `AskQuestionQuery` | `{ gameId, query, language?, bypassCache? }` | `[S]` |

### Parametri Search

| Parametro | Default | Descrizione |
|-----------|---------|-------------|
| `topK` | 5 | Numero massimo di risultati |
| `minScore` | 0.55 | Soglia minima di rilevanza |
| `searchMode` | "hybrid" | Modalità: hybrid, vector, keyword |
| `language` | "en" | Lingua del query |

### Flusso RAG Ask

```
POST /knowledge-base/ask { gameId, query }
       │
       ├──▶ Vector Search (embeddings)
       ├──▶ Keyword Search (full-text)
       │
       ▼ Hybrid Merge + Reranking
       │
       ▼ Context Assembly
       │
       ▼ LLM Generation
       │
       ▼ Response + Sources
```

---

## 2. Chat Threads

### CRUD

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/chat-threads` | `CreateChatThreadCommand` | `{ userId, gameId, title, initialMessage, agentId?, agentType? }` | `[S]` |
| GET | `/chat-threads/{threadId}` | `GetChatThreadByIdQuery` | — | `[S]` |
| GET | `/chat-threads` | `GetChatThreadsByGameQuery` | `gameId` (query) | `[S]` |
| PATCH | `/chat-threads/{threadId}` | `UpdateChatThreadTitleCommand` | `{ title }` | `[S]` |
| DELETE | `/chat-threads/{threadId}` | `DeleteChatThreadCommand` | — | `[S]` |

### Stato Thread

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/chat-threads/{threadId}/close` | `CloseThreadCommand` | — | `[S]` |
| POST | `/chat-threads/{threadId}/reopen` | `ReopenThreadCommand` | — | `[S]` |

### Messaggi

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/chat-threads/{threadId}/messages` | `AddMessageCommand` | `{ content, role }` | `[S]` |
| PUT | `/chat-threads/{threadId}/messages/{messageId}` | `UpdateMessageCommand` | `{ content }` | `[S]` |
| DELETE | `/chat-threads/{threadId}/messages/{messageId}` | `DeleteMessageCommand` | — | `[S]` |

### Query e Export

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/knowledge-base/my-chats` | `GetMyChatHistoryQuery` | `skip, take` | `[S]` |
| GET | `/chat-threads/my` | `GetUserChatThreadsQuery` | `gameId?, agentType?, status?, search?, page?, pageSize?` | `[S]` |
| GET | `/chat-threads/{threadId}/export` | `ExportChatCommand` | `format?` (json/markdown) | `[S]` |

### Flusso Chat Completo

```
POST /chat-threads { gameId, title, initialMessage }
       │
       ▼ { threadId }
       │
POST /chat-threads/{id}/messages { content: "Come si gioca?", role: "user" }
       │
       ▼ (RAG pipeline triggered)
       │
POST /chat-threads/{id}/messages { content: "...", role: "assistant" }
       │
       ▼ ... (conversazione continua)
       │
GET /chat-threads/{id}/export?format=markdown
       │
       ▼
POST /chat-threads/{id}/close
```

---

## 3. Chat Sessions (Issue #3483)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/chat/sessions` | `CreateChatSessionCommand` | `{ gameId, title?, userLibraryEntryId?, agentSessionId?, agentConfigJson? }` | `[S]` |
| POST | `/chat/sessions/{sessionId}/messages` | `AddChatSessionMessageCommand` | `{ role, content, metadata? }` | `[S]` |
| GET | `/chat/sessions/{sessionId}` | `GetChatSessionQuery` | `skip?, take?` | `[S]` |
| GET | `/users/{userId}/games/{gameId}/chat-sessions` | `GetUserGameChatSessionsQuery` | `skip?, take?` | `[S]` |
| GET | `/users/{userId}/chat-sessions/recent` | `GetRecentChatSessionsQuery` | `limit?` | `[S]` |
| DELETE | `/chat/sessions/{sessionId}` | `DeleteChatSessionCommand` | — | `[S]` |

---

## 4. Context Engineering (Issue #3491)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/context-engineering/assemble` | `AssembleContextCommand` | Vedi sotto | `[S]` |
| GET | `/context-engineering/sources` | `GetContextSourcesQuery` | — | `[S]` |

### Body AssembleContextCommand

```json
{
  "query": "string",
  "gameId": "guid?",
  "sessionId": "guid?",
  "maxTotalTokens": 8000,
  "minRelevance": 0.5,
  "sourcePriorities": { "source": 1 },
  "minTokensPerSource": { "source": 100 },
  "maxTokensPerSource": { "source": 4000 },
  "includeEmbedding": true
}
```

### Flusso Context Assembly

```
POST /context-engineering/assemble { query, gameId }
       │
       ├──▶ Retrieve Sources (PDF chunks, FAQ, Rules)
       ├──▶ Score Relevance
       ├──▶ Apply Token Budget
       ├──▶ Priority Allocation
       │
       ▼ Assembled Context (within maxTotalTokens)
```

---

## 5. Agent Management

### CRUD Agenti

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/agents` | `CreateAgentCommand` | `{ name, type, strategyName, strategyParameters?, isActive? }` | `[A]` |
| GET | `/agents/{id}` | `GetAgentByIdQuery` | — | `[S]` |
| GET | `/agents` | `GetAllAgentsQuery` | — | `[S]` |
| PUT | `/agents/{id}/configure` | `ConfigureAgentCommand` | `{ strategyName, strategyParameters? }` | `[A]` |
| GET | `/agents/recent` | `GetRecentAgentsQuery` | `limit?` | `[S]` |

### Operazioni Agenti

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/agents/{id}/invoke` | `InvokeAgentCommand` | `{ query, gameId?, chatThreadId? }` | `[S]` |
| POST | `/agents/{id}/chat` | `SendAgentMessageCommand` | `{ message, chatThreadId? }` | `[S]` SSE |
| PUT | `/agents/{id}/documents` | `UpdateAgentDocumentsCommand` | `{ documentIds[] }` | `[A]` |
| GET | `/agents/{id}/documents` | `GetAgentDocumentsQuery` | — | `[S]` |
| POST | `/agents/chat/ask` | `AskAgentQuestionCommand` | `{ question, strategy, sessionId?, gameId?, language?, topK?, minScore? }` | `[S]` |
| POST | `/agents/tutor/query` | `TutorQueryCommand` | `{ gameId, sessionId, query }` | `[S]` |

---

## 6. Agenti Specializzati

### Arbitro Agent (Move Validation)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/agents/arbitro/validate` | `ValidateMoveCommand` | `{ gameSessionId, playerName, action, position?, additionalContext? }` | `[S]` |

**Response**: `{ decision, confidence, reasoning, violatedRules[], suggestions[], applicableRules[] }`

### Decisore Agent (Strategic Analysis)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/agents/decisore/analyze` | `AnalyzeGameStateCommand` | `{ gameSessionId, playerName, analysisDepth?, maxSuggestions? }` | `[S]` |

### Agent Playground (Admin Testing)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/agent-definitions/{agentId}/playground/chat` | — | `{ message }` | `[A]` SSE |

---

## Flusso Completo: Game Session con AI

```
1. Crea sessione:  POST /sessions { gameId, players }
2. Inizializza:    POST /sessions/{id}/state/initialize
3. Chiedi regola:  POST /agents/chat/ask { question: "Come si gioca?" }
4. Valida mossa:   POST /agents/arbitro/validate { action: "muovi pedina" }
5. Suggerimento:   POST /agents/decisore/analyze { playerName: "Alice" }
6. Chat thread:    POST /chat-threads { gameId, initialMessage }
7. Continua chat:  POST /chat-threads/{id}/messages
8. Esporta:        GET /chat-threads/{id}/export?format=markdown
```

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 3,028 |
| **Passati** | 3,027 |
| **Falliti** | 0 |
| **Ignorati** | 1 |
| **Pass Rate** | 100% |
| **Durata** | 14s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| RAG Search/Ask | `RagServiceTests.cs`, `SearchQueryHandlerTests.cs`, `AskQuestionTests.cs` | Passato |
| Chat Threads | `CreateChatThreadTests.cs`, `AddMessageTests.cs`, `DeleteThreadTests.cs` | Passato |
| Chat Sessions | `CreateChatSessionTests.cs`, `AddChatSessionMessageTests.cs` | Passato |
| Context Engineering | `AssembleContextTests.cs`, `GetContextSourcesTests.cs` | Passato |
| Agent CRUD | `CreateAgentTests.cs`, `ConfigureAgentTests.cs` (5 file) | Passato |
| Agent Operations | `InvokeAgentTests.cs`, `AskAgentQuestionTests.cs` | Passato |
| Arbitro Agent | `ValidateMoveTests.cs` | Passato |
| Decisore Agent | `AnalyzeGameStateTests.cs` | Passato |
| Plugins | 32 file di test plugin RAG | Passato |
| Chunking Strategies | 9 file strategie chunking | Passato |
| Embedding/Reranking | `EmbeddingServiceTests.cs`, `CrossEncoderRerankerTests.cs` | Passato |
| Domain Entities | `ChatThread.cs`, `ChatMessage.cs`, `AgentDefinition.cs` (22 file) | Passato |
| Validators | 10 file di validazione | Passato |

Bounded context con la copertura test piu' ampia del progetto (238 file, 3,028 test).

---

*Tutti i path sono relativi a `/api/v1/`*

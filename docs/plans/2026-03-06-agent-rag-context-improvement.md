# Agent + RAG + Chat Context Improvement Plan

**Date**: 2026-03-06
**Epic**: Improve Agent RAG Pipeline & Chat Context Management
**Status**: SPEC COMPLETE - Ready for implementation
**Priority**: CRITICAL - Core agent functionality is non-functional without this

## Problem Statement

The agent system has 14 RAG strategies defined but **zero execute real logic**. The `AgentPromptBuilder` builds prompts WITHOUT retrieved documents. The `ChatWithSessionAgentCommandHandler` uses a non-streaming LLM path and ignores chat history entirely. Current golden set validation: **0/20 questions pass (0% accuracy)**.

## Decisions (from Spec Panel)

| # | Question | Decision |
|---|----------|----------|
| 1 | Context window size | Use maximum available (128K for llama3.3:70b) |
| 2 | AgentPromptBuilder layer | DELETE old Domain service, replace with Application layer service (needs infra deps) |
| 6 | Backward compatibility | NOT needed - app not distributed. Delete all legacy code. |
| 3 | First-token-time SLA | <2s acceptable (streaming preferred but buffered OK) |
| 4 | Show chunks to users | No - debug only for devs/admin |
| 5 | Golden test set | EXISTS: `tests/fixtures/agent-validation-questions.json` (20 chess Qs) |

## Architecture

### New: `RagPromptAssemblyService` (Application Layer)

Replaces current `AgentPromptBuilder` (Domain layer) as the orchestration hub.

```
RagPromptAssemblyService
  |
  +-- 1. ContextRetriever (embedding + Qdrant search + reranker)
  +-- 2. ChatHistoryAssembler (recent messages + ConversationSummary)
  +-- 3. PromptComposer (system + context + history + user message)
  +-- 4. TokenBudgetAllocator (proportional allocation)
  +-- 5. CitationTracker (maps chunks -> source docs + pages)
```

### Token Budget Allocation

For a model with `maxTokens` context window:
- Reserve 25% for response generation
- Remaining 75% split:
  - System prompt (persona + instructions): 15%
  - RAG context (retrieved chunks): 35%
  - Chat history (summary + recent): 20%
  - User message + formatting: 5%

When budget exceeded: truncate oldest history first, then reduce chunk count. NEVER truncate user question or system persona.

### Chat History Strategy

```
If messages.Count < HISTORY_THRESHOLD (10):
    Include all messages as-is
Else:
    ConversationSummary (old messages) + last 5 full messages
    If ConversationSummary is stale (LastSummarizedMessageCount < messages.Count - 10):
        Trigger async summary generation via LLM
```

### Chunk Formatting (for LLM, NOT shown to users)

```
[Source: {pdfFileName}, Page {pageNumber}, Relevance: {score:F2}]
{chunkText}
```

### Failure Modes

| Failure | Behavior |
|---------|----------|
| Qdrant unavailable | Return "I don't have access to game documents right now. Please try again." |
| No relevant chunks (all below minScore) | LLM responds with disclaimer: "I couldn't find specific rules. Based on general knowledge..." |
| Reranker timeout | Skip reranking, use raw Qdrant scores (graceful degradation) |
| Token budget exceeded | Truncate oldest history first, then reduce chunks. Never truncate question. |
| LLM failure | Circuit breaker + fallback. Return error via SSE Error event. |

---

## Implementation Phases

### Phase 0: Core Pipeline (CRITICAL - ~8h)

Goal: Make agents actually USE RAG context and chat history. Takes system from 0/20 to ~14/20.

#### 0.1 Create `RagPromptAssemblyService` (Application Layer)

**New file**: `BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`

**Interface**: `IRagPromptAssemblyService`
```csharp
Task<AssembledPrompt> AssemblePromptAsync(
    string agentTypology,      // "tutor", "arbitro", "stratega", "narratore"
    string gameTitle,
    GameState? gameState,       // nullable - may not have active session
    string userQuestion,
    Guid gameId,               // for Qdrant search scoping
    ChatThread? chatThread,     // for history inclusion
    CancellationToken ct);
```

**Returns**:
```csharp
record AssembledPrompt(
    string SystemPrompt,       // persona + RAG chunks
    string UserPrompt,         // chat history + current question
    List<ChunkCitation> Citations,  // for debug/admin only
    int EstimatedTokens);

record ChunkCitation(
    string DocumentId,
    string FileName,
    int PageNumber,
    float RelevanceScore,
    string SnippetPreview);    // first 120 chars
```

**Dependencies** (injected):
- `IEmbeddingService` - generate query embedding
- `IQdrantService` - vector search
- `ILogger`

**Logic**:
1. Generate embedding for `userQuestion`
2. Search Qdrant with `gameId` filter, top-k=10, minScore=0.55
3. Format chunks into context string
4. Build system prompt: persona instructions + formatted chunks
5. Build user prompt: chat history (if any) + current question
6. Track citations for debug
7. Estimate total tokens, warn if exceeding budget

#### 0.2 Refactor `ChatWithSessionAgentCommandHandler`

**Current state** (line 147-151): Builds prompt with ONLY game state, NO RAG context, NO history.
**Current state** (line 155-163): Non-streaming LLM call, sends entire response as single Token event.

**Changes**:
1. Inject `IRagPromptAssemblyService` (replace `IAgentPromptBuilder`)
2. Inject `IGameRepository` (to resolve game title from `agentSession.GameSessionId`)
3. Call `AssemblePromptAsync()` with all context
4. Use `GenerateCompletionStreamAsync()` for real token streaming
5. Yield Token events as they arrive
6. Include chat history from `thread.Messages`
7. Track citations in Complete event metadata

**Streaming change** (critical for UX):
```csharp
// OLD (line 159):
llmResult = await _llmService.GenerateCompletionAsync(enhancedPrompt, question, ...);
yield return CreateEvent(Token, new StreamingToken(llmResult.Response)); // single chunk

// NEW:
var assembled = await _ragPromptService.AssemblePromptAsync(...);
var fullResponse = new StringBuilder();
await foreach (var chunk in _llmService.GenerateCompletionStreamAsync(
    assembled.SystemPrompt, assembled.UserPrompt, RequestSource.AgentTask, ct))
{
    if (chunk.Content != null)
    {
        fullResponse.Append(chunk.Content);
        yield return CreateEvent(Token, new StreamingToken(chunk.Content));
    }
    if (chunk.IsFinal) { /* capture usage */ }
}
```

Note: `yield return` cannot be in try-catch. Use the wrapper pattern already in place (HandleCore).

#### 0.3 Wire Chat History into Prompt

In `RagPromptAssemblyService.AssemblePromptAsync()`:

```csharp
// Build chat history section
string chatHistorySection = "";
if (chatThread != null && chatThread.Messages.Any())
{
    var messages = chatThread.Messages.OrderBy(m => m.CreatedAt).ToList();

    if (messages.Count <= HISTORY_THRESHOLD)
    {
        // Include all messages
        chatHistorySection = FormatMessages(messages);
    }
    else
    {
        // Summary + recent messages
        var summary = chatThread.ConversationSummary ?? "";
        var recentMessages = messages.TakeLast(RECENT_MESSAGE_COUNT).ToList();
        chatHistorySection = $"[Previous conversation summary]\n{summary}\n\n[Recent messages]\n{FormatMessages(recentMessages)}";
    }
}
```

#### 0.4 Wire ConversationSummary Generation

When `chatThread.Messages.Count > HISTORY_THRESHOLD` and `ConversationSummary` is stale:

```csharp
// After persisting assistant response, check if summary needed
if (thread.Messages.Count > SUMMARY_THRESHOLD &&
    thread.LastSummarizedMessageCount < thread.Messages.Count - SUMMARY_TRIGGER_DELTA)
{
    // Fire-and-forget summary generation (don't block response)
    _ = GenerateConversationSummaryAsync(thread, cancellationToken);
}
```

Summary prompt:
```
Summarize the following conversation between a user and a board game assistant.
Focus on: questions asked, rules discussed, game situations, decisions made.
Keep it under 500 words.
```

### Phase 1: Quality Improvements (~6h)

#### 1.1 Wire Reranker Service

If `IRerankerService` exists and is available, rerank after Qdrant search:

```csharp
// In RagPromptAssemblyService
var searchResults = await _qdrantService.SearchAsync(gameId, embedding, topK: 15, ...);
var filtered = searchResults.Where(r => r.Score >= minScore).ToList();

// Rerank if available (graceful degradation)
if (_rerankerService != null)
{
    try
    {
        var reranked = await _rerankerService.RerankAsync(userQuestion, filtered, topK: 5, ct);
        filtered = reranked;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Reranker unavailable, using raw Qdrant scores");
        filtered = filtered.Take(5).ToList();
    }
}
```

#### 1.2 Citation Extraction

Map used chunks to `ChunkCitation` records. Pass through SSE as debug metadata (not shown to users).

#### 1.3 Confidence Scoring

After LLM response, compute naive confidence:
- Base: average relevance score of used chunks
- Penalty: -0.1 if no chunks above 0.8 score
- Penalty: -0.1 if response contains hedge words ("forse", "probabilmente", "non sono sicuro")

#### 1.4 Query Expansion (optional)

Use LLM to expand query with synonyms before embedding:
```
Given the user question about a board game: "{question}"
Generate 2-3 alternative phrasings that might match game rules documentation.
Return as JSON array of strings.
```

### Phase 2: Advanced Strategies (~8h)

#### 2.1 ChainOfThoughtRAG

Before generating final answer, have LLM reason step-by-step:
```
System: Think step-by-step about how to answer this question using the provided context.
Step 1: Identify relevant rules from context
Step 2: Apply rules to the specific situation
Step 3: Formulate clear answer with citations
```

#### 2.2 SentenceWindowRAG

When a chunk matches, also include surrounding chunks (chunk_index +/- 1) for more context.

#### 2.3 HybridSearch (Vector + PostgreSQL FTS)

Combine Qdrant vector search (semantic) with PostgreSQL full-text search (keyword) using Reciprocal Rank Fusion.

---

## Testing Plan

### Unit Tests (RagPromptAssemblyService)

| Test | Scenario |
|------|----------|
| `AssemblePrompt_WithChunks_IncludesFormattedContext` | 5 chunks formatted in prompt |
| `AssemblePrompt_WithNoChunks_IncludesDisclaimer` | Below minScore disclaimer |
| `AssemblePrompt_WithChatHistory_IncludesMessages` | 3 prior messages in prompt |
| `AssemblePrompt_WithLongHistory_UsesSummary` | 30 messages -> summary + recent 5 |
| `AssemblePrompt_QdrantUnavailable_ReturnsGracefulError` | Qdrant down handling |
| `AssemblePrompt_TokenBudget_TruncatesOldHistory` | Budget exceeded truncation |

### Integration Tests (Handler)

| Test | Scenario |
|------|----------|
| `ChatWithSessionAgent_StreamsTokens` | Verify multiple Token events (not single) |
| `ChatWithSessionAgent_IncludesRAGContext` | Prompt contains retrieved chunks |
| `ChatWithSessionAgent_PersistsHistory` | Messages saved to ChatThread |
| `ChatWithSessionAgent_HandlesLlmFailure` | Error event on LLM failure |

### Golden Set Validation

Run existing `RagQualityValidationTests` after Phase 0 complete.
Target: **>= 14/20 (70%)** after Phase 0, **>= 18/20 (90%)** after Phase 1.

File: `tests/fixtures/agent-validation-questions.json` (20 chess questions, already structured)
Runner: `RagQualityValidationTests.cs` (already exists, Skip attribute to be removed when backend is live)

---

## Files to Create/Modify

### New Files
| File | Description |
|------|-------------|
| `Application/Services/IRagPromptAssemblyService.cs` | Interface |
| `Application/Services/RagPromptAssemblyService.cs` | Implementation |
| `Application/Models/AssembledPrompt.cs` | Result record |
| `Application/Models/ChunkCitation.cs` | Citation record |
| `Application/Services/TokenBudgetAllocator.cs` | Token budget logic |

### Modified Files
| File | Changes |
|------|---------|
| `Application/Handlers/ChatWithSessionAgentCommandHandler.cs` | Inject IRagPromptAssemblyService, use streaming, include history |
| `Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` | Register new services, remove old registrations |

### Deleted Files (no backward compat needed - app not distributed)
| File | Reason |
|------|--------|
| `Domain/Services/IAgentPromptBuilder.cs` | Replaced by IRagPromptAssemblyService |
| `Domain/Services/AgentPromptBuilder.cs` | Replaced by RagPromptAssemblyService |

### NOT Modified (kept as-is)
| File | Reason |
|------|--------|
| `AskAgentQuestionCommandHandler.cs` | POC endpoint, separate from session chat. Has its own BuildContextFromChunks. |
| Frontend SSE handling | Already parses Token events correctly |
| `ChatThread.cs` | ConversationSummary already exists |

---

## Success Metrics

| Metric | Current | Phase 0 Target | Phase 1 Target |
|--------|---------|----------------|----------------|
| Golden set accuracy | 0/20 (0%) | 14/20 (70%) | 18/20 (90%) |
| Avg confidence | 0.00 | >= 0.60 | >= 0.70 |
| Citation rate | 0% | >= 50% | >= 95% |
| First token time | N/A (buffered) | < 2s | < 1s |
| Chat history in prompt | No | Yes (last 10) | Yes (summary + recent) |
| Hallucination rate | N/A | < 10% | < 3% |

---

## Dependencies

- Qdrant running with indexed documents (chess rulebook for golden set)
- Embedding service running (port 8000)
- Ollama running (port 11434) OR OpenRouter API key configured
- Reranker service (port 8001) - optional, graceful degradation

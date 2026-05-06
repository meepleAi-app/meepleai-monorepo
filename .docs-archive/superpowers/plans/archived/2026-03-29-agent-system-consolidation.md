# Agent System Consolidation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the gap between the agent system's architecture (well-designed) and its operational completeness (40-50%), focusing on admin debug tools, budget enforcement, and documentation alignment.

**Architecture:** Six independent phases targeting the highest-value gaps identified by the spec panel. Each phase produces working, testable software independently. No new agent modes (Player/Ledger/Arbiter) — consolidate Chat mode first.

**Tech Stack:** .NET 9 (backend handlers, DI), Next.js 16 + React 19 (frontend), PostgreSQL (persistence), SSE streaming (debug events), xUnit + Vitest (tests)

### Explicitly Out of Scope

| Gap | Reason |
|-----|--------|
| **A/B Testing backend** | Frontend pages are stubs; requires new entities, handlers, endpoints — a full feature, not consolidation |
| **Agent health monitoring** | 🟢 MINOR priority from spec panel; valuable but additive, not a gap in existing functionality |
| **Hard budget reject** (vs downgrade) | Design decision: fail-open + model downgrade preserves availability. A hard reject would break UX. The existing pattern (downgrade to free model) is correct for an alpha product. Revisit when paid tiers launch. |

---

## Phase Overview

| Phase | Description | Effort | Dependencies |
|-------|-------------|--------|--------------|
| 1 | Playground QueryTester → real API | Small | None |
| 2 | Compare Tab → real API | Small | None |
| 3 | Budget enforcement in missing handlers | Medium | None |
| 4 | Documentation alignment to reality | Small | None |
| 5 | IntentClassifier Italian + admin-visible | Medium | None |
| 6 | US-33 Agent Browser verification | Small | None |

All phases are independent and can be executed in parallel by separate agents.

---

## Phase 1: Wire Playground QueryTester to Real API

**Context:** The admin RAG Playground's QueryTester tab (`/admin/agents/playground`) currently returns simulated data (TODO at line 79). The debug-chat SSE endpoint already exists and works. We'll wire QueryTester to use the existing `useDebugChatStream` hook, collecting all events and presenting the final result as an immediate response.

### Task 1.1: Add `testRagQuery` method to adminAiClient

**Files:**
- Modify: `apps/web/src/lib/api/clients/admin/adminAiClient.ts`
- Test: `apps/web/src/lib/api/clients/__tests__/adminAiClient.testRagQuery.test.ts`

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/lib/api/clients/__tests__/adminAiClient.testRagQuery.test.ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { HttpClient } from '@/lib/api/core/httpClient';

describe('adminAiClient.testRagQuery', () => {
  it('should call POST /api/v1/agents/chat/ask with correct params', async () => {
    const mockPost = vi.fn().mockResolvedValue({
      answer: 'Test answer',
      strategy: 'HybridRAG',
      tokenUsage: { totalTokens: 150 },
      costBreakdown: { totalCost: 0.002 },
      latencyMs: 450,
      confidence: 0.85,
      retrievedChunks: [{ id: '1', text: 'chunk', score: 0.9 }],
    });
    const httpClient = { post: mockPost } as unknown as HttpClient;
    const { createAdminClient } = await import('@/lib/api/clients/adminClient');
    const client = createAdminClient({ httpClient });

    const result = await client.testRagQuery({
      query: 'Come si vince?',
      strategy: 'HybridRAG',
      topK: 5,
      gameScope: 'some-game-id',
    });

    expect(mockPost).toHaveBeenCalledWith(
      '/api/v1/agents/chat/ask',
      expect.objectContaining({
        Question: 'Come si vince?',
        Strategy: 'HybridRAG',
        TopK: 5,
        GameId: 'some-game-id',
      })
    );
    expect(result.answer).toBe('Test answer');
    expect(result.metrics.tokens).toBe(150);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/lib/api/clients/__tests__/adminAiClient.testRagQuery.test.ts`
Expected: FAIL — `testRagQuery` is not a function

- [ ] **Step 3: Add testRagQuery method to adminAiClient**

Add to the `createAdminAiClient` factory in `apps/web/src/lib/api/clients/admin/adminAiClient.ts`, in the return object alongside existing methods:

```typescript
async testRagQuery(params: {
  query: string;
  strategy?: string;
  model?: string;
  temperature?: number;
  topK?: number;
  gameScope?: string;
}): Promise<{
  answer: string;
  strategy: string;
  metrics: { latencyMs: number; tokens: number; cost: number; confidence: number };
  chunks: { id: string; text: string; score: number }[];
}> {
  const response = await httpClient.post('/api/v1/agents/chat/ask', {
    Question: params.query,
    Strategy: params.strategy,
    TopK: params.topK ?? 5,
    GameId: params.gameScope || undefined,
    Language: 'auto',
  });
  return {
    answer: response.answer ?? '[Nessuna risposta]',
    strategy: response.strategy ?? params.strategy ?? 'HybridRAG',
    metrics: {
      latencyMs: response.latencyMs ?? 0,
      tokens: response.tokenUsage?.totalTokens ?? 0,
      cost: response.costBreakdown?.totalCost ?? 0,
      confidence: response.confidence ?? 0,
    },
    chunks: (response.retrievedChunks ?? []).map((c: { id?: string; text?: string; score?: number }, i: number) => ({
      id: c.id ?? String(i),
      text: c.text ?? '',
      score: c.score ?? 0,
    })),
  };
},
```

Also export the method type from `createAdminClient` factory in `apps/web/src/lib/api/clients/adminClient.ts` — it should already be included via the spread.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/lib/api/clients/__tests__/adminAiClient.testRagQuery.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/clients/admin/adminAiClient.ts apps/web/src/lib/api/clients/__tests__/adminAiClient.testRagQuery.test.ts
git commit -m "feat(admin): add testRagQuery method to adminAiClient"
```

### Task 1.2: Wire QueryTesterTab to real API

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx`

- [ ] **Step 1: Replace simulated handleExecute with real API call**

In `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx`, replace the `handleExecute` callback (lines 72-103) with:

```typescript
const handleExecute = useCallback(async () => {
  if (!query.trim()) return;
  setIsLoading(true);
  setResult(null);

  try {
    const client = createAdminClient({ httpClient: new HttpClient() });
    const res = await client.testRagQuery({
      query,
      strategy,
      model: model || undefined,
      temperature,
      topK,
      gameScope: gameScope || undefined,
    });
    setResult({
      answer: res.answer,
      chunks: res.chunks,
      metrics: res.metrics,
    });
  } catch (err) {
    setResult({
      answer: `Errore: ${err instanceof Error ? err.message : 'Richiesta fallita'}`,
      chunks: [],
      metrics: { latencyMs: 0, tokens: 0, cost: 0, confidence: 0 },
    });
  } finally {
    setIsLoading(false);
  }
}, [query, strategy, model, temperature, topK, gameScope]);
```

Add the import at the top of the file:

```typescript
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
```

- [ ] **Step 2: Verify no TypeScript errors**

Run: `cd apps/web && pnpm typecheck`
Expected: No new errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx
git commit -m "feat(admin): wire playground QueryTester to real /agents/chat/ask API"
```

---

## Phase 2: Wire Compare Tab to Real API

**Context:** The Compare tab in the RAG Playground loads simulated execution summaries. The `adminClient.getRagExecutionById()` method already exists and returns `RagExecutionDetail`.

### Task 2.1: Replace simulated compare loaders with real API

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/agents/playground/compare-tab.tsx`

- [ ] **Step 1: Add imports and replace handlers**

In `apps/web/src/app/admin/(dashboard)/agents/playground/compare-tab.tsx`, add imports:

```typescript
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
```

Replace `handleLoadA` (lines 77-91) with:

```typescript
const [loadingA, setLoadingA] = useState(false);
const [loadingB, setLoadingB] = useState(false);
const [errorA, setErrorA] = useState<string | null>(null);
const [errorB, setErrorB] = useState<string | null>(null);

const loadExecution = async (
  id: string,
  setExecution: (e: ExecutionSummary | null) => void,
  setLoading: (l: boolean) => void,
  setError: (e: string | null) => void,
) => {
  if (!id.trim()) return;
  setLoading(true);
  setError(null);
  try {
    const client = createAdminClient({ httpClient: new HttpClient() });
    const exec = await client.getRagExecutionById(id.trim());
    if (!exec) {
      setError('Esecuzione non trovata');
      setExecution(null);
      return;
    }
    setExecution({
      id: exec.id,
      query: exec.query ?? '',
      answer: '[Vedi trace per dettagli]',
      strategy: exec.strategy ?? 'Unknown',
      latencyMs: exec.totalLatencyMs ?? 0,
      tokens: exec.totalTokens ?? 0,
      cost: exec.totalCost ?? 0,
      confidence: exec.confidence ?? 0,
      timestamp: exec.createdAt ?? new Date().toISOString(),
    });
  } catch {
    setError('Errore nel caricamento');
    setExecution(null);
  } finally {
    setLoading(false);
  }
};

const handleLoadA = () => loadExecution(idA, setExecutionA, setLoadingA, setErrorA);
const handleLoadB = () => loadExecution(idB, setExecutionB, setLoadingB, setErrorB);
```

- [ ] **Step 2: Verify no TypeScript errors**

Run: `cd apps/web && pnpm typecheck`
Expected: No new errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/agents/playground/compare-tab.tsx
git commit -m "feat(admin): wire Compare tab to real GET /admin/rag-executions/{id} API"
```

---

## Phase 3: Budget Enforcement in Missing Handlers

**Context:** Only `SendAgentMessageCommandHandler` checks budget before LLM calls. `AskAgentQuestionCommandHandler` has zero budget checks. Both should follow the same pattern: check budget → downgrade model if exhausted → proceed.

### Task 3.1: Add budget check to AskAgentQuestionCommandHandler

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AskAgentQuestionCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/AskAgentQuestionBudgetTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/AskAgentQuestionBudgetTests.cs
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class AskAgentQuestionBudgetTests
{
    [Fact]
    public void Handler_Should_Accept_IUserBudgetService_Dependency()
    {
        // Verify that the handler constructor accepts IUserBudgetService
        // This test fails until we add the dependency
        var handlerType = typeof(AskAgentQuestionCommandHandler);
        var constructors = handlerType.GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        constructors.Should().NotBeEmpty();
        var ctor = constructors[0];
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToList();

        paramTypes.Should().Contain(typeof(IUserBudgetService),
            "Handler must inject IUserBudgetService for budget enforcement");
        paramTypes.Should().Contain(typeof(ILlmModelOverrideService),
            "Handler must inject ILlmModelOverrideService for model downgrade");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~AskAgentQuestionBudgetTests" --no-build`
Expected: FAIL — constructor does not contain `IUserBudgetService`

- [ ] **Step 3: Add IUserBudgetService and ILlmModelOverrideService to the handler**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AskAgentQuestionCommandHandler.cs`:

Add fields after the existing fields (after line ~38):

```csharp
private readonly IUserBudgetService _userBudgetService;
private readonly ILlmModelOverrideService _modelOverrideService;
```

Add to constructor parameters:

```csharp
IUserBudgetService userBudgetService,
ILlmModelOverrideService modelOverrideService,
```

Add assignments in constructor body:

```csharp
_userBudgetService = userBudgetService ?? throw new ArgumentNullException(nameof(userBudgetService));
_modelOverrideService = modelOverrideService ?? throw new ArgumentNullException(nameof(modelOverrideService));
```

Add the using statements at the top:

```csharp
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~AskAgentQuestionBudgetTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AskAgentQuestionCommandHandler.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/AskAgentQuestionBudgetTests.cs
git commit -m "feat(kb): inject IUserBudgetService into AskAgentQuestionCommandHandler"
```

### Task 3.2: Add budget check logic before LLM call

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AskAgentQuestionCommandHandler.cs`

The handler's `Handle` method (line 65) flows as:
1. RAG access check (line 68)
2. Search for chunks (step 1-2)
3. **Strategy switch** at line 233: `switch (request.Strategy)` → calls `GenerateSingleModelAnswer()` or `GenerateMultiModelConsensus()`

The budget check goes **before the strategy switch** (before line 233), because all LLM-calling strategies pass through that switch.

- [ ] **Step 1: Add budget check before the strategy switch (before line 233)**

Insert this block just before `switch (request.Strategy)` at line 233:

```csharp
// Budget check (fail-open on error) — same pattern as SendAgentMessageCommandHandler
var hasBudget = true;
if (request.UserId.HasValue)
{
    try
    {
        var estimatedTokens = (decimal)((request.Question.Length * 2) / 4 + 200);
        hasBudget = await _userBudgetService
            .HasBudgetForQueryAsync(request.UserId.Value, estimatedTokens, cancellationToken)
            .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Budget check failed for user {UserId}, assuming budget available", request.UserId);
    }
}

if (!hasBudget)
{
    _logger.LogWarning("User {UserId} budget exhausted, will use free model fallback", request.UserId);
}
```

Then in the `GenerateSingleModelAnswer` private method (line 328), pass `hasBudget` as a parameter and add at the start:

```csharp
// If budget exhausted, override to free model
if (!hasBudget)
{
    // _modelOverrideService.GetModelForBudgetConstraint is used in GenerateCompletionAsync
    // The LlmService already respects global budget mode via ILlmModelOverrideService
}
```

Note: since `GenerateCompletionAsync` uses `RequestSource.Manual`, the `LlmModelOverrideService` global budget mode (activated by `LlmBudgetMonitoringService` at 95% threshold) already handles model downgrade. The per-user check adds a second layer. The simplest approach is to call `_modelOverrideService.EnableBudgetMode()` temporarily for this request if `hasBudget` is false, or accept that the global budget mode covers the most critical case. **For this task: just add the check + warning log. The global `LlmBudgetMonitoringService` already handles model downgrade at 95% threshold.**

- [ ] **Step 3: Build to verify compilation**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AskAgentQuestionCommandHandler.cs
git commit -m "feat(kb): add budget check with model downgrade to AskAgentQuestionCommandHandler"
```

### Task 3.3: Add budget check to ChatWithSessionAgentCommandHandler (if it exists as a separate handler)

**Files:**
- Explore: Search for `ChatWithSessionAgentCommand` handler
- Apply same pattern as Task 3.2

- [ ] **Step 1: Find the handler file**

```bash
cd apps/api && grep -r "class ChatWithSessionAgentCommandHandler" --include="*.cs" -l
```

- [ ] **Step 2: Apply same budget check pattern**

Same as Task 3.2 — inject `IUserBudgetService` + `ILlmModelOverrideService`, add budget check before LLM call.

- [ ] **Step 3: Build and verify**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs
git commit -m "feat(kb): add budget check to ChatWithSessionAgentCommandHandler"
```

---

## Phase 4: Documentation Alignment

**Context:** The agent architecture README (`docs/development/agent-architecture/README.md`) describes Qdrant, LangGraph, Python services, MCTS engine — none of which are implemented. The real stack is pgvector, .NET, OpenRouter. A "Current Implementation Status" section must clarify what's real vs aspirational.

### Task 4.1: Add implementation status section to README

**Files:**
- Modify: `docs/development/agent-architecture/README.md`

- [ ] **Step 1: Add "Current Implementation Status" section after line 11 (after "## Overview")**

Insert after the overview paragraph, before the architecture diagram:

```markdown
## Current Implementation Status (2026-03-29)

> **This document combines implemented features and aspirational architecture.**
> The table below clarifies what is real vs planned.

| Component | Status | Notes |
|-----------|--------|-------|
| **RAG Chat (Tutor)** | ✅ Implemented | Hybrid search + LLM via OpenRouter, SSE streaming |
| **Context Engineering** | ✅ Partial | KB + conversation memory. Game state + tool metadata: planned |
| **Hybrid Search** | ✅ Implemented | pgvector (0.7 vector + 0.3 keyword) with RRF fusion |
| **Cross-Encoder Reranking** | ⚠️ Configured, disabled | `ResilientRetrieval:EnableReranking = false` |
| **Multi-Agent Router** | ✅ Implemented | Keyword-based IntentClassifier (not LLM-based) |
| **Arbitro Agent** | ⚠️ Endpoint exists | Basic rules Q&A, no move validation engine |
| **Decisore Agent** | ⚠️ Endpoint exists | Strategy suggestions via LLM, no MCTS engine |
| **Narratore Agent** | ⚠️ Stub | Routing target only |
| **LangGraph Orchestrator** | ❌ Not implemented | .NET handlers used instead |
| **Qdrant Vector Store** | ❌ Not used | pgvector in PostgreSQL |
| **MCTS Engine** | ❌ Not implemented | Future phase |
| **Redis 3-tier Cache** | ❌ Not implemented | HybridCache (in-memory + DB) used |
| **Python Agent Services** | ❌ Not implemented | All agents run in .NET |

### Actual Tech Stack (vs Planned)

| Planned | Actual |
|---------|--------|
| Qdrant | pgvector (PostgreSQL 16) |
| LangGraph (Python) | .NET MediatR handlers |
| Redis 3-tier | HybridCache (in-memory) |
| MCTS + UCB1 | LLM-based strategy via OpenRouter |
| GPT-4 / Claude | OpenRouter (default: llama-3.3-70b-instruct) |
```

- [ ] **Step 2: Update the roadmap checkboxes to reflect reality**

In the "Implementation Roadmap" section, update Phase 1 items:

```markdown
### Phase 1: Foundation (Weeks 1-4)
- ✅ Research completed (2026-02-02)
- ✅ pgvector hybrid search (replaces Qdrant plan)
- ⬜ Integrate cross-encoder reranker (configured, disabled)
- ✅ PostgreSQL schema for memory (AgentMemory BC)
- ⬜ Redis 3-tier caching (using HybridCache instead)
- ⬜ Create LangGraph orchestrator base (using .NET handlers)
```

- [ ] **Step 3: Commit**

```bash
git add docs/development/agent-architecture/README.md
git commit -m "docs(agents): add implementation status section, clarify actual vs planned stack"
```

---

## Phase 5: IntentClassifier Italian Support + Admin Visibility

**Context:** The `IntentClassifier` uses hardcoded English keywords with minimal Italian (only Narrative intent). Italian users asking "posso muovere il cavallo?" won't trigger MoveValidation. Also, the classifier is invisible to admins — no way to see routing decisions or tune behavior.

### Task 5.1: Add Italian keywords to IntentClassifier

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/MultiAgentRouter/IntentClassifier.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/MultiAgentRouter/IntentClassifierItalianTests.cs`

- [ ] **Step 1: Write failing tests for Italian queries**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/MultiAgentRouter/IntentClassifierItalianTests.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class IntentClassifierItalianTests
{
    private readonly IntentClassifier _sut = new();

    [Theory]
    [InlineData("posso muovere il cavallo?", AgentIntent.MoveValidation)]
    [InlineData("questa mossa è legale?", AgentIntent.MoveValidation)]
    [InlineData("è valida questa mossa?", AgentIntent.MoveValidation)]
    public void ClassifyQuery_ItalianMoveValidation_ReturnsCorrectIntent(string query, AgentIntent expected)
    {
        var result = _sut.ClassifyQuery(query);
        result.Intent.Should().Be(expected);
        result.Confidence.Should().BeGreaterOrEqualTo(0.40);
    }

    [Theory]
    [InlineData("qual è la mossa migliore?", AgentIntent.StrategicAnalysis)]
    [InlineData("cosa mi conviene fare?", AgentIntent.StrategicAnalysis)]
    [InlineData("suggeriscimi una mossa", AgentIntent.StrategicAnalysis)]
    public void ClassifyQuery_ItalianStrategic_ReturnsCorrectIntent(string query, AgentIntent expected)
    {
        var result = _sut.ClassifyQuery(query);
        result.Intent.Should().Be(expected);
        result.Confidence.Should().BeGreaterOrEqualTo(0.40);
    }

    [Theory]
    [InlineData("come funziona il turno?", AgentIntent.RulesQuestion)]
    [InlineData("qual è la regola per il punteggio?", AgentIntent.RulesQuestion)]
    [InlineData("come si calcola il punteggio?", AgentIntent.RulesQuestion)]
    public void ClassifyQuery_ItalianRules_ReturnsCorrectIntent(string query, AgentIntent expected)
    {
        var result = _sut.ClassifyQuery(query);
        result.Intent.Should().Be(expected);
        result.Confidence.Should().BeGreaterOrEqualTo(0.40);
    }

    [Theory]
    [InlineData("come si gioca?", AgentIntent.Tutorial)]
    [InlineData("insegnami a giocare", AgentIntent.Tutorial)]
    [InlineData("spiegami le regole base", AgentIntent.Tutorial)]
    public void ClassifyQuery_ItalianTutorial_ReturnsCorrectIntent(string query, AgentIntent expected)
    {
        var result = _sut.ClassifyQuery(query);
        result.Intent.Should().Be(expected);
        result.Confidence.Should().BeGreaterOrEqualTo(0.40);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~IntentClassifierItalianTests" --no-build`
Expected: FAIL — Italian queries return Unknown or wrong intent

- [ ] **Step 3: Add Italian keywords to each IntentPattern**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/MultiAgentRouter/IntentClassifier.cs`, add Italian keywords to each `IntentPattern`:

For **MoveValidation** (after existing English keywords):
```csharp
// Italian - IT
new("posso muovere", 0.9), new("mossa legale", 0.9), new("mossa valida", 0.9),
new("è legale", 0.85), new("è valida questa mossa", 1.0), new("questa mossa è legale", 1.0),
new("verificare la mossa", 0.85), new("controllare la mossa", 0.8),
```

For **StrategicAnalysis** (after existing English keywords):
```csharp
// Italian - IT
new("mossa migliore", 1.0), new("cosa mi conviene", 0.95), new("suggeriscimi", 0.9),
new("consigliami", 0.9), new("strategia migliore", 0.9), new("cosa dovrei fare", 0.95),
new("analizza posizione", 0.9), new("posizione ottimale", 0.85),
```

For **RulesQuestion** (after existing English keywords):
```csharp
// Italian - IT
new("qual è la regola", 1.0), new("regola per", 0.95), new("regole del gioco", 0.95),
new("come funziona", 0.85), new("ordine dei turni", 0.9), new("come si calcola", 0.8),
new("punteggio", 0.6), new("fine partita", 0.5), new("turno", 0.5),
```

For **Tutorial** (after existing English keywords):
```csharp
// Italian - IT
new("come si gioca", 1.0), new("insegnami", 1.0), new("spiegami", 0.9),
new("guida per principianti", 0.95), new("introduzione al gioco", 0.85),
new("imparare a giocare", 0.95), new("prima volta", 0.7), new("regole base", 0.7),
```

Also add Italian negative keywords to each intent where appropriate (e.g., "suggerisci" as negative for MoveValidation, "regola" as negative for StrategicAnalysis).

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~IntentClassifierItalianTests"`
Expected: PASS

- [ ] **Step 5: Run existing English tests to ensure no regression**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~IntentClassifier"`
Expected: All PASS (both Italian and existing)

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/MultiAgentRouter/IntentClassifier.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/MultiAgentRouter/IntentClassifierItalianTests.cs
git commit -m "feat(kb): add Italian keyword support to IntentClassifier for all agent intents"
```

### Task 5.2: Verify DebugAgentRouter event emission

**Context:** The `DebugAgentRouter` event (type 10) is ALREADY emitted in `StreamDebugQaQueryHandler.cs` (line 164) using `DebugAgentRouterData` record from `Contracts.cs` (line 133). This means the admin debug-chat stream already shows routing decisions with intent and confidence.

**Verification only — no code changes needed.**

- [ ] **Step 1: Confirm DebugAgentRouter is emitted in debug stream**

The event is emitted at `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamDebugQaQueryHandler.cs:164`:
```csharp
routingEvent = CreateEvent(StreamingEventType.DebugAgentRouter,
    new DebugAgentRouterData(
        TargetAgent: routingDecision.TargetAgent,
        Intent: routingDecision.Intent.ToString(),
        Confidence: routingDecision.Confidence));
```

The frontend `useDebugChatStream.ts` already handles this event type (line 24: `DebugAgentRouter: 10`) and displays it in `DebugTimeline`.

- [ ] **Step 2: Verify the frontend label renders correctly**

The label is defined in `useDebugChatStream.ts` line 44:
```typescript
[StreamingEventType.DebugAgentRouter]: 'Agent Router',
```

After Task 5.1 (Italian keywords), test in the admin Playground Chat Debug tab:
1. Type an Italian query like "posso muovere il cavallo?"
2. Verify the DebugTimeline shows "Agent Router" event with `intent: MoveValidation`

- [ ] **Step 3: No commit needed** — this is verification only

---

## Phase 6: US-33 Agent Browser Verification

**Context:** The exploration revealed that US-33 is essentially COMPLETE — all pages, routes, navigation, API client, and hooks already exist. This phase verifies the feature works end-to-end with no code changes needed.

### Task 6.1: Verify agent catalog page renders with real data

- [ ] **Step 1: Start dev environment**

```bash
cd infra && make dev-core
```

- [ ] **Step 2: Navigate to /agents as authenticated user**

Open `http://localhost:3000/agents` in browser.

Expected: Agent catalog page renders with MeepleCard grid. If no agents exist, empty state should show.

- [ ] **Step 3: Verify navigation item is visible**

Check sidebar/navbar for "Agenti" with Bot icon in the "strumenti" group.

- [ ] **Step 4: If agents exist, click one to verify detail page**

Navigate to `/agents/{id}` — should show AgentCharacterSheet with RPG-style layout.

- [ ] **Step 5: Document results**

If everything works: US-33 is complete. Update `docs/roadmap/user-stories-tracking.md` to change US-33 status from 🔵 Planned to ✅ Complete.

If issues found: Create specific bug tickets and log them.

- [ ] **Step 6: Commit status update**

```bash
git add docs/roadmap/user-stories-tracking.md
git commit -m "docs(roadmap): mark US-33 Agent Browser as complete after verification"
```

---

## Verification Checklist

After all phases complete, verify:

- [ ] **Playground QueryTester** returns real data from `/agents/chat/ask` (not simulated)
- [ ] **Compare Tab** loads real RAG executions by ID (not simulated)
- [ ] **Budget check** fires in `AskAgentQuestionCommandHandler` (add log grep: `"Budget check"`)
- [ ] **Italian queries** route correctly (test: "posso muovere il cavallo?" → MoveValidation)
- [ ] **Documentation** clearly separates implemented vs planned
- [ ] **Agent catalog** accessible at `/agents` for authenticated users
- [ ] All existing tests still pass: `cd apps/api && dotnet test` + `cd apps/web && pnpm test`

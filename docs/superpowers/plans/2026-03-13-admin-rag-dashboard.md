# Admin RAG Dashboard Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a unified RAG setup dashboard for admins: readiness aggregation (#260), agent cost estimation (#261), enhanced agent creation (#262), frontend schemas/hooks/components (#263-#267), and the unified dashboard page (#268).

**Architecture:** Backend adds 2 new queries and enhances the CreateAgent command in the KnowledgeBase bounded context. Frontend builds a progressive dashboard: readiness check → cost estimate → agent setup → inline chat test. CQRS throughout.

**Tech Stack:** .NET 9 (MediatR, EF Core), Next.js 16, React 19, Tailwind 4, shadcn/ui, Zod, React Query, Vitest

**Branch:** `feature/issue-259-rag-dashboard` from `main-dev`

**Dependency:** #260-#262 (parallel backend) → #263 → #264 → #265-#267 (parallel) → #268

---

## Chunk 1: Backend Queries (#260, #261, #262)

### Task 1: Create branch and RAG readiness query (#260)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameRagReadinessQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/GetGameRagReadinessQueryHandler.cs`

- [ ] **Step 1: Create feature branch**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git checkout main-dev && git pull
git checkout -b feature/issue-259-rag-dashboard
git config branch.feature/issue-259-rag-dashboard.parent main-dev
```

- [ ] **Step 2: Research existing KnowledgeBase structure**

Read these files:
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/` — all entities, queries, handlers
- `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`
- `apps/api/src/Api/Routing/RagDashboardEndpoints.cs`
- `apps/api/src/Api/Routing/AgentEndpoints.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/` — CreateAgent patterns

Identify:
- How agents link to games (AgentId, GameId)
- VectorDocument structure and states
- Existing RAG readiness checks
- Agent creation command structure

- [ ] **Step 3: Create RAG readiness query**

```csharp
// GetGameRagReadinessQuery.cs
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Aggregates RAG readiness status for a game: documents indexed, vector store health,
/// agent availability, and overall readiness score.
/// Issue #260: GetGameRagReadinessQuery.
/// </summary>
public record GetGameRagReadinessQuery(Guid GameId) : IRequest<GameRagReadinessDto>;

public record GameRagReadinessDto(
    Guid GameId,
    bool IsReady,
    double ReadinessScore,
    RagReadinessChecks Checks);

public record RagReadinessChecks(
    CheckResult DocumentsIndexed,
    CheckResult VectorStoreHealthy,
    CheckResult EmbeddingServiceAvailable,
    CheckResult AgentConfigured);

public record CheckResult(
    bool Passed,
    string Status,
    string? Detail);
```

The handler checks:
1. At least 1 PdfDocument with state=Indexed for the game
2. Qdrant collection exists and has vectors for the game
3. Embedding service is healthy (health check endpoint)
4. An agent exists for the game

ReadinessScore = (passed checks / total checks) * 100

- [ ] **Step 4: Create handler**

Follow existing handler patterns. Query PdfDocuments by GameId, check agent existence, call embedding service health endpoint.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/
git commit -m "feat(admin): RAG readiness aggregation query (#260)"
```

### Task 2: Agent cost estimation query (#261)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/EstimateAgentCostQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/EstimateAgentCostQueryHandler.cs`

- [ ] **Step 1: Create cost estimation query**

```csharp
// EstimateAgentCostQuery.cs
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Estimates the cost of running an AI agent for a game based on document count,
/// model selection, and expected usage patterns.
/// Issue #261: EstimateAgentCostQuery.
/// </summary>
public record EstimateAgentCostQuery(
    Guid GameId,
    string? ModelId = null,
    int? ExpectedQueriesPerMonth = null) : IRequest<AgentCostEstimateDto>;

public record AgentCostEstimateDto(
    Guid GameId,
    CostBreakdown Embedding,
    CostBreakdown Inference,
    CostBreakdown Storage,
    decimal TotalMonthlyEstimate,
    string Currency,
    string ModelUsed,
    CostAssumptions Assumptions);

public record CostBreakdown(
    string Category,
    decimal EstimatedCost,
    string Unit,
    string Detail);

public record CostAssumptions(
    int DocumentCount,
    int EstimatedChunks,
    int QueriesPerMonth,
    int AvgTokensPerQuery,
    int AvgTokensPerResponse);
```

The handler calculates:
- **Embedding cost**: chunks × embedding model price per token
- **Inference cost**: queries/month × (input + output tokens) × model price
- **Storage cost**: vector count × Qdrant storage per vector
- Uses existing model pricing from OpenRouter config or AgentDefaults

- [ ] **Step 2: Create handler and commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/
git commit -m "feat(admin): agent cost estimation query (#261)"
```

### Task 3: Enhanced CreateAgent command (#262) and endpoints

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/` — enhance existing CreateAgent
- Create or modify: `apps/api/src/Api/Routing/RagDashboardEndpoints.cs` — add readiness + cost endpoints

- [ ] **Step 1: Research existing CreateAgent command**

Read the existing CreateAgentWithSetupCommand (or similar). Enhance it to:
- Accept optional document selection (which PDFs to include in RAG)
- Accept model configuration
- Auto-configure RAG pipeline if documents are ready

- [ ] **Step 2: Add endpoints**

```csharp
// In RagDashboardEndpoints.cs, add:

// GET /api/v1/admin/rag/games/{gameId}/readiness
ragGroup.MapGet("/games/{gameId}/readiness", async (
    HttpContext context,
    IMediator mediator,
    Guid gameId,
    CancellationToken ct) =>
{
    var (authorized, _, error) = context.RequireAdminSession();
    if (!authorized) return error!;

    var result = await mediator.Send(new GetGameRagReadinessQuery(gameId), ct)
        .ConfigureAwait(false);
    return Results.Ok(result);
})
.RequireAuthorization("RequireAdminOrAbove")
.WithSummary("Get RAG readiness for game")
.WithDescription("Aggregated readiness check: documents, vectors, embeddings, agent");

// GET /api/v1/admin/rag/games/{gameId}/cost-estimate
ragGroup.MapGet("/games/{gameId}/cost-estimate", async (
    HttpContext context,
    IMediator mediator,
    Guid gameId,
    string? modelId,
    int? queriesPerMonth,
    CancellationToken ct) =>
{
    var (authorized, _, error) = context.RequireAdminSession();
    if (!authorized) return error!;

    var query = new EstimateAgentCostQuery(gameId, modelId, queriesPerMonth);
    var result = await mediator.Send(query, ct).ConfigureAwait(false);
    return Results.Ok(result);
})
.RequireAuthorization("RequireAdminOrAbove")
.WithSummary("Estimate agent cost for game")
.WithDescription("Cost estimation based on documents, model, and expected usage");
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/ apps/api/src/Api/Routing/
git commit -m "feat(admin): enhanced CreateAgent and RAG dashboard endpoints (#262)"
```

---

## Chunk 2: Frontend Schemas, Hooks, Components (#263-#267)

### Task 4: Zod schemas and API client (#263)

**Files:**
- Modify: `apps/web/src/lib/api/schemas/` — add RAG dashboard schemas
- Modify: `apps/web/src/lib/api/clients/adminClient.ts` — add methods

- [ ] **Step 1: Add Zod schemas**

```typescript
// RAG Dashboard schemas
export const checkResultSchema = z.object({
  passed: z.boolean(),
  status: z.string(),
  detail: z.string().nullable(),
});

export const ragReadinessChecksSchema = z.object({
  documentsIndexed: checkResultSchema,
  vectorStoreHealthy: checkResultSchema,
  embeddingServiceAvailable: checkResultSchema,
  agentConfigured: checkResultSchema,
});

export const gameRagReadinessSchema = z.object({
  gameId: z.string(),
  isReady: z.boolean(),
  readinessScore: z.number(),
  checks: ragReadinessChecksSchema,
});

export const costBreakdownSchema = z.object({
  category: z.string(),
  estimatedCost: z.number(),
  unit: z.string(),
  detail: z.string(),
});

export const agentCostEstimateSchema = z.object({
  gameId: z.string(),
  embedding: costBreakdownSchema,
  inference: costBreakdownSchema,
  storage: costBreakdownSchema,
  totalMonthlyEstimate: z.number(),
  currency: z.string(),
  modelUsed: z.string(),
  assumptions: z.object({
    documentCount: z.number(),
    estimatedChunks: z.number(),
    queriesPerMonth: z.number(),
    avgTokensPerQuery: z.number(),
    avgTokensPerResponse: z.number(),
  }),
});

export type GameRagReadiness = z.infer<typeof gameRagReadinessSchema>;
export type AgentCostEstimate = z.infer<typeof agentCostEstimateSchema>;
```

- [ ] **Step 2: Add API client methods**

```typescript
// In adminClient.ts:
async getRagReadiness(gameId: string): Promise<GameRagReadiness> {
  const result = await httpClient.get(
    `/api/v1/admin/rag/games/${gameId}/readiness`,
    gameRagReadinessSchema
  );
  if (!result) throw new Error('Failed to load RAG readiness');
  return result;
},

async getAgentCostEstimate(
  gameId: string,
  modelId?: string,
  queriesPerMonth?: number
): Promise<AgentCostEstimate> {
  const params = new URLSearchParams();
  if (modelId) params.set('modelId', modelId);
  if (queriesPerMonth) params.set('queriesPerMonth', String(queriesPerMonth));
  const qs = params.toString();
  const result = await httpClient.get(
    `/api/v1/admin/rag/games/${gameId}/cost-estimate${qs ? `?${qs}` : ''}`,
    agentCostEstimateSchema
  );
  if (!result) throw new Error('Failed to load cost estimate');
  return result;
},
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/
git commit -m "feat(admin): RAG dashboard Zod schemas and API client (#263)"
```

### Task 5: React Query hooks (#264)

**Files:**
- Create: `apps/web/src/hooks/queries/useRagDashboard.ts`

- [ ] **Step 1: Create hooks file**

```typescript
// apps/web/src/hooks/queries/useRagDashboard.ts
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export const ragDashboardKeys = {
  all: ['rag-dashboard'] as const,
  readiness: (gameId: string) => [...ragDashboardKeys.all, 'readiness', gameId] as const,
  costEstimate: (gameId: string) => [...ragDashboardKeys.all, 'cost-estimate', gameId] as const,
};

export function useRagReadiness(gameId: string | undefined) {
  return useQuery({
    queryKey: ragDashboardKeys.readiness(gameId ?? ''),
    queryFn: () => api.admin.getRagReadiness(gameId!),
    enabled: !!gameId,
    staleTime: 30_000,
  });
}

export function useAgentCostEstimate(
  gameId: string | undefined,
  modelId?: string,
  queriesPerMonth?: number
) {
  return useQuery({
    queryKey: [...ragDashboardKeys.costEstimate(gameId ?? ''), modelId, queriesPerMonth],
    queryFn: () => api.admin.getAgentCostEstimate(gameId!, modelId, queriesPerMonth),
    enabled: !!gameId,
    staleTime: 60_000,
  });
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/hooks/queries/
git commit -m "feat(admin): RAG dashboard React Query hooks (#264)"
```

### Task 6: RagReadinessIndicator component (#265)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/rag/components/RagReadinessIndicator.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/rag/components/__tests__/RagReadinessIndicator.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RagReadinessIndicator } from '../RagReadinessIndicator';

const mockReadiness = {
  gameId: 'game-1',
  isReady: true,
  readinessScore: 75,
  checks: {
    documentsIndexed: { passed: true, status: 'OK', detail: '3 documents indexed' },
    vectorStoreHealthy: { passed: true, status: 'OK', detail: null },
    embeddingServiceAvailable: { passed: true, status: 'OK', detail: null },
    agentConfigured: { passed: false, status: 'Missing', detail: 'No agent configured' },
  },
};

describe('RagReadinessIndicator', () => {
  it('renders readiness score', () => {
    render(<RagReadinessIndicator readiness={mockReadiness} />);
    expect(screen.getByTestId('readiness-score')).toHaveTextContent('75%');
  });

  it('renders check items', () => {
    render(<RagReadinessIndicator readiness={mockReadiness} />);
    expect(screen.getByTestId('check-documentsIndexed')).toBeInTheDocument();
    expect(screen.getByTestId('check-agentConfigured')).toBeInTheDocument();
  });

  it('shows passed/failed status', () => {
    render(<RagReadinessIndicator readiness={mockReadiness} />);
    // documentsIndexed passed
    expect(screen.getByTestId('check-documentsIndexed')).toHaveAttribute('data-passed', 'true');
    // agentConfigured failed
    expect(screen.getByTestId('check-agentConfigured')).toHaveAttribute('data-passed', 'false');
  });
});
```

- [ ] **Step 2: Implement component**

Shows a readiness score circle/bar + 4 check items with pass/fail status. Glassmorphism card design.

- [ ] **Step 3: Run tests and commit**

```bash
cd apps/web && pnpm test -- --run src/app/admin/\(dashboard\)/rag/components/__tests__/RagReadinessIndicator.test.tsx
git add apps/web/src/app/admin/\(dashboard\)/rag/
git commit -m "feat(admin): RagReadinessIndicator component (#265)"
```

### Task 7: AgentSetupPanel component (#266)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/rag/components/AgentSetupPanel.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/rag/components/__tests__/AgentSetupPanel.test.tsx`

- [ ] **Step 1: Write failing test**

Test covers:
- Renders model selection dropdown
- Renders document selection checkboxes
- Shows cost estimate when model selected
- "Create Agent" button enabled when all required fields filled
- Submit calls API

- [ ] **Step 2: Implement component**

Agent setup form with:
- Game selector (search existing games)
- Model selection (dropdown with available models)
- Document selection (checkboxes for indexed PDFs)
- Cost estimate display (auto-updates when model changes)
- "Create Agent" button with confirmation

- [ ] **Step 3: Run tests and commit**

```bash
cd apps/web && pnpm test -- --run src/app/admin/\(dashboard\)/rag/components/__tests__/AgentSetupPanel.test.tsx
git add apps/web/src/app/admin/\(dashboard\)/rag/components/
git commit -m "feat(admin): AgentSetupPanel component (#266)"
```

### Task 8: InlineChatPanel component (#267)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/rag/components/InlineChatPanel.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/rag/components/__tests__/InlineChatPanel.test.tsx`

- [ ] **Step 1: Write failing test**

Test covers:
- Renders chat input field
- Renders message list
- Sending a message calls the agent chat API
- Shows loading state during response
- Displays AI response when received

- [ ] **Step 2: Implement component**

Inline chat panel for testing the agent directly from the dashboard:
- Message list with user/AI message bubbles
- Input field with send button
- Connects to existing agent chat SSE endpoint
- Shows RAG source documents in response metadata

Reuse patterns from existing `SessionChatWidget.tsx` and `useAgentChatStream.ts`.

- [ ] **Step 3: Run tests and commit**

```bash
cd apps/web && pnpm test -- --run src/app/admin/\(dashboard\)/rag/components/__tests__/InlineChatPanel.test.tsx
git add apps/web/src/app/admin/\(dashboard\)/rag/components/
git commit -m "feat(admin): InlineChatPanel component (#267)"
```

---

## Chunk 3: Unified Dashboard Page (#268)

### Task 9: RAG Setup Dashboard Page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/rag/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/rag/RagDashboard.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/rag/NavConfig.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/rag/__tests__/RagDashboard.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';

vi.mock('@/hooks/queries/useRagDashboard', () => ({
  useRagReadiness: vi.fn().mockReturnValue({ data: null, isLoading: true }),
  useAgentCostEstimate: vi.fn().mockReturnValue({ data: null, isLoading: true }),
}));

import { RagDashboard } from '../RagDashboard';

describe('RagDashboard', () => {
  it('renders dashboard with all sections', () => {
    render(<RagDashboard />);
    expect(screen.getByTestId('rag-dashboard')).toBeInTheDocument();
    expect(screen.getByText(/RAG Setup/i)).toBeInTheDocument();
  });

  it('renders game selector', () => {
    render(<RagDashboard />);
    expect(screen.getByTestId('rag-game-selector')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Implement unified dashboard**

Layout:
```
┌─────────────────────────────────────────┐
│  RAG Setup Dashboard                     │
├──────────────┬──────────────────────────┤
│  Game Select │  Readiness Indicator     │
│  (search)    │  ┌──┐┌──┐┌──┐┌──┐      │
│              │  │✅││✅││✅││❌│      │
│              │  └──┘└──┘└──┘└──┘      │
├──────────────┼──────────────────────────┤
│  Agent Setup │  Cost Estimate           │
│  - Model     │  ├─ Embedding: $X.XX    │
│  - Docs      │  ├─ Inference: $X.XX    │
│  - Config    │  └─ Total: $X.XX/mo     │
├──────────────┴──────────────────────────┤
│  Inline Chat Test                        │
│  ┌─────────────────────────────────────┐│
│  │ [User message]                      ││
│  │ [AI response with sources]          ││
│  │ [Input field]              [Send]   ││
│  └─────────────────────────────────────┘│
└─────────────────────────────────────────┘
```

The dashboard is progressive:
1. Select a game → loads readiness
2. If not ready → shows what's missing with action links
3. If ready → shows cost estimate + agent setup
4. After agent created → shows inline chat for testing

- [ ] **Step 3: Create page wrapper**

```tsx
// apps/web/src/app/admin/(dashboard)/rag/page.tsx
import { RagDashboard } from './RagDashboard';
import { RagNavConfig } from './NavConfig';

export default function RagDashboardPage() {
  return (
    <div className="space-y-5" data-testid="rag-page">
      <RagNavConfig />
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          RAG Setup
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Configure RAG agents: check readiness, estimate costs, create agents, and test inline.
        </p>
      </div>
      <RagDashboard />
    </div>
  );
}
```

- [ ] **Step 4: Run tests**

```bash
cd apps/web && pnpm test -- --run src/app/admin/\(dashboard\)/rag/
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/rag/
git commit -m "feat(admin): unified RAG Setup dashboard page (#268)"
```

### Task 10: Final validation and PR

- [ ] **Step 1: Run all tests and checks**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm test --run
cd ../api/src/Api && dotnet build
```

- [ ] **Step 2: Create PR**

```bash
git push -u origin feature/issue-259-rag-dashboard
gh pr create --base main-dev --title "feat(admin): RAG Dashboard — readiness, cost estimation, agent setup (#260-#268)" --body "$(cat <<'EOF'
## Summary
- RAG readiness aggregation query with 4-check health assessment (#260)
- Agent cost estimation based on documents, model, and usage (#261)
- Enhanced CreateAgent with document selection and model config (#262)
- Zod schemas and admin API client methods (#263)
- React Query hooks for RAG dashboard data (#264)
- RagReadinessIndicator component with pass/fail checks (#265)
- AgentSetupPanel with model selection and cost preview (#266)
- InlineChatPanel for testing agents directly from dashboard (#267)
- Unified RAG Setup dashboard page (#268)

## Test plan
- [ ] Readiness check shows correct status for game with/without documents
- [ ] Cost estimate updates when changing model or query volume
- [ ] Agent creation flow works end-to-end
- [ ] Inline chat connects to agent and shows responses
- [ ] All frontend tests pass
- [ ] Backend builds successfully

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Update issues**

```bash
for issue in 260 261 262 263 264 265 266 267 268; do
  gh issue edit $issue --add-label "status:in-review"
done
```

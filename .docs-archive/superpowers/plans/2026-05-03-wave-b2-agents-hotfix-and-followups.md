# Wave B.2 Agents Backend Hotfix + 6 Followups — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Risolvere il P0 di Wave B.2 (`/agents` permanently broken in prod per backend missing) + 6 followup azioni emerse dallo spec-panel critique 2026-05-03 (issue tracking, audit doc, CI gate, E2E smoke real backend, RecentActivityRail backend, AgentDto.GameName drift).

**Architecture:** 7 azioni indipendenti, ognuna isolata in un branch + PR separato verso `main-dev`. Sequenza ottimizzata per dipendenze: hotfix backend → issue tracking parallelo → audit doc → CI gate → E2E smoke → RecentActivityRail backend → AgentDto.GameName cleanup. Phase 1 unblocks Phase 5 (E2E smoke ha bisogno del route reale per testare path success).

**Tech Stack:**
- Backend: .NET 9, ASP.NET Minimal APIs + MediatR, FluentValidation, xUnit + Testcontainers
- Frontend: Next.js 16, TanStack Query, Vitest + Playwright
- CI: GitHub Actions
- Issue tracking: GitHub Issues (gh CLI)

**Effort totale stimato:** ~16-22h (Phase 1 ~3h + Phase 2 ~30min + Phase 3 ~2h + Phase 4 ~2h + Phase 5 ~6-8h + Phase 6 ~3h + Phase 7 ~1h)

**Audit evidence**: vedi memoria `project_wave-b2-agents-backend-missing.md` + sessione spec-panel 2026-05-03 che ha verificato con `verification-before-completion` ogni claim.

---

## 0. Pre-flight gates (P-1 through P-3) — MUST PASS prima di iniziare ogni Phase

### P-1 — Branch hygiene

```bash
git fetch origin
git checkout main-dev
git pull --ff-only origin main-dev
git status   # MUST be clean
git log -1 --oneline   # confirm last main-dev commit
```

**Pass criteria**: working tree clean, on `main-dev`, fast-forward succeeded.

### P-2 — Backend dev container up (per integration tests Phase 1, Phase 5, Phase 7)

```bash
cd infra && make dev-core   # postgres + redis + api senza AI/monitoring
# wait ~30s
curl -s http://localhost:8080/api/v1/library | head   # MUST return 401 (route exists, auth required)
```

**Pass criteria**: API up at `:8080`, library endpoint reachable.

### P-3 — Verifica skill MCP disponibili

Per Phase 5 (E2E smoke real backend) richiediamo Playwright MCP. Verifica che il browser automation sia disponibile prima di iniziare Phase 5.

---

## Phase 1 — Backend hotfix `GET /api/v1/agents` (Action #1)

**Issue**: nuova issue (creata in Phase 2 Task 2.1)
**Branch**: `feature/issue-XXX-agents-getall-hotfix` (parent: `main-dev`)
**PR target**: `main-dev`
**Effort**: ~3h
**Dependency**: P-1, P-2

**Approach**: implementare il route HTTP `GET /api/v1/agents` consumando il `GetAllAgentsQueryHandler` MediatR già esistente. Il route ritorna il response shape che il frontend già aspetta (`{ success: boolean, agents: AgentDto[], count: number }` per `GetAllAgentsResponseSchema`). Hotfix scope = SOLO list endpoint; create/update/configure/typologies restano missing (issue separate via Phase 2/3).

### Task 1.1 — Setup branch + issue link

**Files:** —

- [ ] **Step 1: Create branch from main-dev**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-XXX-agents-getall-hotfix
git config branch.feature/issue-XXX-agents-getall-hotfix.parent main-dev
```

- [ ] **Step 2: Verifica issue creata in Phase 2.1**

```bash
gh issue view <ISSUE_NUMBER> --json number,title,state
```

Expected: `state: OPEN`, title contains `Wave B.2 hotfix`.

### Task 1.2 — Integration test rosso per `GET /api/v1/agents`

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/Agents/AgentsEndpointsIntegrationTests.cs`

- [ ] **Step 1: Scrivi il test che verifica route esistenza + shape**

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Agents;

[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentsEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public AgentsEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"agents_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetAgents_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/agents");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgents_WithValidSession_ReturnsEmptyList()
    {
        // Arrange: authenticated user, no agents seeded
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        _client.DefaultRequestHeaders.Add("Cookie", $"meepleai_session={sessionToken}");

        // Act
        var response = await _client.GetAsync("/api/v1/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAllAgentsResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Agents.Should().BeEmpty();
        body.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetAgents_WithActiveOnlyFilter_OnlyReturnsActive()
    {
        // Arrange: seed 2 active + 1 inactive agent definitions
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        _client.DefaultRequestHeaders.Add("Cookie", $"meepleai_session={sessionToken}");
        await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 2, inactiveCount: 1);

        // Act
        var response = await _client.GetAsync("/api/v1/agents?activeOnly=true");

        // Assert
        var body = await response.Content.ReadFromJsonAsync<GetAllAgentsResponse>();
        body!.Agents.Should().HaveCount(2);
        body.Agents.Should().OnlyContain(a => a.IsActive);
    }
}

internal record GetAllAgentsResponse(bool Success, List<AgentDto> Agents, int Count);
```

- [ ] **Step 2: Scrivi helper di seed in TestSessionHelper**

```csharp
// In apps/api/tests/Api.Tests/TestHelpers/TestSessionHelper.cs (extend existing class)
public static async Task SeedAgentDefinitionsAsync(
    MeepleAiDbContext dbContext, int activeCount, int inactiveCount)
{
    for (var i = 0; i < activeCount; i++)
    {
        dbContext.AgentDefinitions.Add(AgentDefinition.Create(
            name: $"Active Agent {i}",
            type: "Strategist",
            strategyName: "HybridSearch",
            isActive: true));
    }
    for (var i = 0; i < inactiveCount; i++)
    {
        dbContext.AgentDefinitions.Add(AgentDefinition.Create(
            name: $"Inactive Agent {i}",
            type: "Rules",
            strategyName: "VectorOnly",
            isActive: false));
    }
    await dbContext.SaveChangesAsync();
}
```

- [ ] **Step 3: Run integration test → verify all RED**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~AgentsEndpointsIntegrationTests" --logger "console;verbosity=normal"
```

Expected: 3 test FAIL — il primo con `Unauthorized` PASS (l'API ritorna 404 ma il fail riporta status mismatch), il secondo+terzo FAIL con `Expected: OK, but found: NotFound`.

- [ ] **Step 4: Commit (red)**

```bash
git add apps/api/tests/Api.Tests/Integration/Agents/AgentsEndpointsIntegrationTests.cs apps/api/tests/Api.Tests/TestHelpers/TestSessionHelper.cs
git commit -m "test(agents): integration tests for GET /api/v1/agents (red)

Refs #XXX (Wave B.2 hotfix)"
```

### Task 1.3 — Implementazione route `GET /api/v1/agents`

**Files:**
- Create: `apps/api/src/Api/Routing/AgentsEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs:702`

- [ ] **Step 1: Create AgentsEndpoints.cs con MapGet `/agents`**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User-facing Agents endpoints (read-only listing).
/// Issue #XXX (Wave B.2 hotfix): expose existing GetAllAgentsQuery handler over HTTP.
/// </summary>
internal static class AgentsEndpoints
{
    public static RouteGroupBuilder MapAgentsEndpoints(this RouteGroupBuilder group)
    {
        MapGetAgentsEndpoint(group);
        return group;
    }

    private static void MapGetAgentsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents", async (
            [FromQuery] bool? activeOnly,
            [FromQuery] string? type,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var query = new GetAllAgentsQuery(
                ActiveOnly: activeOnly,
                Type: type
            );
            var agents = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                success = true,
                agents,
                count = agents.Count
            });
        })
        .RequireAuthenticatedUser()
        .Produces(200)
        .Produces(401)
        .WithTags("Agents")
        .WithSummary("List all agents")
        .WithDescription("Returns all active/inactive agents with optional activeOnly and type filters. Wraps GetAllAgentsQuery for user-facing consumption.")
        .WithOpenApi();
    }
}
```

- [ ] **Step 2: Register endpoint in Program.cs (after MapAiEndpoints)**

In `apps/api/src/Api/Program.cs:702` add new line after `v1Api.MapAiEndpoints();`:

```csharp
v1Api.MapAiEndpoints();
v1Api.MapAgentsEndpoints(); // Issue #XXX (Wave B.2 hotfix): user-facing agent listing
v1Api.MapRulebookAnalysisEndpoints(); // ISSUE-2402: Rulebook analysis service
```

- [ ] **Step 3: Verify build success**

```bash
cd apps/api && dotnet build --no-restore
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 4: Run integration tests → verify GREEN**

```bash
dotnet test --filter "FullyQualifiedName~AgentsEndpointsIntegrationTests" --logger "console;verbosity=normal"
```

Expected: 3 tests PASS.

- [ ] **Step 5: Commit (green)**

```bash
git add apps/api/src/Api/Routing/AgentsEndpoints.cs apps/api/src/Api/Program.cs
git commit -m "feat(agents): expose GET /api/v1/agents user-facing route (green)

Wraps existing GetAllAgentsQueryHandler MediatR handler over HTTP.
Replaces backend-missing 404 with proper {success, agents, count} response
matching frontend GetAllAgentsResponseSchema.

Refs #XXX (Wave B.2 hotfix)"
```

### Task 1.4 — Frontend cleanup `@todo BACKEND MISSING` annotation

**Files:**
- Modify: `apps/web/src/lib/api/clients/agentsClient.ts:60-66`

- [ ] **Step 1: Rimuovi @todo BACKEND MISSING dal getAll JSDoc**

Cerca le righe attuali:

```typescript
    /**
     * Get all agents with optional filtering
     * Implements GetAllAgentsQuery from backend
     * @param activeOnly If true, only return active agents
     * @param type Optional agent type filter
     * @todo BACKEND MISSING: No route registered for GET /api/v1/agents. Returns empty array via fallback. See: endpoint audit 2026-04-15
     */
    async getAll(activeOnly?: boolean, type?: string): Promise<AgentDto[]> {
```

Sostituisci con:

```typescript
    /**
     * Get all agents with optional filtering
     * Implements GetAllAgentsQuery from backend
     * @param activeOnly If true, only return active agents
     * @param type Optional agent type filter
     * Resolved by Wave B.2 hotfix #XXX (2026-05-03) — route registered in AgentsEndpoints.cs.
     */
    async getAll(activeOnly?: boolean, type?: string): Promise<AgentDto[]> {
```

- [ ] **Step 2: Run typecheck + lint**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```

Expected: 0 errors.

- [ ] **Step 3: Run unit test AgentsLibraryView**

```bash
pnpm test -- AgentsLibraryView
```

Expected: tutti i test esistenti continuano a passare (modifica è solo JSDoc, no behavior change).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/clients/agentsClient.ts
git commit -m "chore(agents): remove BACKEND MISSING todo from getAll annotation

Route now registered via AgentsEndpoints.cs (Wave B.2 hotfix).

Refs #XXX"
```

### Task 1.5 — Smoke test manuale + push + PR

- [ ] **Step 1: Avvia dev stack + verifica route reale**

```bash
cd infra && make dev-core
sleep 20
# Login (assumi cookie file da dev session)
curl -s -b cookies.txt http://localhost:8080/api/v1/agents | jq '.'
```

Expected: `{ "success": true, "agents": [], "count": 0 }` (o agents popolati se DB seeded).

- [ ] **Step 2: Push branch + create PR target main-dev**

```bash
git push -u origin feature/issue-XXX-agents-getall-hotfix
gh pr create --base main-dev --title "fix(agents): expose GET /api/v1/agents user-facing route (Wave B.2 hotfix)" --body "$(cat <<'EOF'
## Summary
- Expose `GET /api/v1/agents` user-facing route via new `AgentsEndpoints.cs`
- Wraps existing `GetAllAgentsQueryHandler` MediatR handler over HTTP
- Resolves P0 finding from spec-panel critique 2026-05-03: Wave B.2 page `/agents` was permanently in `error` state in production because backend route did not exist (404 → TanStack 3 retries → query.isError=true → realKind='error')

## Scope (deliberately limited to LIST only)
This hotfix targets ONLY `getAll`. The other 13 BACKEND MISSING agent endpoints (audit 2026-04-15) are tracked in followup issues — see plan §Phase 3 audit doc.

Out of scope (deferred):
- POST `/agents/create-with-setup` (CTA "Crea agente" still broken)
- GET `/agents/{id}` agent detail
- PUT/PATCH/POST configuration endpoints

## Test plan
- [x] Integration tests `AgentsEndpointsIntegrationTests` (3 tests: 401/empty/activeOnly)
- [x] Manual smoke test against dev stack at :8080
- [x] Frontend typecheck + lint + unit AgentsLibraryView still pass
- [ ] Manual verification: navigate to `/agents` after merge → list renders (or empty state if no seeded agents)

Closes #XXX
EOF
)"
```

- [ ] **Step 3: Wait CI green + merge**

```bash
gh pr checks --watch
# After all green
gh pr merge --squash --delete-branch --admin   # if codecov flake (Wave A pattern)
# OR
gh pr merge --squash --delete-branch   # if all green naturally
```

- [ ] **Step 4: Post-merge cleanup**

```bash
git checkout main-dev && git pull --ff-only
git branch -D feature/issue-XXX-agents-getall-hotfix
git remote prune origin
```

- [ ] **Step 5: Verify Issue closure on GitHub**

```bash
gh issue view <ISSUE_NUMBER> --json state
```

Expected: `state: CLOSED` (auto-closed via `Closes #XXX` in PR body).

---

## Phase 2 — Issue tracking (Actions #2 + #6)

**Branch**: nessuno (issue creation only)
**Effort**: ~30min
**Dependency**: P-1

### Task 2.1 — Apri issue Wave B.2 hotfix

- [ ] **Step 1: Crea issue P0**

```bash
gh issue create --title "[Wave B.2 hotfix] /agents backend route missing — page permanently broken in prod" --label "bug,P0,wave-b" --body "$(cat <<'EOF'
## Summary

PR #637 (Wave B.2, merged 2026-04-30) ha mergiato la pagina `/agents` con CI green, ma in produzione la pagina è permanently broken: il frontend chiama `GET /api/v1/agents` ma il backend NON ha registrato il route per quel path.

## Root cause

L'audit `2026-04-15` ha già tracciato il gap esplicitamente in `apps/web/src/lib/api/clients/agentsClient.ts:65`:

```typescript
@todo BACKEND MISSING: No route registered for GET /api/v1/agents. Returns empty array via fallback. See: endpoint audit 2026-04-15
```

Il fix non è stato linkato a Wave B.2 prima del merge.

## Effetto produzione

1. User naviga a `/agents`
2. `useAgents({})` → `api.agents.getAll()` → `GET /api/v1/agents` → 404
3. `httpClient.handleError` throws `NotFoundError`
4. TanStack Query default 3 retries → tutti 404
5. `query.isError = true` → orchestrator `realKind='error'`
6. UI: `EmptyAgents kind='error'` con CTA "Riprova" — nessun agente listato

## Scope hotfix (questo issue)

SOLO `GET /api/v1/agents` user-facing read endpoint. L'handler MediatR `GetAllAgentsQueryHandler` esiste già — manca solo registrazione HTTP.

## Out of scope (tracked separately — vedi Phase 3 audit doc)

13 altri endpoint Agents management ancora missing — issues separate per ognuno (creare in Phase 3).

## Acceptance criteria (DoD)

- [ ] Route `GET /api/v1/agents` registrato in nuovo file `apps/api/src/Api/Routing/AgentsEndpoints.cs`
- [ ] Endpoint registrato in `Program.cs` dopo `MapAiEndpoints()`
- [ ] Integration tests `AgentsEndpointsIntegrationTests` (3 test: 401/empty/activeOnly)
- [ ] `@todo BACKEND MISSING` rimosso da `agentsClient.ts:65`
- [ ] PR target `main-dev` (branch parent)
- [ ] Manual smoke test: `/agents` route renders agent list (o empty state se DB pulito)
- [ ] Issue closed via `Closes #XXX` in PR body

## Refs

- Audit memory: `project_wave-b2-agents-backend-missing.md`
- Spec-panel critique: 2026-05-03
- Affected PR: #637 (Wave B.2)
- Plan: `docs/superpowers/plans/2026-05-03-wave-b2-agents-hotfix-and-followups.md`
EOF
)"
```

Expected: nuova issue creata con number `<ISSUE_NUMBER>`. Annotare per Phase 1 Task 1.1.

- [ ] **Step 2: Save issue number per Phase 1**

```bash
echo "ISSUE_NUMBER=<numero_appena_creato>" >> /tmp/wave-b2-hotfix.env
```

### Task 2.2 — Apri issue Wave B.3 followup `/library/activity`

- [ ] **Step 1: Crea issue P1**

```bash
gh issue create --title "[Wave B.3 followup] /library/activity backend endpoint + RecentActivityRail wiring" --label "enhancement,P1,wave-b" --body "$(cat <<'EOF'
## Summary

`LibraryHubV2.tsx:427` passa `<RecentActivityRail items={[]} />` hardcoded perché il backend `GET /api/v1/library/activity` non esiste. Il rail mostra placeholder permanente. Documented gap, not bug — ma UX debt da risolvere.

## Implementation outline

### Backend
- New query `GetLibraryActivityQuery(userId, limit = 20)` aggregando domain events già emessi:
  - `GameAddedToLibraryEvent`
  - `GameRemovedFromLibraryEvent`
  - `GameSessionRecordedEvent`
  - `GameStateChangedEvent`
- New endpoint `GET /api/v1/library/activity` in `UserLibraryCoreEndpoints.cs`
- DTO `LibraryActivityItemDto { id, type, timestamp, gameTitle, message }`

### Frontend
- New hook `useLibraryActivity({ limit: 20 })` in `apps/web/src/hooks/queries/useLibrary.ts`
- Update `LibraryHubV2.tsx:427` to pass real data: `<RecentActivityRail items={activityQuery.data ?? []} />`

## Acceptance criteria (DoD)

- [ ] Backend: query + handler + endpoint + integration test (4 events × pagination)
- [ ] Frontend: hook + LibraryHubV2 wireup + unit test (mock activity query)
- [ ] Branch from main-dev, PR target main-dev
- [ ] Update memory `project_wave-b2-agents-backend-missing.md` rimuovendo riferimento al gap

## Refs

- Spec-panel critique 2026-05-03
- LibraryHubV2.tsx:33-37 docstring documenting gap
- Plan: `docs/superpowers/plans/2026-05-03-wave-b2-agents-hotfix-and-followups.md` Phase 7
EOF
)"
```

- [ ] **Step 2: Salva issue number per Phase 7**

```bash
echo "ACTIVITY_ISSUE_NUMBER=<numero>" >> /tmp/wave-b2-hotfix.env
```

### Task 2.3 — Verifica issue tracking

- [ ] **Step 1: List both issues**

```bash
gh issue list --label "wave-b" --limit 5 --state open
```

Expected: 2 issues (Wave B.2 hotfix + Wave B.3 followup).

---

## Phase 3 — Audit doc 14 endpoint Agents missing (Action #3)

**Issue**: tracked in dedicated branch
**Branch**: `docs/agents-backend-audit-2026-05-03` (parent: `main-dev`)
**PR target**: `main-dev`
**Effort**: ~2h
**Dependency**: P-1

### Task 3.1 — Setup branch + struct doc

**Files:**
- Create: `docs/audit/agents-backend-audit-2026-05-03.md`

- [ ] **Step 1: Branch + dir setup**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b docs/agents-backend-audit-2026-05-03
git config branch.docs/agents-backend-audit-2026-05-03.parent main-dev
mkdir -p docs/audit
```

### Task 3.2 — Scrivi audit doc

**Files:**
- Create: `docs/audit/agents-backend-audit-2026-05-03.md`

- [ ] **Step 1: Crea il documento di audit completo**

```markdown
# Agents Backend Endpoint Audit — 2026-05-03

> Verifies the 14 `@todo BACKEND MISSING` annotations in `apps/web/src/lib/api/clients/agentsClient.ts` from the original audit `2026-04-15`.

## Context

Spec-panel critique 2026-05-03 ha verificato (con `verification-before-completion` gate) che il sub-system Agents management è ampiamente backend-missing. Esistono SOLO gli endpoint di INVOCAZIONE.

## Endpoint inventory

### ✅ Existing routes (registered backend)

| Path | Method | Backend file:line | Frontend consumer |
|---|---|---|---|
| `/agents/qa` | POST | `AiEndpoints.cs:63` | `useAskAgent` (chat) |
| `/agents/explain` | POST | `AiEndpoints.cs:76` | `useAskExplain` |
| `/agents/setup` | POST | `AiEndpoints.cs:84` | `agentsClient.generateSetupGuide` |
| `/agents/feedback` | POST | `AiEndpoints.cs:90` | feedback collection |
| `/agents/player-mode/suggest` | POST | `AiEndpoints.cs:98` | `agentsClient.suggestPlayerMove` |
| `/agents/explain/stream` | POST | `AiEndpoints.cs:112` | streaming explain |
| `/agents/qa/stream` | POST | `AiEndpoints.cs:119` | streaming QA |
| `/agents/arbitro/validate` | POST | `ArbitroAgentEndpoints.cs:44` | arbitro |
| `/agents/arbitro/feedback` | POST | `ArbitroAgentEndpoints.cs:124` | arbitro |
| `/agents/estimate-cost` | POST | `AdminKnowledgeBaseEndpoints.cs:75` | `useEstimateAgentCost` |
| `/games/{id}/agents` | GET | `GameEndpoints.cs:91` | `agentsClient.getUserAgentsForGame` |

### ❌ Missing routes (BACKEND MISSING)

| # | Path | Method | agentsClient:line | Consumer | Affected feature |
|---|---|---|---|---|---|
| 1 | `/agents` | GET | 65 | `useAgents` | Wave B.2 `/agents` list |
| 2 | `/agents/{id}` | GET | 111 | `agentsClient.getById` | (futura agent detail) |
| 3 | `/agents/{id}/status` | GET | 121 | `useAgentStatus` | agent readiness widget |
| 4 | `/agent-typologies` | GET | 157 | `useAgentConfigModal` | Wave B.2 modal create |
| 5 | `/agents/recent` | GET | 183 | `useRecentAgents` | dashboard widget |
| 6 | `/agents/{id}/invoke` | POST | 200 | `agentsClient.invoke` | legacy chat |
| 7 | `/agents` | POST | 220 | `agentsClient.create` | admin create |
| 8 | `/agents/{id}/configure` | PUT | 237 | `agentsClient.configure` | admin config |
| 9 | `/agents/user` | POST | 396 | AgentCreationSheet | Wave B.2 modal save (user-flow) |
| 10 | `/agents/create-with-setup` | POST | 461 | `useCreateAgentFlow` | Wave B.2 modal save (setup-flow) |
| 11 | `/agents/{id}/user` | PUT | 526 | (user agent edit) | edit user agent |
| 12 | `/agents/{id}/configuration` | GET | 640 | `useModels` | model config view |
| 13 | `/agents/{id}/configuration` | PATCH | 654 | `useModels` | model config edit |
| 14 | `/agents/quick-create` | POST | 693 | (quick onboarding) | onboarding |

## Resolution status

| # | Path | Resolution status | Linked issue |
|---|---|---|---|
| 1 | `GET /agents` | ✅ Resolved by Wave B.2 hotfix #XXX | (filed Phase 2.1) |
| 2-14 | (others) | ⏳ Followup | Filed individually below |

## Followup issues (created by this audit)

For each missing endpoint, file a separate issue with template:

```
Title: [BACKEND MISSING] {METHOD} /api/v1/{path} — {use case}

## Context
{frontend consumer + UX impact when called}

## Implementation
{handler MediatR if exists, else design}

## DoD
- [ ] Backend route registered
- [ ] Integration test
- [ ] Frontend `@todo BACKEND MISSING` removed at agentsClient.ts:{line}
- [ ] PR target main-dev
```

Run for each row 2-14 of "Missing routes" table.

## Alternative approaches

### Option A — Implement individual endpoints (current plan)
Each missing endpoint as separate PR. ~13 small PRs. Pros: low blast radius, parallelizable. Cons: 13 review cycles.

### Option B — Alias `/admin/agent-definitions` for non-admin
`AdminAgentDefinitionEndpoints.cs:65` ha già list/get/create/update/delete su path `/admin/agent-definitions`. Si può aliasare per non-admin con scope filter `(userId == currentUser.Id || isPublic)`. Pros: 1 PR risolve 8 endpoint (1,2,7,8,11,12,13,14). Cons: scope filter security review serio, modifica DTO (`AgentDefinitionDto` ≠ `AgentDto`).

### Option C — Spec dedicata Agents BC user-facing (long-term)
Design completo del sub-system Agents management user-facing come nuovo BC. Pros: clean architecture. Cons: ~1-2 sprint, blocca Wave B.2 fix.

**Recommendation**: Option A per i route già consumed (1, 4, 9, 10) — quelli che bloccano Wave B.2. Option B come spec separata per il resto.

## References

- Audit precedente: `2026-04-15` (in agentsClient.ts JSDoc)
- Memoria: `project_wave-b2-agents-backend-missing.md`
- Spec-panel critique: 2026-05-03
- Plan: `docs/superpowers/plans/2026-05-03-wave-b2-agents-hotfix-and-followups.md`
```

- [ ] **Step 2: Verifica typecheck/markdown lint (se esistono)**

```bash
# Markdown lint se configurato
test -f .markdownlint.json && pnpm exec markdownlint docs/audit/ || echo "no markdownlint config"
```

- [ ] **Step 3: Commit**

```bash
git add docs/audit/agents-backend-audit-2026-05-03.md
git commit -m "docs(audit): comprehensive Agents backend endpoint audit 2026-05-03

Documents 14 BACKEND MISSING annotations from 2026-04-15 audit + their
resolution status. Provides Option A/B/C strategy choices for next steps.

Resolves audit #3 from spec-panel critique 2026-05-03."
```

### Task 3.3 — File issue per ogni endpoint Missing rimanente

- [ ] **Step 1: Bulk-file 13 issues per endpoint (#2-#14 dalla tabella)**

```bash
for i in 2 3 4 5 6 7 8 9 10 11 12 13 14; do
  case $i in
    2) METHOD=GET; PATH="/agents/{id}"; CASE="agent detail" ;;
    3) METHOD=GET; PATH="/agents/{id}/status"; CASE="agent readiness check" ;;
    4) METHOD=GET; PATH="/agent-typologies"; CASE="agent typology dropdown" ;;
    5) METHOD=GET; PATH="/agents/recent"; CASE="dashboard recent agents widget" ;;
    6) METHOD=POST; PATH="/agents/{id}/invoke"; CASE="legacy chat invocation" ;;
    7) METHOD=POST; PATH="/agents"; CASE="admin create agent" ;;
    8) METHOD=PUT; PATH="/agents/{id}/configure"; CASE="admin configure strategy" ;;
    9) METHOD=POST; PATH="/agents/user"; CASE="user-create custom agent" ;;
    10) METHOD=POST; PATH="/agents/create-with-setup"; CASE="agent creation with KB setup wizard" ;;
    11) METHOD=PUT; PATH="/agents/{id}/user"; CASE="edit user agent" ;;
    12) METHOD=GET; PATH="/agents/{id}/configuration"; CASE="agent model config view" ;;
    13) METHOD=PATCH; PATH="/agents/{id}/configuration"; CASE="agent model config update" ;;
    14) METHOD=POST; PATH="/agents/quick-create"; CASE="quick agent onboarding" ;;
  esac
  gh issue create \
    --title "[BACKEND MISSING] $METHOD /api/v1$PATH — $CASE" \
    --label "bug,P1,backend-missing-2026-04-15" \
    --body "Audit 2026-05-03: vedi \`docs/audit/agents-backend-audit-2026-05-03.md\` row #$i.

Frontend consumer line: \`apps/web/src/lib/api/clients/agentsClient.ts\` cerca \`@todo BACKEND MISSING\` per riga.

DoD: backend route registered, integration test, frontend annotation removed, PR target main-dev."
done
```

Expected: 13 nuove issues create con label `backend-missing-2026-04-15`.

### Task 3.4 — Push + PR

- [ ] **Step 1: Push branch + create PR**

```bash
git push -u origin docs/agents-backend-audit-2026-05-03
gh pr create --base main-dev --title "docs(audit): Agents backend endpoint audit 2026-05-03 + 13 followup issues" --body "$(cat <<'EOF'
## Summary
- Comprehensive audit of 14 BACKEND MISSING annotations in agentsClient.ts
- Resolution status table linking each to followup issues
- Option A/B/C strategy recommendations for each gap

## Companion issues
- #XXX (Wave B.2 hotfix — resolves row #1)
- 13 issues filed by this PR with label `backend-missing-2026-04-15`

## Test plan
- [x] Markdown rendering verified locally
- [x] All 13 followup issues created via gh CLI batch
EOF
)"
gh pr checks --watch
gh pr merge --squash --delete-branch
```

---

## Phase 4 — CI gate `BACKEND MISSING` (Action #5)

**Branch**: `feature/ci-backend-missing-gate` (parent: `main-dev`)
**PR target**: `main-dev`
**Effort**: ~2h
**Dependency**: P-1

### Task 4.1 — Setup workflow file

**Files:**
- Create: `.github/workflows/backend-missing-gate.yml`

- [ ] **Step 1: Branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/ci-backend-missing-gate
git config branch.feature/ci-backend-missing-gate.parent main-dev
```

- [ ] **Step 2: Crea workflow GitHub Actions**

```yaml
name: Backend Missing Gate

on:
  pull_request:
    branches: [main, main-dev, frontend-dev]
    paths:
      - 'apps/web/src/**/*.ts'
      - 'apps/web/src/**/*.tsx'

permissions:
  contents: read
  pull-requests: write

jobs:
  detect-backend-missing:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout PR
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          ref: ${{ github.event.pull_request.head.sha }}

      - name: Determine changed files
        id: changes
        run: |
          git fetch origin ${{ github.event.pull_request.base.ref }} --depth=1
          changed=$(git diff --name-only --diff-filter=AM origin/${{ github.event.pull_request.base.ref }}...HEAD -- 'apps/web/src/**/*.ts' 'apps/web/src/**/*.tsx' || true)
          echo "files<<EOF" >> $GITHUB_OUTPUT
          echo "$changed" >> $GITHUB_OUTPUT
          echo "EOF" >> $GITHUB_OUTPUT

      - name: Scan for new BACKEND MISSING annotations
        id: scan
        run: |
          set +e
          NEW_HITS=""
          while IFS= read -r f; do
            [ -z "$f" ] && continue
            [ ! -f "$f" ] && continue
            HITS=$(grep -nE 'BACKEND MISSING' "$f" || true)
            if [ -n "$HITS" ]; then
              # Check if this is a NEW occurrence (not in base)
              BASE_HITS=$(git show "origin/${{ github.event.pull_request.base.ref }}:$f" 2>/dev/null | grep -cE 'BACKEND MISSING' || echo 0)
              CURRENT_HITS=$(echo "$HITS" | wc -l)
              if [ "$CURRENT_HITS" -gt "$BASE_HITS" ]; then
                NEW_HITS+="$f:\n$HITS\n\n"
              fi
            fi
          done <<< "${{ steps.changes.outputs.files }}"

          if [ -n "$NEW_HITS" ]; then
            echo "found=true" >> $GITHUB_OUTPUT
            {
              echo "hits<<HITSEOF"
              echo -e "$NEW_HITS"
              echo "HITSEOF"
            } >> $GITHUB_OUTPUT
          else
            echo "found=false" >> $GITHUB_OUTPUT
          fi

      - name: Comment on PR if violations found
        if: steps.scan.outputs.found == 'true'
        uses: actions/github-script@v7
        with:
          script: |
            const hits = `${{ steps.scan.outputs.hits }}`;
            const body = [
              '## ⚠️ Backend Missing Gate — violation detected',
              '',
              'This PR introduces NEW `BACKEND MISSING` annotations:',
              '',
              '```',
              hits,
              '```',
              '',
              '**Action required**: either',
              '- Implement the backend route before merging, OR',
              '- File a tracking issue and link it in the JSDoc with `Tracked: #N` instead of `BACKEND MISSING`',
              '',
              'Refs: `docs/audit/agents-backend-audit-2026-05-03.md` for context on existing 14 missing endpoints.',
            ].join('\n');
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body
            });

      - name: Fail if new violations
        if: steps.scan.outputs.found == 'true'
        run: |
          echo "❌ New BACKEND MISSING annotations detected. See PR comment."
          exit 1

      - name: Pass
        if: steps.scan.outputs.found == 'false'
        run: echo "✅ No new BACKEND MISSING annotations introduced."
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/backend-missing-gate.yml
git commit -m "ci(gate): block PRs that introduce new BACKEND MISSING annotations

Scans changed .ts/.tsx files in apps/web/src/ for new @todo BACKEND MISSING
JSDoc comments. Comments on PR + fails if found.

Pre-existing annotations are NOT affected — only NEW ones in this PR's diff.

Resolves audit #5 from spec-panel critique 2026-05-03."
```

### Task 4.2 — Test workflow su PR fittizia

- [ ] **Step 1: Push + create draft PR for self-test**

```bash
git push -u origin feature/ci-backend-missing-gate
gh pr create --base main-dev --draft --title "ci(gate): backend-missing-gate workflow [draft for self-test]" --body "Testing workflow — will undraft after self-test passes."
```

- [ ] **Step 2: Add throwaway test commit with NEW BACKEND MISSING**

```bash
# Crea file fittizio con nuova annotazione
cat > apps/web/src/lib/test-throwaway.ts <<'EOF'
/**
 * @todo BACKEND MISSING: test gate self-validation. DELETE BEFORE MERGE.
 */
export const TEST = true;
EOF
git add apps/web/src/lib/test-throwaway.ts
git commit -m "test(gate): introduce throwaway BACKEND MISSING for gate self-test"
git push
```

- [ ] **Step 3: Watch workflow detection**

```bash
gh pr checks --watch
```

Expected: `Backend Missing Gate` job FAIL con comment su PR che riporta il file.

- [ ] **Step 4: Remove throwaway + verify gate passes**

```bash
git rm apps/web/src/lib/test-throwaway.ts
git commit -m "test(gate): remove throwaway after self-test validated detection"
git push
gh pr checks --watch
```

Expected: gate PASS this time.

- [ ] **Step 5: Undraft + merge**

```bash
gh pr ready
gh pr checks --watch
gh pr merge --squash --delete-branch
```

---

## Phase 5 — E2E smoke test real backend (Action #4)

**Branch**: `feature/e2e-smoke-real-backend` (parent: `main-dev`)
**PR target**: `main-dev`
**Effort**: ~6-8h
**Dependency**: Phase 1 merged (per testare path success `/agents`)

### Task 5.1 — Setup smoke test infra

**Files:**
- Create: `apps/web/e2e/smoke-real-backend/_helpers/auth.ts`
- Create: `apps/web/e2e/smoke-real-backend/agents.smoke.spec.ts`
- Create: `apps/web/e2e/smoke-real-backend/library.smoke.spec.ts`
- Create: `apps/web/e2e/smoke-real-backend/games.smoke.spec.ts`
- Create: `apps/web/e2e/smoke-real-backend/shared-game-detail.smoke.spec.ts`
- Create: `apps/web/e2e/smoke-real-backend/invites.smoke.spec.ts`
- Create: `.github/workflows/e2e-smoke-real-backend.yml`

- [ ] **Step 1: Branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/e2e-smoke-real-backend
git config branch.feature/e2e-smoke-real-backend.parent main-dev
```

### Task 5.2 — Helper auth real backend

- [ ] **Step 1: Crea helper `_helpers/auth.ts`**

```typescript
/**
 * Real-backend auth helper for smoke tests.
 *
 * Differs from visual-test fixtures: makes a REAL POST /api/v1/auth/login
 * against backend at :8080 with seeded credentials.
 *
 * Used by smoke-real-backend specs to detect endpoint binding regressions
 * (404, schema mismatch) that visual-test fixtures hide.
 */
import { type Page, type APIRequestContext } from '@playwright/test';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';
const SEED_EMAIL = process.env.SMOKE_USER_EMAIL ?? 'smoke-user@meepleai.test';
const SEED_PASSWORD = process.env.SMOKE_USER_PASSWORD ?? 'SmokeUser1!';

export async function smokeLogin(request: APIRequestContext): Promise<{
  cookieHeader: string;
}> {
  const res = await request.post(`${API_BASE}/api/v1/auth/login`, {
    data: { email: SEED_EMAIL, password: SEED_PASSWORD },
  });
  if (!res.ok()) {
    throw new Error(`smokeLogin failed ${res.status()} for ${SEED_EMAIL}`);
  }
  const cookies = res.headersArray().filter(h => h.name.toLowerCase() === 'set-cookie');
  const session = cookies.find(c => c.value.startsWith('meepleai_session='));
  if (!session) throw new Error('No meepleai_session cookie returned');
  return { cookieHeader: session.value.split(';')[0] };
}

export async function applySessionToPage(page: Page, cookieHeader: string): Promise<void> {
  const [name, value] = cookieHeader.split('=');
  await page.context().addCookies([
    {
      name,
      value,
      url: API_BASE,
      httpOnly: true,
      secure: false,
      sameSite: 'Lax',
    },
  ]);
}
```

- [ ] **Step 2: Commit helper**

```bash
git add apps/web/e2e/smoke-real-backend/_helpers/auth.ts
git commit -m "test(e2e): smoke real-backend auth helper

Makes POST /api/v1/auth/login against :8080 with seeded credentials.
Distinct from visual-test fixtures: real round-trip per detection of
endpoint binding regressions."
```

### Task 5.3 — Spec smoke test `/agents`

**Files:**
- Create: `apps/web/e2e/smoke-real-backend/agents.smoke.spec.ts`

- [ ] **Step 1: Scrivi spec**

```typescript
import { test, expect } from '@playwright/test';
import { smokeLogin, applySessionToPage } from './_helpers/auth';

test.describe('SMOKE — /agents real backend round-trip', () => {
  test('GET /api/v1/agents returns success+agents+count shape', async ({ request }) => {
    const { cookieHeader } = await smokeLogin(request);
    const res = await request.get('http://localhost:8080/api/v1/agents', {
      headers: { Cookie: cookieHeader },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toMatchObject({
      success: true,
      agents: expect.any(Array),
      count: expect.any(Number),
    });
  });

  test('frontend /agents renders without entering kind="error"', async ({ page, request }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/agents', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="agents-library-view"]', { timeout: 30_000 });
    // Either default state (results-grid) or empty state (no agents seeded) — but NEVER error.
    const errorSurface = await page.locator('[data-slot="agents-empty-state"][data-kind="error"]').count();
    expect(errorSurface).toBe(0);
  });
});
```

- [ ] **Step 2: Verifica spec contro dev backend (manual run)**

```bash
# Start dev stack (separate terminal)
cd infra && make dev-core
sleep 30

# Seed smoke user (one-time per dev DB)
cd ../apps/api && dotnet run --project tests/Api.Tests/Tools/SeedSmokeUser -- --email smoke-user@meepleai.test --password SmokeUser1!
# OR via curl-based registration if available

# Run spec
cd ../../web && pnpm playwright test smoke-real-backend/agents.smoke.spec.ts --project=chromium-desktop
```

Expected: 2 tests PASS (post-Phase-1 merge).

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/smoke-real-backend/agents.smoke.spec.ts
git commit -m "test(e2e): smoke real backend /agents (Wave B.2 hotfix coverage)

2 tests: API round-trip shape + frontend NEVER kind='error'.
Detects regressions that visual-test fixtures hide."
```

### Task 5.4 — Spec smoke test per le altre 4 routes v2

- [ ] **Step 1: `library.smoke.spec.ts`**

```typescript
import { test, expect } from '@playwright/test';
import { smokeLogin, applySessionToPage } from './_helpers/auth';

test.describe('SMOKE — /library real backend round-trip', () => {
  test('GET /api/v1/library returns paginated shape', async ({ request }) => {
    const { cookieHeader } = await smokeLogin(request);
    const res = await request.get('http://localhost:8080/api/v1/library?page=1&pageSize=20', {
      headers: { Cookie: cookieHeader },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toMatchObject({
      items: expect.any(Array),
      page: 1,
      pageSize: 20,
      totalCount: expect.any(Number),
      totalPages: expect.any(Number),
      hasNextPage: expect.any(Boolean),
      hasPreviousPage: expect.any(Boolean),
    });
  });

  test('frontend /library renders without kind="error"', async ({ page, request }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/library', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
    const errorState = await page.locator('[data-state="error"]').count();
    expect(errorState).toBe(0);
  });
});
```

- [ ] **Step 2: `games.smoke.spec.ts`** (mirror /games?tab=library)

```typescript
import { test, expect } from '@playwright/test';
import { smokeLogin, applySessionToPage } from './_helpers/auth';

test.describe('SMOKE — /games?tab=library real backend round-trip', () => {
  test('frontend /games?tab=library renders without kind="error"', async ({ page, request }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/games?tab=library', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="games-library-view"]', { timeout: 30_000 });
    const errorState = await page.locator('[data-slot="games-empty-state"][data-kind="error"]').count();
    expect(errorState).toBe(0);
  });
});
```

- [ ] **Step 3: `shared-game-detail.smoke.spec.ts`** (Wave A.4)

```typescript
import { test, expect } from '@playwright/test';

test.describe('SMOKE — /shared-games/[id] real backend (public, no auth)', () => {
  test('GET /api/v1/shared-games/top-contributors returns array', async ({ request }) => {
    const res = await request.get('http://localhost:8080/api/v1/shared-games/top-contributors?limit=8');
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  test('frontend /shared-games/{seeded-id} renders hero (or notFound if no seed)', async ({ page, request }) => {
    // Seed-aware: requires a seeded shared game in dev DB
    const SEEDED_ID = process.env.SMOKE_SHARED_GAME_ID ?? '';
    test.skip(!SEEDED_ID, 'SMOKE_SHARED_GAME_ID not seeded');
    await page.goto(`/shared-games/${SEEDED_ID}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="shared-game-hero"], [data-slot="shared-game-not-found"]', {
      timeout: 30_000,
    });
  });
});
```

- [ ] **Step 4: `invites.smoke.spec.ts`** (Wave A.5b — token error path coverage)

```typescript
import { test, expect } from '@playwright/test';

test.describe('SMOKE — /invites/[token] real backend (public, no auth)', () => {
  test('GET /api/v1/game-nights/invitations/{invalid-token} returns 404', async ({ request }) => {
    const res = await request.get(
      'http://localhost:8080/api/v1/game-nights/invitations/invalid-token-12345'
    );
    expect(res.status()).toBe(404);
  });

  test('frontend /invites/invalid-token-12345 renders not-found shell', async ({ page }) => {
    await page.goto('/invites/invalid-token-12345', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="invite-not-found"], [data-slot="invite-expired"]', {
      timeout: 30_000,
    });
  });
});
```

- [ ] **Step 5: Verifica suite local**

```bash
cd apps/web && pnpm playwright test smoke-real-backend/ --project=chromium-desktop
```

Expected: 7 tests, tutti PASS (assuming dev stack up + seed user creato).

- [ ] **Step 6: Commit**

```bash
git add apps/web/e2e/smoke-real-backend/
git commit -m "test(e2e): smoke real backend specs for 5 v2 routes

Routes covered: /agents, /library, /games?tab=library,
/shared-games/[id], /invites/[token].

Each spec asserts:
1. Backend route returns expected shape (no 404)
2. Frontend renders without entering error state

Detects endpoint binding regressions that visual-test fixtures hide."
```

### Task 5.5 — CI workflow E2E smoke nightly

- [ ] **Step 1: Workflow file**

```yaml
name: E2E Smoke Real Backend (nightly)

on:
  schedule:
    # 03:00 UTC daily
    - cron: '0 3 * * *'
  workflow_dispatch:

permissions:
  contents: read

jobs:
  smoke:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Boot dev stack (postgres + redis + api)
        run: |
          cd infra && make dev-core
          # Wait for API healthy
          for i in {1..30}; do
            if curl -fs http://localhost:8080/health 2>/dev/null; then break; fi
            sleep 5
          done

      - name: Seed smoke user
        run: |
          cd apps/api && dotnet run --project tests/Api.Tests/Tools/SeedSmokeUser -- \
            --email smoke-user@meepleai.test \
            --password SmokeUser1!

      - name: Setup pnpm + Node
        uses: pnpm/action-setup@v3
        with:
          version: 9
      - uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: pnpm
          cache-dependency-path: apps/web/pnpm-lock.yaml

      - name: Install web deps
        run: cd apps/web && pnpm install --frozen-lockfile

      - name: Install Playwright browsers
        run: cd apps/web && pnpm playwright install --with-deps chromium

      - name: Build web
        run: cd apps/web && pnpm build

      - name: Run smoke specs
        env:
          SMOKE_API_BASE: http://localhost:8080
          SMOKE_USER_EMAIL: smoke-user@meepleai.test
          SMOKE_USER_PASSWORD: SmokeUser1!
        run: cd apps/web && pnpm playwright test smoke-real-backend/ --project=chromium-desktop

      - name: Upload report on failure
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report-smoke
          path: apps/web/playwright-report
          retention-days: 7

      - name: Open issue on failure
        if: failure()
        uses: actions/github-script@v7
        with:
          script: |
            github.rest.issues.create({
              owner: context.repo.owner,
              repo: context.repo.repo,
              title: `[SMOKE FAILURE] Nightly E2E smoke real backend — ${new Date().toISOString().slice(0,10)}`,
              labels: ['bug', 'P0', 'smoke-failure'],
              body: `Nightly smoke run failed. See [run](${context.serverUrl}/${context.repo.owner}/${context.repo.repo}/actions/runs/${context.runId}).\n\nCheck attached \`playwright-report-smoke\` artifact.`
            });
```

- [ ] **Step 2: Commit + push + PR**

```bash
git add .github/workflows/e2e-smoke-real-backend.yml
git commit -m "ci(smoke): nightly E2E smoke real backend (5 v2 routes)

Schedule: 03:00 UTC daily + workflow_dispatch.
Steps: boot dev stack, seed user, run Playwright smoke specs.
On failure: opens P0 issue with smoke-failure label."

git push -u origin feature/e2e-smoke-real-backend
gh pr create --base main-dev --title "test+ci(smoke): E2E smoke real backend for 5 v2 routes + nightly workflow" --body "Resolves audit #4 from spec-panel critique 2026-05-03."
gh pr checks --watch
gh pr merge --squash --delete-branch
```

---

## Phase 6 — AgentDto.GameName drift fix (Action #7)

**Branch**: `feature/agent-dto-gamename-drift` (parent: `main-dev`)
**PR target**: `main-dev`
**Effort**: ~1h
**Dependency**: Phase 1 merged

### Task 6.1 — Backend: aggiungi GameName a AgentDto + popolalo

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/AgentDto.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryHandler.cs`

- [ ] **Step 1: Branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/agent-dto-gamename-drift
git config branch.feature/agent-dto-gamename-drift.parent main-dev
```

- [ ] **Step 2: Aggiungi GameName a AgentDto record**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/AgentDto.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

internal record AgentDto(
    Guid Id,
    string Name,
    string Type,
    string StrategyName,
    IReadOnlyDictionary<string, object> StrategyParameters,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastInvokedAt,
    int InvocationCount,
    bool IsRecentlyUsed,
    bool IsIdle,
    Guid? GameId = null,
    string? GameName = null,           // NEW: Issue #XXX drift fix — was simulated in fixture but missing in DTO
    Guid? CreatedByUserId = null
);
```

- [ ] **Step 3: Update handler MapToDto popola GameName**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryHandler.cs:48-67`:

Aggiungi dependency su `ISharedGameRepository` per name lookup:

```csharp
internal sealed class GetAllAgentsQueryHandler
    : IRequestHandler<GetAllAgentsQuery, List<AgentDto>>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public GetAllAgentsQueryHandler(
        IAgentDefinitionRepository repository,
        ISharedGameRepository sharedGameRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    public async Task<List<AgentDto>> Handle(
        GetAllAgentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agents = request.ActiveOnly == true
            ? await _repository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false)
            : await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            agents = agents
                .Where(a => string.Equals(a.Type.Value, request.Type, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (request.GameId.HasValue)
        {
            agents = agents.Where(a => a.GameId == request.GameId.Value).ToList();
        }

        // Issue #XXX: bulk-fetch game names per drift fix
        var gameIds = agents.Where(a => a.GameId.HasValue).Select(a => a.GameId!.Value).Distinct().ToList();
        var gameNamesByID = gameIds.Any()
            ? await _sharedGameRepository.GetNamesByIdsAsync(gameIds, cancellationToken).ConfigureAwait(false)
            : new Dictionary<Guid, string>();

        return agents.Select(a => MapToDto(a, gameNamesByID)).ToList();
    }

    private static AgentDto MapToDto(
        Domain.Entities.AgentDefinition agent,
        IReadOnlyDictionary<Guid, string> gameNamesByID)
    {
        var recentThreshold = DateTime.UtcNow.AddHours(-24);
        var idleThreshold = DateTime.UtcNow.AddDays(-7);

        return new AgentDto(
            Id: agent.Id,
            Name: agent.Name,
            Type: agent.Type.Value,
            StrategyName: agent.Strategy.Name,
            StrategyParameters: agent.Strategy.Parameters,
            IsActive: agent.IsActive,
            CreatedAt: agent.CreatedAt,
            LastInvokedAt: agent.LastInvokedAt,
            InvocationCount: agent.InvocationCount,
            IsRecentlyUsed: agent.LastInvokedAt.HasValue && agent.LastInvokedAt.Value > recentThreshold,
            IsIdle: !agent.LastInvokedAt.HasValue || agent.LastInvokedAt.Value < idleThreshold,
            GameId: agent.GameId,
            GameName: agent.GameId.HasValue && gameNamesByID.TryGetValue(agent.GameId.Value, out var name) ? name : null,
            CreatedByUserId: null
        );
    }
}
```

- [ ] **Step 4: Aggiungi GetNamesByIdsAsync a ISharedGameRepository**

Verifica se esiste, altrimenti add:

```bash
grep -n "GetNamesByIdsAsync\|GetByIdsAsync" apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/ISharedGameRepository.cs
```

Se non esiste, aggiungi metodo + implementation:

```csharp
// In ISharedGameRepository
Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
    IReadOnlyCollection<Guid> ids,
    CancellationToken cancellationToken);

// In SharedGameRepository implementation:
public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
    IReadOnlyCollection<Guid> ids,
    CancellationToken cancellationToken)
{
    if (ids.Count == 0) return new Dictionary<Guid, string>();
    return await _dbContext.SharedGames
        .Where(g => ids.Contains(g.Id))
        .Select(g => new { g.Id, g.Title })
        .ToDictionaryAsync(g => g.Id, g => g.Title, cancellationToken)
        .ConfigureAwait(false);
}
```

- [ ] **Step 5: Update integration tests Phase 1.2 con assertion GameName**

In `apps/api/tests/Api.Tests/Integration/Agents/AgentsEndpointsIntegrationTests.cs` aggiungi:

```csharp
[Fact]
public async Task GetAgents_WithSeededAgentLinkedToGame_PopulatesGameName()
{
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
    _client.DefaultRequestHeaders.Add("Cookie", $"meepleai_session={sessionToken}");

    var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan");
    await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 1, inactiveCount: 0, gameId: gameId);

    var response = await _client.GetAsync("/api/v1/agents");
    var body = await response.Content.ReadFromJsonAsync<GetAllAgentsResponse>();

    body!.Agents.Should().HaveCount(1);
    body.Agents[0].GameId.Should().Be(gameId);
    body.Agents[0].GameName.Should().Be("Catan");
}
```

- [ ] **Step 6: Run tests**

```bash
cd apps/api && dotnet build && dotnet test --filter "FullyQualifiedName~AgentsEndpointsIntegrationTests"
```

Expected: 4 tests PASS (3 originals + GameName).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/AgentDto.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryHandler.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/ISharedGameRepository.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/SharedGameRepository.cs \
        apps/api/tests/Api.Tests/Integration/Agents/AgentsEndpointsIntegrationTests.cs
git commit -m "fix(agents): populate AgentDto.GameName from SharedGame catalog (drift fix)

Resolves drift where Wave B.2 visual-test fixture simulated gameName='Catan'
but backend AgentDto record lacked the field entirely.

Adds:
- GameName property on AgentDto record
- ISharedGameRepository.GetNamesByIdsAsync for bulk lookup
- Handler bulk-fetches names + maps per agent.GameId
- Integration test asserts GameName populated when agent linked to game

Refs spec-panel critique 2026-05-03 finding #4."
```

### Task 6.2 — Push + PR

- [ ] **Step 1: Push + create PR**

```bash
git push -u origin feature/agent-dto-gamename-drift
gh pr create --base main-dev --title "fix(agents): populate AgentDto.GameName from SharedGame catalog" --body "Resolves drift fix #7 from spec-panel critique 2026-05-03."
gh pr checks --watch
gh pr merge --squash --delete-branch
```

---

## Phase 7 — RecentActivityRail backend (Action #6 implementation)

**Issue**: tracked in Phase 2.2 `<ACTIVITY_ISSUE_NUMBER>`
**Branch**: `feature/issue-YYY-library-activity-endpoint`
**PR target**: `main-dev`
**Effort**: ~3h
**Dependency**: P-1, P-2

### Task 7.1 — Backend: query + handler + endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/LibraryActivityItemDto.cs`
- Modify: `apps/api/src/Api/Routing/UserLibrary/UserLibraryCoreEndpoints.cs`

- [ ] **Step 1: Branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-YYY-library-activity-endpoint
git config branch.feature/issue-YYY-library-activity-endpoint.parent main-dev
```

- [ ] **Step 2: Crea DTO**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/LibraryActivityItemDto.cs
namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// Activity feed item per RecentActivityRail (Wave B.3 followup).
/// Aggregates from domain events: Added, Removed, StateChanged, SessionRecorded.
/// </summary>
internal record LibraryActivityItemDto(
    Guid Id,
    string Type,            // "added" | "removed" | "state-changed" | "session-recorded"
    DateTime Timestamp,
    Guid GameId,
    string GameTitle,
    string Message
);
```

- [ ] **Step 3: Crea query**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQuery.cs
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

internal record GetLibraryActivityQuery(
    Guid UserId,
    int Limit = 20
) : IQuery<IReadOnlyList<LibraryActivityItemDto>>;
```

- [ ] **Step 4: Crea handler (query event store via DbContext)**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQueryHandler.cs
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

internal class GetLibraryActivityQueryHandler
    : IQueryHandler<GetLibraryActivityQuery, IReadOnlyList<LibraryActivityItemDto>>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetLibraryActivityQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<LibraryActivityItemDto>> Handle(
        GetLibraryActivityQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Approach: union 4 event types from DomainEventLog (or eq. event store table)
        // ordered by Timestamp desc, take Limit.
        // Assumes: domain events already persisted via EFCore SaveChanges interceptor.
        var events = await _dbContext.DomainEventLog
            .AsNoTracking()
            .Where(e => e.UserId == query.UserId)
            .Where(e => new[] { "GameAddedToLibraryEvent", "GameRemovedFromLibraryEvent",
                                "GameStateChangedEvent", "GameSessionRecordedEvent" }.Contains(e.EventType))
            .OrderByDescending(e => e.Timestamp)
            .Take(query.Limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return events.Select(e => new LibraryActivityItemDto(
            Id: e.Id,
            Type: MapEventTypeToActivityType(e.EventType),
            Timestamp: e.Timestamp,
            GameId: e.AggregateId,
            GameTitle: e.AggregateLabel ?? "Unknown game",
            Message: BuildMessage(e.EventType, e.AggregateLabel)
        )).ToList();
    }

    private static string MapEventTypeToActivityType(string eventType) => eventType switch
    {
        "GameAddedToLibraryEvent" => "added",
        "GameRemovedFromLibraryEvent" => "removed",
        "GameStateChangedEvent" => "state-changed",
        "GameSessionRecordedEvent" => "session-recorded",
        _ => "unknown"
    };

    private static string BuildMessage(string eventType, string? gameTitle)
    {
        var title = gameTitle ?? "(unknown)";
        return eventType switch
        {
            "GameAddedToLibraryEvent" => $"Aggiunto {title} alla libreria",
            "GameRemovedFromLibraryEvent" => $"Rimosso {title} dalla libreria",
            "GameStateChangedEvent" => $"Aggiornato lo stato di {title}",
            "GameSessionRecordedEvent" => $"Registrata una sessione di {title}",
            _ => title
        };
    }
}
```

> **NOTE**: se `DomainEventLog` non esiste come tabella, è necessario un design alternativo (es. proiezione da AggregateRoot o tabella ad-hoc). Verificare prima:
>
> ```bash
> grep -rn "DomainEventLog\|class DomainEvent" apps/api/src/Api/Infrastructure/ | head
> ```
>
> Se assente, aprire issue separata "Library activity endpoint requires event log infra" e parking di Phase 7.

- [ ] **Step 5: Endpoint registration**

In `apps/api/src/Api/Routing/UserLibrary/UserLibraryCoreEndpoints.cs:67` aggiungi alla `Map`:

```csharp
        // Library activity feed (Issue #YYY — Wave B.3 followup)
        MapGetLibraryActivityEndpoint(group);
```

E aggiungi il metodo statico:

```csharp
    private static void MapGetLibraryActivityEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/activity", async (
            [FromQuery] int? limit,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            var query = new GetLibraryActivityQuery(userId, Limit: Math.Clamp(limit ?? 20, 1, 50));
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<LibraryActivityItemDto>>(200)
        .WithTags("Library")
        .WithSummary("Get library activity feed")
        .WithDescription("Returns recent activity events (added/removed/state-changed/session) for the user.")
        .WithOpenApi();
    }
```

- [ ] **Step 6: Integration test**

```csharp
// In AgentsEndpointsIntegrationTests... actually new file:
// apps/api/tests/Api.Tests/Integration/UserLibrary/LibraryActivityEndpointTests.cs

[Fact]
public async Task GetLibraryActivity_WithSeededEvents_ReturnsLatestFirst()
{
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
    _client.DefaultRequestHeaders.Add("Cookie", $"meepleai_session={sessionToken}");
    await TestSessionHelper.SeedDomainEventLogAsync(dbContext, userId, count: 3);

    var response = await _client.GetAsync("/api/v1/library/activity?limit=10");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<List<LibraryActivityItemDto>>();
    body.Should().HaveCount(3);
    body.Should().BeInDescendingOrder(e => e.Timestamp);
}
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQuery.cs \
        apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQueryHandler.cs \
        apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/LibraryActivityItemDto.cs \
        apps/api/src/Api/Routing/UserLibrary/UserLibraryCoreEndpoints.cs \
        apps/api/tests/Api.Tests/Integration/UserLibrary/LibraryActivityEndpointTests.cs
git commit -m "feat(library): GET /api/v1/library/activity endpoint (Wave B.3 followup)

Aggregates 4 domain events (added/removed/state-changed/session-recorded)
into a chronological feed, limit 1-50, default 20.

Refs #YYY"
```

### Task 7.2 — Frontend: hook + LibraryHubV2 wireup

**Files:**
- Create: `apps/web/src/lib/api/schemas/library-activity.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/libraryClient.ts`
- Modify: `apps/web/src/hooks/queries/useLibrary.ts`
- Modify: `apps/web/src/app/(authenticated)/library/_components/LibraryHubV2.tsx:33-37,427`

- [ ] **Step 1: Crea schema**

```typescript
// apps/web/src/lib/api/schemas/library-activity.schemas.ts
import { z } from 'zod';

export const LibraryActivityItemSchema = z.object({
  id: z.string().uuid(),
  type: z.enum(['added', 'removed', 'state-changed', 'session-recorded', 'unknown']),
  timestamp: z.string().datetime({ offset: true }),
  gameId: z.string().uuid(),
  gameTitle: z.string(),
  message: z.string(),
});

export type LibraryActivityItem = z.infer<typeof LibraryActivityItemSchema>;

export const LibraryActivityResponseSchema = z.array(LibraryActivityItemSchema);
```

- [ ] **Step 2: Aggiungi metodo a libraryClient**

```typescript
// apps/web/src/lib/api/clients/libraryClient.ts (extend existing)
async getActivity(limit: number = 20): Promise<LibraryActivityItem[]> {
  const response = await httpClient.get<LibraryActivityItem[]>(
    `/api/v1/library/activity?limit=${limit}`,
    LibraryActivityResponseSchema
  );
  return response ?? [];
}
```

- [ ] **Step 3: Aggiungi hook**

```typescript
// apps/web/src/hooks/queries/useLibrary.ts (extend)
export function useLibraryActivity(
  limit: number = 20,
  enabled: boolean = true
): UseQueryResult<LibraryActivityItem[], Error> {
  return useQuery({
    queryKey: [...libraryKeys.all, 'activity', { limit }],
    queryFn: () => api.library.getActivity(limit),
    enabled,
    staleTime: 1 * 60 * 1000, // 1min — activity changes often
  });
}
```

- [ ] **Step 4: Wireup LibraryHubV2**

In `apps/web/src/app/(authenticated)/library/_components/LibraryHubV2.tsx`:

Sostituisci docstring `:33-37` (rimuovi caveat) e riga 427:

```tsx
// Importazione (in cima)
import { useLibrary, useLibraryActivity, useRemoveGameFromLibrary } from '@/hooks/queries/useLibrary';

// Dentro il componente, dopo libraryQuery:
const activityQuery = useLibraryActivity(20);

// Sostituisci riga 427:
<RecentActivityRail items={activityQuery.data ?? []} isLoading={activityQuery.isLoading} />
```

Aggiorna anche docstring header rimuovendo `RecentActivityRail Phase 1 contract (AC-7)` paragrafo (righe 33-37).

- [ ] **Step 5: Run frontend tests**

```bash
cd apps/web && pnpm typecheck && pnpm test -- LibraryHubV2 RecentActivityRail
```

- [ ] **Step 6: Commit + push + PR**

```bash
git add apps/web/src/lib/api/schemas/library-activity.schemas.ts \
        apps/web/src/lib/api/clients/libraryClient.ts \
        apps/web/src/hooks/queries/useLibrary.ts \
        apps/web/src/app/(authenticated)/library/_components/LibraryHubV2.tsx
git commit -m "feat(library): wire RecentActivityRail to GET /library/activity

Removes hardcoded items={[]} placeholder. Hook useLibraryActivity fetches
recent events with 1min staleTime.

Closes #YYY"

git push -u origin feature/issue-YYY-library-activity-endpoint
gh pr create --base main-dev --title "feat(library): GET /library/activity endpoint + RecentActivityRail wiring (Wave B.3 followup)" --body "Closes #YYY"
gh pr checks --watch
gh pr merge --squash --delete-branch
```

---

## 8. Self-review checklist

Before marking the plan complete, verify:

**Spec coverage** (each of 7 actions has an implementing Phase):
- [x] Action #1 (P0 backend hotfix `/agents`) → Phase 1
- [x] Action #2 (P0 issue tracking) → Phase 2.1
- [x] Action #3 (P1 audit doc 14 endpoints) → Phase 3
- [x] Action #4 (P1 E2E smoke real backend) → Phase 5
- [x] Action #5 (P2 CI gate BACKEND MISSING) → Phase 4
- [x] Action #6 (P2 issue + impl `/library/activity`) → Phase 2.2 (issue) + Phase 7 (impl)
- [x] Action #7 (P3 AgentDto.GameName drift) → Phase 6

**Placeholder scan**: nessun TBD/TODO/"implement later" usato.

**Type consistency**: `GetAllAgentsResponse` shape coerente tra Phase 1 (`{ success, agents, count }`) e schema Zod frontend `GetAllAgentsResponseSchema` esistente.

**Branch strategy**: tutti i feature branches da `main-dev` parent, PR target `main-dev` (CLAUDE.md rule).

**Issue lifecycle**: ogni Phase ha link `Closes #N` o `Refs #N` nel commit + PR body per auto-closure GitHub.

**Dependency graph**:
```
P-1 → Phase 2 (parallel: 2.1 + 2.2)
P-1 → Phase 1 (needs Phase 2.1 issue) → Phase 5 (needs Phase 1 merged) → Phase 6 (needs Phase 1 merged)
P-1 → Phase 3 (needs Phase 2.1 issue created)
P-1 → Phase 4 (independent)
P-1 + Phase 2.2 issue → Phase 7
```

**Rollback strategy**: ogni Phase è atomica (PR singolo). Rollback = `git revert <merge-commit>` su main-dev. Nessuna migration database irreversibile.

---

## 9. Execution sequence (ordine ottimale)

```
Day 1 (~3h):
  - P-1, P-2 gates
  - Phase 2 (issue tracking) — 30min
  - Phase 1 (backend hotfix /agents) — 3h
    * BLOCKER until merged for Phase 5/6

Day 2 (~4h):
  - Phase 3 (audit doc + 13 followup issues) — 2h
  - Phase 4 (CI gate) — 2h

Day 3 (~6-8h):
  - Phase 5 (E2E smoke real backend) — 6-8h
    * Verifies Phase 1 fix end-to-end

Day 4 (~4h):
  - Phase 6 (AgentDto.GameName drift) — 1h
  - Phase 7 (RecentActivityRail backend) — 3h

Total: ~17-21h, 4 working days.
```

---

## 10. Memory updates after execution

Post-merge di tutte le 7 phases, aggiornare:

- `MEMORY.md` topic file pointers: rimuovere "🔴 P0 Wave B.2" (resolved), aggiungere session note di chiusura con squash hashes
- `project_wave-b2-agents-backend-missing.md`: aggiungere closing section "Resolution 2026-05-XX" con PR# di Phase 1
- `feedback_endpoint-binding-pre-merge.md`: aggiungere note "validated by CI gate Phase 4" e "validated by smoke specs Phase 5"

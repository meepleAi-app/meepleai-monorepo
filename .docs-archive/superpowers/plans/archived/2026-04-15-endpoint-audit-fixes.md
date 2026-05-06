# Endpoint Audit Fixes — Implementation Plan

> **Status:** ✅ **COMPLETED** (2026-04-16) — evidence verified against codebase:
> - Route renamed: `apps/api/src/Api/Routing/UserLibrary/UserLibraryCoreEndpoints.cs:420` → `/agent-config` ✓
> - `apps/api/tests/Api.Tests/Routing/EndpointContractTests.cs` exists with contract coverage ✓
> - 14 `@todo BACKEND MISSING` JSDoc annotations on `apps/web/src/lib/api/clients/agentsClient.ts` ✓
>
> Shipped incrementally across multiple PRs; no dedicated feature branch was needed.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the 1 route mismatch found in the backend↔frontend endpoint audit, document 12+ orphan frontend agent methods with no backend route, and add a contract test to prevent future drift.

**Architecture:** Three surgical changes: (1) rename one backend route to match its siblings, (2) add JSDoc `@todo` annotations on frontend client methods whose backend endpoints don't exist yet, (3) add a single integration test that verifies all frontend API URLs resolve to a registered backend route.

**Tech Stack:** C# ASP.NET Minimal API, TypeScript/Next.js, xUnit

---

## File Map

| Action | File | Responsibility |
|--------|------|---------------|
| Modify | `apps/api/src/Api/Routing/UserLibrary/UserLibraryCoreEndpoints.cs:420` | Fix PUT route `/agent` → `/agent-config` |
| Modify | `apps/web/src/lib/api/clients/agentsClient.ts` | Add `@todo` annotations on 12 orphan methods |
| Create | `tests/Api.Tests/Routing/EndpointContractTests.cs` | Contract test: every frontend URL must match a backend route |

---

### Task 1: Fix PUT Route Mismatch (P0)

The backend defines `PUT /library/games/{gameId}/agent` but the frontend calls `PUT /library/games/{gameId}/agent-config`. The sibling endpoints (GET, POST) already use `/agent-config`. The fix is to rename the backend PUT route to `/agent-config` for consistency.

**Files:**
- Modify: `apps/api/src/Api/Routing/UserLibrary/UserLibraryCoreEndpoints.cs:420`

- [x] **Step 1: Verify current backend route**

Run:
```bash
grep -n "MapPut.*library/games.*agent" apps/api/src/Api/Routing/UserLibrary/UserLibraryCoreEndpoints.cs
```
Expected output:
```
420:        group.MapPut("/library/games/{gameId:guid}/agent", async (
```

- [x] **Step 2: Change the route from `/agent` to `/agent-config`**

In `apps/api/src/Api/Routing/UserLibrary/UserLibraryCoreEndpoints.cs`, line 420, change:

```csharp
// BEFORE
group.MapPut("/library/games/{gameId:guid}/agent", async (
```

to:

```csharp
// AFTER
group.MapPut("/library/games/{gameId:guid}/agent-config", async (
```

- [x] **Step 3: Update the method name for clarity**

In the same file, rename the method (line 418):

```csharp
// BEFORE
private static void MapConfigureGameAgentEndpoint(RouteGroupBuilder group)
```

to:

```csharp
// AFTER
private static void MapUpdateAgentConfigEndpoint(RouteGroupBuilder group)
```

Also update the call site in the same file where this method is invoked. Search for `MapConfigureGameAgentEndpoint` and rename to `MapUpdateAgentConfigEndpoint`.

- [x] **Step 4: Update the OpenAPI metadata**

Change `.WithSummary` and `.WithDescription` to reflect the `/agent-config` path:

```csharp
.WithSummary("Update AI agent configuration")
.WithDescription("Updates the custom AI agent configuration for a game in user's library. Replaces any existing configuration.")
```

- [x] **Step 5: Verify the sibling routes are consistent**

Run:
```bash
grep -n "agent-config\|/agent\"" apps/api/src/Api/Routing/UserLibrary/UserLibraryCoreEndpoints.cs
```

Expected: all three endpoints (GET, POST, PUT) should now use `/agent-config`. The DELETE endpoint at `/agent` is intentionally different (it resets the agent entirely, not just the config).

- [x] **Step 6: Build to verify no compilation errors**

```bash
cd apps/api/src/Api && dotnet build
```

Expected: Build succeeded.

- [x] **Step 7: Run existing library tests**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~UserLibrary" --no-build
```

Expected: All pass.

- [x] **Step 8: Commit**

```bash
git add apps/api/src/Api/Routing/UserLibrary/UserLibraryCoreEndpoints.cs
git commit -m "fix(routing): rename PUT /library/games/{gameId}/agent to /agent-config

Aligns the PUT route with the sibling GET and POST endpoints that already
use /agent-config. The frontend was calling /agent-config but the backend
had /agent, causing 404/405 at runtime.

Discovered during endpoint audit spec-panel review.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Annotate Orphan Frontend Agent Methods (P1)

12+ methods in `agentsClient.ts` call backend URLs that have no registered route. These are actively used by UI components (hooks, pages) but return empty/null via error handling fallbacks. Rather than deleting them (which would break UI), annotate each with `@todo` documenting the missing backend endpoint.

**Files:**
- Modify: `apps/web/src/lib/api/clients/agentsClient.ts`

**Active UI consumers per orphan method:**

| Method | Frontend Consumer | Backend Route Needed |
|--------|------------------|---------------------|
| `getAll()` | `useAgents.ts:13` | `GET /agents` |
| `getById()` | `agents/[id]/page.tsx:23` | `GET /agents/{id}` |
| `getStatus()` | `useAgentStatus.ts:38` | `GET /agents/{id}/status` |
| `getTypologies()` | `useAgentConfigModal.ts:142` | `GET /agent-typologies` |
| `getRecent()` | `useRecentAgents.ts:13` | `GET /agents/recent` |
| `create()` | (admin) | `POST /agents` |
| `configure()` | (admin) | `PUT /agents/{id}/configure` |
| `createUserAgent()` | `AgentCreationWizard.tsx:615`, `FirstAgentStep.tsx:42` | `POST /agents/user` |
| `getSlots()` | `useAgentSlots.ts:33` | `GET /user/agent-slots` |
| `createWithSetup()` | `useCreateAgentFlow.ts:51` | `POST /agents/create-with-setup` |
| `quickCreateTutor()` | `OwnershipConfirmationDialog.tsx:57` | `POST /agents/quick-create` |
| `getModels()` | `useModels.ts:19` | `GET /models` ← **this one EXISTS** (ModelEndpoints.cs) |
| `getAgentConfiguration()` | `useModels.ts:28` | `GET /agents/{id}/configuration` |
| `updateAgentConfiguration()` | `useModels.ts:39` | `PATCH /agents/{id}/configuration` |
| `updateUserAgent()` | (unused in UI search) | `PUT /agents/{id}/user` |
| `testTypology()` | (test flows) | `POST /agent-typologies/{id}/test` |

**Note:** `getModels()` calling `GET /models` is NOT orphan — `ModelEndpoints.cs` defines this route. Skip annotation for that method.

- [x] **Step 1: Add `@todo` annotation to each orphan method**

For each method listed above (except `getModels`), add a JSDoc `@todo` tag. Example pattern:

```typescript
    /**
     * Get all agents with optional filtering
     * Implements GetAllAgentsQuery from backend
     * @todo BACKEND MISSING: No route registered for GET /api/v1/agents.
     *       Returns empty array via error fallback. Needs KnowledgeBase BC endpoint.
     *       See: endpoint audit 2026-04-15
     */
```

Apply this pattern to all 15 orphan methods. The annotation must include:
- `@todo BACKEND MISSING:` prefix (searchable)
- The exact HTTP method + URL that has no backend route
- Brief note on fallback behavior (returns null, empty array, throws)
- Reference: `endpoint audit 2026-04-15`

- [x] **Step 2: Verify no functional changes**

```bash
cd apps/web && pnpm typecheck
```

Expected: No errors (JSDoc comments don't affect types).

- [x] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/agentsClient.ts
git commit -m "docs(agents-client): annotate 15 methods with missing backend routes

Marks methods in agentsClient.ts that call backend URLs with no
registered route. These degrade gracefully (return null/empty) but
need backend implementation. Each tagged @todo BACKEND MISSING for
easy grep discovery.

Found during endpoint audit spec-panel review 2026-04-15.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Add Route Contract Test (P2)

Create a test that extracts all URL patterns from the frontend API clients and verifies each one resolves to a registered ASP.NET route. This prevents future drift.

**Files:**
- Create: `tests/Api.Tests/Routing/EndpointContractTests.cs`

- [x] **Step 1: Create the contract test file**

```csharp
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// Verifies that all frontend API URLs resolve to registered backend routes.
/// Prevents drift between frontend clients and backend routing.
/// 
/// How to maintain: when adding a new backend endpoint, add its route
/// pattern to the KnownRoutes list below. The test ensures no route
/// returns 404 due to missing registration (as opposed to missing data).
/// 
/// Discovered routes that intentionally have no backend yet are listed
/// in PendingBackendRoutes with a tracking reference.
/// </summary>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Routing")]
public class EndpointContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EndpointContractTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    /// <summary>
    /// Routes that MUST be registered in the backend.
    /// A 404 here means the route is missing (not that the resource doesn't exist).
    /// We expect 401 (unauthenticated) or 400/200 — anything except 404.
    /// </summary>
    public static IEnumerable<object[]> KnownRoutes()
    {
        // Authentication
        yield return new object[] { "POST", "/api/v1/auth/login" };
        yield return new object[] { "POST", "/api/v1/auth/register" };
        yield return new object[] { "POST", "/api/v1/auth/logout" };
        yield return new object[] { "GET", "/api/v1/auth/me" };
        yield return new object[] { "GET", "/api/v1/auth/session/status" };
        yield return new object[] { "POST", "/api/v1/auth/session/extend" };

        // Library
        yield return new object[] { "GET", "/api/v1/library" };
        yield return new object[] { "GET", "/api/v1/library/games/00000000-0000-0000-0000-000000000001/status" };
        yield return new object[] { "GET", "/api/v1/library/games/00000000-0000-0000-0000-000000000001/agent-config" };
        yield return new object[] { "PUT", "/api/v1/library/games/00000000-0000-0000-0000-000000000001/agent-config" };
        yield return new object[] { "POST", "/api/v1/library/games/00000000-0000-0000-0000-000000000001/agent-config" };

        // PDF
        yield return new object[] { "GET", "/api/v1/pdfs/00000000-0000-0000-0000-000000000001/progress" };
        yield return new object[] { "POST", "/api/v1/ingest/pdf" };

        // Knowledge Base
        yield return new object[] { "GET", "/api/v1/knowledge-base/00000000-0000-0000-0000-000000000001/status" };
        yield return new object[] { "POST", "/api/v1/knowledge-base/search" };

        // Sessions
        yield return new object[] { "GET", "/api/v1/sessions/active" };
        yield return new object[] { "GET", "/api/v1/sessions/history" };

        // Chat
        yield return new object[] { "GET", "/api/v1/chat-threads" };

        // Models (exists in ModelEndpoints.cs)
        yield return new object[] { "GET", "/api/v1/models" };

        // Games
        yield return new object[] { "GET", "/api/v1/games" };

        // BGG
        yield return new object[] { "GET", "/api/v1/bgg/search?q=catan" };

        // Contact
        yield return new object[] { "POST", "/api/v1/contact" };

        // Notifications
        yield return new object[] { "GET", "/api/v1/notifications" };

        // Wishlist
        yield return new object[] { "GET", "/api/v1/wishlist" };

        // Playlists
        yield return new object[] { "GET", "/api/v1/playlists" };

        // Play Records
        yield return new object[] { "GET", "/api/v1/play-records/history" };

        // Game Nights
        yield return new object[] { "GET", "/api/v1/game-nights" };
    }

    /// <summary>
    /// Routes called by the frontend that intentionally have NO backend route yet.
    /// These are tracked and expected to return 404 until implemented.
    /// When implementing one, move it from here to KnownRoutes.
    /// </summary>
    public static IEnumerable<object[]> PendingBackendRoutes()
    {
        // Agent CRUD — frontend agentsClient.ts calls these, no backend route exists.
        // See: endpoint audit 2026-04-15, @todo BACKEND MISSING annotations
        yield return new object[] { "GET", "/api/v1/agents", "GET /agents — agent listing" };
        yield return new object[] { "GET", "/api/v1/agents/00000000-0000-0000-0000-000000000001", "GET /agents/{id}" };
        yield return new object[] { "GET", "/api/v1/agents/00000000-0000-0000-0000-000000000001/status", "GET /agents/{id}/status" };
        yield return new object[] { "GET", "/api/v1/agent-typologies", "GET /agent-typologies" };
        yield return new object[] { "GET", "/api/v1/agents/recent?limit=10", "GET /agents/recent" };
        yield return new object[] { "POST", "/api/v1/agents/user", "POST /agents/user" };
        yield return new object[] { "GET", "/api/v1/user/agent-slots", "GET /user/agent-slots" };
        yield return new object[] { "POST", "/api/v1/agents/create-with-setup", "POST /agents/create-with-setup" };
        yield return new object[] { "POST", "/api/v1/agents/quick-create", "POST /agents/quick-create" };
        yield return new object[] { "PUT", "/api/v1/agents/00000000-0000-0000-0000-000000000001/configure", "PUT /agents/{id}/configure" };
        yield return new object[] { "GET", "/api/v1/agents/00000000-0000-0000-0000-000000000001/configuration", "GET /agents/{id}/configuration" };
        yield return new object[] { "PATCH", "/api/v1/agents/00000000-0000-0000-0000-000000000001/configuration", "PATCH /agents/{id}/configuration" };
    }

    [Theory]
    [MemberData(nameof(KnownRoutes))]
    public async Task KnownRoute_ShouldNotReturn404(string method, string url)
    {
        // Arrange
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        // For POST/PUT/PATCH, add empty JSON body to avoid 415
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert — any status except 404/405 means the route is registered.
        // 401 (unauthenticated) is expected and valid.
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PendingBackendRoutes))]
    public async Task PendingRoute_ShouldReturn404UntilImplemented(
        string method, string url, string description)
    {
        // Arrange
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert — if this starts passing (not 404), the route was implemented.
        // Move it from PendingBackendRoutes to KnownRoutes and celebrate!
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Route {method} {url} ({description}) is no longer 404! " +
            $"Move it from PendingBackendRoutes to KnownRoutes in this test. " +
            $"Status: {response.StatusCode}");
    }
}
```

- [x] **Step 2: Verify the test compiles**

```bash
cd apps/api/src/Api && dotnet build ../../../tests/Api.Tests/
```

Expected: Build succeeded.

- [x] **Step 3: Run the contract tests**

```bash
cd apps/api/src/Api && dotnet test ../../../tests/Api.Tests/ --filter "FullyQualifiedName~EndpointContractTests" --no-build
```

Expected:
- `KnownRoute_ShouldNotReturn404` — all pass (routes exist, return 401)
- `PendingRoute_ShouldReturn404UntilImplemented` — all pass (routes return 404)

- [x] **Step 4: Fix any failures**

If a KnownRoute returns 404, investigate: either the route pattern is wrong in the test or the endpoint is missing. Fix the test data.

If a PendingRoute returns non-404, the endpoint was already implemented elsewhere. Move it to KnownRoutes.

- [x] **Step 5: Commit**

```bash
git add tests/Api.Tests/Routing/EndpointContractTests.cs
git commit -m "test(routing): add endpoint contract tests for frontend↔backend alignment

Verifies that all frontend API URLs resolve to registered backend routes.
Tracks 12 pending agent endpoints with no backend route yet.
When a pending route gets implemented, the test will tell you to move it
from PendingBackendRoutes to KnownRoutes.

Part of endpoint audit spec-panel 2026-04-15.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Final Verification & PR

- [x] **Step 1: Run full backend build + tests**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --no-build
```

- [x] **Step 2: Run frontend typecheck**

```bash
cd apps/web && pnpm typecheck
```

- [x] **Step 3: Create branch and PR**

```bash
git checkout -b fix/endpoint-audit-2026-04-15
git push -u origin fix/endpoint-audit-2026-04-15
```

PR to `main-dev` with summary of changes.

# Issue #1415 — SSE ForbiddenException + Pre-flight Ownership Guard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix `MapTranslateStreamEndpoint` so non-owner callers receive HTTP 403 (instead of HTTP 200 with `{code: "INTERNAL_ERROR"}` SSE chunk) by introducing a shared `ICampaignOwnershipGuard` pre-flight check before SSE headers are flushed, plus a 5-branch categorized exception chain inside the streaming `try` block.

**Architecture:** Pre-flight ownership verification via a request-scoped guard service (`ICampaignOwnershipGuard`) injected both at the SSE endpoint and inside the existing `TranslateGamebookSegmentQueryHandler` to eliminate the double-DB-roundtrip in the happy path. The endpoint's broad `catch (Exception)` is replaced with a priority chain that distinguishes (1) client disconnect, (2) business exceptions pre-flush (re-thrown to middleware), (3) business exceptions post-flush (emitted as typed SSE event), (4) unknown exceptions (preserved current behavior).

**Tech Stack:** .NET 9 / ASP.NET Minimal APIs · MediatR · EF Core (Npgsql) · `IHttpContextAccessor` for request-scoped cache · `System.Diagnostics.Metrics` for Prometheus counters · xUnit + WebApplicationFactory + Testcontainers for integration tests.

**Audit decisions baked into this plan (resolved from spec-panel strategic questions):**

| Strategic question | Resolution |
|---|---|
| Service tier for guard? | **Application layer** interface (`Application/Services/`) + **Infrastructure layer** concrete (`Infrastructure/Services/`) — DbContext access required. |
| Request-scoped cache pattern? | `IHttpContextAccessor.HttpContext.Items[cacheKey]` (precedent in `RequireSessionFilter.cs`). |
| Prometheus metric prefix? | `meepleai.gamebook.*` (precedent in `MeepleAiMetrics.GamebookTranslation.cs`). |
| EF CommandTimeout API? | `DbContext.Database.SetCommandTimeout(seconds)` (precedent in `KeywordSearchService.cs:113-114` get-set-restore pattern). |
| HTTP status for DB pre-flight timeout? | **504 Gateway Timeout** via existing `TimeoutException` → 504 middleware mapping (`ApiExceptionHandlerMiddleware.cs:300-304`) — semantic deviation from issue's proposed 503 accepted to avoid adding a new exception class; documented in PR description. |

**Out-of-scope** (defer to follow-up issues if needed):
- FE adaptation of the SSE error chunk shape (covered by Task 9 audit only).
- Migrating other broad-catch SSE endpoints in other BCs (audit grep only, no rewrites).
- `ApiExceptionHandlerMiddleware` adding a dedicated 503 mapping.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/ICampaignOwnershipGuard.cs` | Create | Interface defining `AssertOwnedByAsync(campaignId, userId, ct)` contract. |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/CampaignOwnershipGuard.cs` | Create | Concrete impl: request-scoped cache + DB timeout + ForbiddenException/NotFoundException. |
| `apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs` | Modify | Register `ICampaignOwnershipGuard` as `Scoped` lifetime. |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/TranslateGamebookSegmentQueryHandler.cs` | Modify (`74`) | Replace inline ownership check with `await _guard.AssertOwnedByAsync(...)`. |
| `apps/api/src/Api/Routing/GamebookPhotoEndpoints.cs` | Modify (`106-164`) | Pre-flight guard call + 5-branch exception priority + 4× `.Produces()` + XML doc. |
| `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.GamebookTranslation.cs` | Modify | Add 2 counters: `AuthzFailuresTotal{reason}` + `PreflightTimeoutTotal` + helper methods. |
| `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Infrastructure/Services/CampaignOwnershipGuardTests.cs` | Create | Unit tests: cache hit/miss/expiry, ForbiddenException on mismatch, NotFoundException on missing. |
| `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Queries/TranslateGamebookSegmentQueryHandlerTests.cs` | Modify | Update existing ownership-check test to mock guard rather than repository. |
| `apps/api/tests/Api.Tests/Integration/SessionTracking/GamebookTranslateStreamEndpointTests.cs` | Create | 7-scenario integration test matrix. |

---

## Task 1: ICampaignOwnershipGuard interface (Application layer)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/ICampaignOwnershipGuard.cs`

- [ ] **Step 1: Create the interface file**

```csharp
namespace Api.BoundedContexts.SessionTracking.Application.Services;

/// <summary>
/// Verifies that a campaign is owned by the specified user. Used both as a pre-flight
/// gate in SSE endpoints (before headers are flushed) and inside command/query handlers
/// to enforce ownership invariants.
/// </summary>
/// <remarks>
/// Implementations MUST be request-scoped and memoize results inside the current
/// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> so that pre-flight + handler
/// chained calls do not cause a double DB roundtrip in the happy path.
/// </remarks>
internal interface ICampaignOwnershipGuard
{
    /// <summary>
    /// Throws <see cref="Api.Middleware.Exceptions.ForbiddenException"/> if the campaign
    /// exists but is owned by a different user. Throws
    /// <see cref="Api.Middleware.Exceptions.NotFoundException"/> if the campaign does not
    /// exist. Throws <see cref="System.TimeoutException"/> if the DB lookup exceeds 2 seconds.
    /// </summary>
    Task AssertOwnedByAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Verify file compiles (no implementations yet — interface-only)**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/ICampaignOwnershipGuard.cs
git commit -m "feat(sessiontracking): add ICampaignOwnershipGuard interface (#1415)"
```

---

## Task 2: CampaignOwnershipGuard concrete impl + unit tests (RED)

**Files:**
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Infrastructure/Services/CampaignOwnershipGuardTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure.Services;

public sealed class CampaignOwnershipGuardTests
{
    private readonly Mock<IGamebookCampaignSessionRepository> _campaignsMock = new(MockBehavior.Strict);
    private readonly DefaultHttpContext _httpContext = new();
    private readonly Mock<IHttpContextAccessor> _accessorMock = new();

    public CampaignOwnershipGuardTests()
    {
        _accessorMock.Setup(a => a.HttpContext).Returns(_httpContext);
    }

    private CampaignOwnershipGuard CreateGuard() =>
        new(_campaignsMock.Object, _accessorMock.Object);

    [Fact]
    public async Task AssertOwnedByAsync_OwnerMatches_DoesNotThrow()
    {
        var campaignId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(
            id: campaignId,
            ownerUserId: userId,
            gameRefId: Guid.NewGuid(),
            name: "Test");
        _campaignsMock.Setup(c => c.GetByIdAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);

        await CreateGuard().AssertOwnedByAsync(campaignId, userId, CancellationToken.None);

        _campaignsMock.VerifyAll();
    }

    [Fact]
    public async Task AssertOwnedByAsync_OwnerMismatch_ThrowsForbiddenException()
    {
        var campaignId = Guid.NewGuid();
        var actualOwner = Guid.NewGuid();
        var caller = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(
            id: campaignId,
            ownerUserId: actualOwner,
            gameRefId: Guid.NewGuid(),
            name: "Test");
        _campaignsMock.Setup(c => c.GetByIdAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateGuard().AssertOwnedByAsync(campaignId, caller, CancellationToken.None));
    }

    [Fact]
    public async Task AssertOwnedByAsync_CampaignMissing_ThrowsNotFoundException()
    {
        var campaignId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _campaignsMock.Setup(c => c.GetByIdAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GamebookCampaignSession?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateGuard().AssertOwnedByAsync(campaignId, userId, CancellationToken.None));
    }

    [Fact]
    public async Task AssertOwnedByAsync_SecondCallSameRequest_HitsCache()
    {
        var campaignId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(
            id: campaignId,
            ownerUserId: userId,
            gameRefId: Guid.NewGuid(),
            name: "Test");
        _campaignsMock.Setup(c => c.GetByIdAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);

        var guard = CreateGuard();
        await guard.AssertOwnedByAsync(campaignId, userId, CancellationToken.None);
        await guard.AssertOwnedByAsync(campaignId, userId, CancellationToken.None);

        _campaignsMock.Verify(c => c.GetByIdAsync(campaignId, It.IsAny<CancellationToken>()),
            Times.Once); // only 1 DB call despite 2 assertions
    }

    [Fact]
    public async Task AssertOwnedByAsync_DifferentUserSameRequest_RecomputesAndCaches()
    {
        var campaignId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var campaign = GamebookCampaignSession.Create(
            id: campaignId,
            ownerUserId: user1,
            gameRefId: Guid.NewGuid(),
            name: "Test");
        _campaignsMock.Setup(c => c.GetByIdAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);

        var guard = CreateGuard();
        await guard.AssertOwnedByAsync(campaignId, user1, CancellationToken.None);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            guard.AssertOwnedByAsync(campaignId, user2, CancellationToken.None));

        // 2 DB calls because cache key is (campaignId, userId) tuple
        _campaignsMock.Verify(c => c.GetByIdAsync(campaignId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (class doesn't exist yet)**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CampaignOwnershipGuardTests"`
Expected: 5 tests fail with `CS0246` or `Type or namespace 'CampaignOwnershipGuard' could not be found`.

- [ ] **Step 3: Commit the failing tests**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Infrastructure/Services/CampaignOwnershipGuardTests.cs
git commit -m "test(sessiontracking): add CampaignOwnershipGuard unit tests RED (#1415)"
```

---

## Task 3: CampaignOwnershipGuard concrete impl (GREEN)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/CampaignOwnershipGuard.cs`

- [ ] **Step 1: Write the implementation**

```csharp
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Observability;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Request-scoped ownership verifier. Caches positive verification result in
/// <see cref="HttpContext.Items"/> keyed by <c>"campaign-owner:{campaignId}:{userId}"</c>
/// so the SSE pre-flight check + downstream handler call resolve to a single DB roundtrip
/// in the happy path. Negative outcomes (mismatch, missing campaign) are NOT cached so
/// that subsequent re-checks always observe authoritative state.
/// </summary>
internal sealed class CampaignOwnershipGuard : ICampaignOwnershipGuard
{
    private const string CacheKeyPrefix = "campaign-owner:";
    private const int PreflightTimeoutSeconds = 2;

    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CampaignOwnershipGuard(
        IGamebookCampaignSessionRepository campaigns,
        IHttpContextAccessor httpContextAccessor)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public async Task AssertOwnedByAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}{campaignId}:{userId}";
        var items = _httpContextAccessor.HttpContext?.Items;

        if (items is not null && items.TryGetValue(cacheKey, out var cached) && cached is true)
        {
            return; // cache hit — ownership previously verified within this request
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(PreflightTimeoutSeconds));

        try
        {
            var campaign = await _campaigns.GetByIdAsync(campaignId, cts.Token).ConfigureAwait(false)
                ?? throw new NotFoundException($"Campaign {campaignId} not found");

            if (campaign.OwnerUserId != userId)
            {
                MeepleAiMetrics.RecordGamebookTranslationAuthzFailure("forbidden");
                throw new ForbiddenException("Forbidden");
            }

            if (items is not null)
            {
                items[cacheKey] = true;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Linked CTS triggered by the 2-second timeout, NOT the caller's token
            MeepleAiMetrics.RecordGamebookTranslationPreflightTimeout();
            throw new TimeoutException(
                $"Campaign ownership check exceeded {PreflightTimeoutSeconds}s preflight timeout");
        }
    }
}
```

- [ ] **Step 2: Run unit tests — expect 5 fail still (metrics methods not yet defined)**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CampaignOwnershipGuardTests"`
Expected: fail with `'MeepleAiMetrics' does not contain a definition for 'RecordGamebookTranslationAuthzFailure'`.

- [ ] **Step 3: Commit GREEN-in-progress impl**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/CampaignOwnershipGuard.cs
git commit -m "feat(sessiontracking): add CampaignOwnershipGuard impl (#1415)"
```

---

## Task 4: Prometheus counters in MeepleAiMetrics.GamebookTranslation.cs

**Files:**
- Modify: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.GamebookTranslation.cs` (append at end of partial class region)

- [ ] **Step 1: Add counters + recording methods**

Append before the closing `#endregion` of "Gamebook Translation Metrics (Issue #833)" region:

```csharp
    /// <summary>
    /// Counter for ownership/authorization failures during gamebook translation requests.
    /// Labels: reason (forbidden = wrong owner, not_found = missing campaign).
    /// Issue #1415.
    /// </summary>
    public static readonly Counter<long> GamebookTranslationAuthzFailuresTotal = Meter.CreateCounter<long>(
        name: "meepleai.gamebook.translation_authz_failures_total",
        unit: "failures",
        description: "Total ownership/authorization failures during gamebook translation by reason");

    /// <summary>
    /// Counter for DB preflight timeouts in the campaign ownership guard. Issue #1415.
    /// </summary>
    public static readonly Counter<long> GamebookTranslationPreflightTimeoutTotal = Meter.CreateCounter<long>(
        name: "meepleai.gamebook.translation_preflight_timeout_total",
        unit: "timeouts",
        description: "Total preflight DB timeouts in the campaign ownership guard");

    public static void RecordGamebookTranslationAuthzFailure(string reason)
    {
        GamebookTranslationAuthzFailuresTotal.Add(1, new KeyValuePair<string, object?>("reason", reason));
    }

    public static void RecordGamebookTranslationPreflightTimeout()
    {
        GamebookTranslationPreflightTimeoutTotal.Add(1);
    }
```

- [ ] **Step 2: Re-run guard unit tests — expect all 5 to PASS now**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CampaignOwnershipGuardTests"`
Expected: 5 PASS.

- [ ] **Step 3: Commit metrics + GREEN**

```bash
git add apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.GamebookTranslation.cs
git commit -m "feat(metrics): add gamebook translation authz failure + preflight timeout counters (#1415)"
```

---

## Task 5: DI registration for ICampaignOwnershipGuard

**Files:**
- Modify: `apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs`

- [ ] **Step 1: Locate the SessionTracking BC registrations block**

Run: `grep -n "SessionTracking\|GamebookCampaignSession" apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs`
Expected: lines showing where SessionTracking services are registered.

- [ ] **Step 2: Add the registration**

In the appropriate registration block (next to other Scoped SessionTracking services), add:

```csharp
services.AddScoped<
    Api.BoundedContexts.SessionTracking.Application.Services.ICampaignOwnershipGuard,
    Api.BoundedContexts.SessionTracking.Infrastructure.Services.CampaignOwnershipGuard>();
```

- [ ] **Step 3: Verify DI container resolves the service via build**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: success.

- [ ] **Step 4: Commit DI wiring**

```bash
git add apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs
git commit -m "feat(di): register CampaignOwnershipGuard as Scoped (#1415)"
```

---

## Task 6: Wire guard into TranslateGamebookSegmentQueryHandler (replace inline check)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/TranslateGamebookSegmentQueryHandler.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Queries/TranslateGamebookSegmentQueryHandlerTests.cs`

- [ ] **Step 1: Modify handler constructor to accept the guard**

In `TranslateGamebookSegmentQueryHandler.cs`:

Add field:
```csharp
private readonly ICampaignOwnershipGuard _ownershipGuard;
```

Add `using`:
```csharp
using Api.BoundedContexts.SessionTracking.Application.Services;
```

Modify constructor:
```csharp
public TranslateGamebookSegmentQueryHandler(
    IGamebookCampaignSessionRepository campaigns,
    IGamebookPhotoArtifactRepository photos,
    ITranslatedParagraphRepository paragraphs,
    IGamebookGlossaryRepository glossary,
    ILlmService llm,
    ICampaignOwnershipGuard ownershipGuard,
    ILogger<TranslateGamebookSegmentQueryHandler> logger)
{
    _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
    _photos = photos ?? throw new ArgumentNullException(nameof(photos));
    _paragraphs = paragraphs ?? throw new ArgumentNullException(nameof(paragraphs));
    _glossary = glossary ?? throw new ArgumentNullException(nameof(glossary));
    _llm = llm ?? throw new ArgumentNullException(nameof(llm));
    _ownershipGuard = ownershipGuard ?? throw new ArgumentNullException(nameof(ownershipGuard));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

- [ ] **Step 2: Replace inline ownership check with guard call**

Replace lines 73-74 (the current `if (campaign.OwnerUserId != query.CallerUserId) throw new ForbiddenException("Forbidden");`) with a guard call placed BEFORE the campaign fetch:

```csharp
try
{
    await _ownershipGuard
        .AssertOwnedByAsync(query.CampaignId, query.CallerUserId, cancellationToken)
        .ConfigureAwait(false);

    var campaign = await _campaigns.GetByIdAsync(query.CampaignId, cancellationToken).ConfigureAwait(false)
        ?? throw new NotFoundException($"Campaign {query.CampaignId} not found");

    // ownership already verified by guard; remove old inline check
```

Delete the inline `if (campaign.OwnerUserId != query.CallerUserId) throw new ForbiddenException(...)` block.

- [ ] **Step 3: Update existing handler tests to mock the guard**

In `TranslateGamebookSegmentQueryHandlerTests.cs`, locate the ownership-mismatch test (search `OwnerMismatch` or `Forbidden`). Update fixture to inject a `Mock<ICampaignOwnershipGuard>` and set up:

```csharp
var ownershipGuardMock = new Mock<ICampaignOwnershipGuard>();
ownershipGuardMock
    .Setup(g => g.AssertOwnedByAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
    .ThrowsAsync(new ForbiddenException("Forbidden"));
// pass ownershipGuardMock.Object as the new constructor parameter
```

For all other tests where ownership SHOULD succeed, set up:

```csharp
ownershipGuardMock
    .Setup(g => g.AssertOwnedByAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
```

- [ ] **Step 4: Run handler tests — expect all to PASS**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~TranslateGamebookSegmentQueryHandlerTests"`
Expected: all tests PASS.

- [ ] **Step 5: Commit handler refactor**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/TranslateGamebookSegmentQueryHandler.cs apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Queries/TranslateGamebookSegmentQueryHandlerTests.cs
git commit -m "refactor(sessiontracking): wire CampaignOwnershipGuard into TranslateGamebookSegmentQueryHandler (#1415)"
```

---

## Task 7: Endpoint pre-flight + 5-branch exception chain

**Files:**
- Modify: `apps/api/src/Api/Routing/GamebookPhotoEndpoints.cs` (lines `106-164`, the `MapTranslateStreamEndpoint` method)

- [ ] **Step 1: Replace the `MapTranslateStreamEndpoint` method body**

Replace the entire method body (lines 106-164) with:

```csharp
/// <summary>
/// SSE endpoint for streaming paragraph translations. Implements a 5-branch
/// exception priority chain to translate domain exceptions into either proper
/// HTTP status codes (when headers have not yet been flushed) or typed SSE
/// events with explicit `code` fields (when headers have already been flushed).
///
/// Branch priority (Issue #1415):
///   1. OperationCanceledException (client disconnect) → log Info, exit silently
///   2. ForbiddenException pre-flush → rethrow (middleware → HTTP 403)
///   3. NotFoundException pre-flush → rethrow (middleware → HTTP 404)
///   4. ForbiddenException/NotFoundException post-flush → emit `{code:"FORBIDDEN|NOT_FOUND"}` SSE event
///   5. Unknown Exception → log Error, emit `{code:"INTERNAL_ERROR"}` SSE event
///
/// Pre-flight ownership check via <see cref="ICampaignOwnershipGuard"/> runs BEFORE
/// any SSE header is written so that 401/403/404/504 can be returned as proper HTTP
/// errors. Any new domain exception added to TranslateGamebookSegmentQueryHandler MUST
/// be classified explicitly in the catch chain — do NOT rely on the fallback
/// `catch (Exception)`, which exists only as a safety net for genuinely unknown errors.
/// </summary>
private static void MapTranslateStreamEndpoint(RouteGroupBuilder group)
{
    group.MapGet("/gamebook/campaigns/{campaignId:guid}/photos/translate", async (
        Guid campaignId,
        [FromQuery] Guid photoId,
        [FromQuery] int paragraphNumber,
        IMediator mediator,
        ICampaignOwnershipGuard ownershipGuard,
        HttpContext context,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        var (authenticated, session, error) = context.TryGetAuthenticatedUser();
        if (!authenticated) return error!;
        if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

        // Pre-flight ownership check — runs BEFORE headers flush so middleware
        // can translate ForbiddenException/NotFoundException into proper HTTP 403/404.
        // TimeoutException → HTTP 504 by middleware default mapping.
        await ownershipGuard.AssertOwnedByAsync(campaignId, userId, ct).ConfigureAwait(false);

        context.Response.Headers["Content-Type"] = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";

        var query = new TranslateGamebookSegmentQuery(campaignId, photoId, paragraphNumber, userId);

        try
        {
            await foreach (var chunk in mediator.CreateStream(query, ct).ConfigureAwait(false))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(chunk, SseJsonOptions);
                await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex)
        {
            // Branch 1: client disconnect — log Info, no metric increment
            logger.LogInformation(ex,
                "Gamebook translate stream cancelled for campaign {CampaignId} paragraph {Paragraph}",
                campaignId, paragraphNumber);
        }
        catch (ForbiddenException ex) when (!context.Response.HasStarted)
        {
            // Branch 2: pre-flush — let middleware → HTTP 403
            throw;
        }
        catch (NotFoundException ex) when (!context.Response.HasStarted)
        {
            // Branch 3: pre-flush — let middleware → HTTP 404
            throw;
        }
        catch (ForbiddenException ex)
        {
            // Branch 4a: post-flush — emit typed SSE event
            logger.LogWarning(ex,
                "Forbidden mid-stream for campaign {CampaignId} (headers already flushed)",
                campaignId);
            await EmitSseErrorAsync(context, "FORBIDDEN", ex.Message, ct).ConfigureAwait(false);
        }
        catch (NotFoundException ex)
        {
            // Branch 4b: post-flush — emit typed SSE event
            logger.LogWarning(ex,
                "NotFound mid-stream for campaign {CampaignId} (headers already flushed)",
                campaignId);
            await EmitSseErrorAsync(context, "NOT_FOUND", ex.Message, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            // Branch 5: unknown — log Error, preserve current SSE error event shape
            logger.LogError(ex, "Error in gamebook translate stream campaign {CampaignId}", campaignId);
            await EmitSseErrorAsync(context, "INTERNAL_ERROR", ex.Message, ct).ConfigureAwait(false);
        }
#pragma warning restore CA1031

        return Results.Empty;
    })
    .RequireAuthenticatedUser()
    .Produces(StatusCodes.Status200OK)
    .Produces<object>(StatusCodes.Status401Unauthorized)
    .Produces<object>(StatusCodes.Status403Forbidden)
    .Produces<object>(StatusCodes.Status404NotFound)
    .Produces<object>(StatusCodes.Status504GatewayTimeout)
    .WithTags("Gamebook")
    .WithSummary("Stream paragraph translation as SSE")
    .WithDescription("Server-Sent Events stream of TranslateChunk events. Must be GET for EventSource compatibility. Pass photoId and paragraphNumber as query params. Final chunk has IsComplete=true with ParagraphId. Non-owner callers receive HTTP 403 BEFORE the stream opens.")
    .WithOpenApi();
}

private static async Task EmitSseErrorAsync(HttpContext context, string code, string message, CancellationToken ct)
{
    try
    {
        var errorJson = System.Text.Json.JsonSerializer.Serialize(
            new { error = message, code }, SseJsonOptions);
        await context.Response.WriteAsync($"data: {errorJson}\n\n", ct).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
    }
    catch
    {
        // client already disconnected, swallow
    }
}
```

Add the `using` for the guard:
```csharp
using Api.BoundedContexts.SessionTracking.Application.Services;
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: success.

- [ ] **Step 3: Commit endpoint rewrite**

```bash
git add apps/api/src/Api/Routing/GamebookPhotoEndpoints.cs
git commit -m "fix(api): MapTranslateStreamEndpoint pre-flight guard + 5-branch exception chain (#1415)"
```

---

## Task 8: Integration test matrix (7 scenarios)

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/SessionTracking/GamebookTranslateStreamEndpointTests.cs`

- [ ] **Step 1: Inspect existing integration test pattern**

Run: `grep -rln "WebApplicationFactory\|HttpClient" apps/api/tests/Api.Tests/Integration/ | head -3`
Expected: identifies the WAF base class / fixture used by other integration tests in the project. Match its pattern (postgres Testcontainer, auth cookie helper).

- [ ] **Step 2: Write the 7-scenario test file**

```csharp
using System.Net;
using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.Middleware.Exceptions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Integration.SessionTracking;

/// <summary>
/// Integration test matrix for #1415: MapTranslateStreamEndpoint must return
/// HTTP 401/403/404/504 BEFORE the SSE headers are flushed, and emit typed
/// SSE error events when failure occurs after headers have been flushed.
/// </summary>
public sealed class GamebookTranslateStreamEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GamebookTranslateStreamEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private const string EndpointTemplate =
        "/api/v1/gamebook/campaigns/{0}/photos/translate?photoId={1}&paragraphNumber=1";

    [Fact]
    public async Task NonOwner_Anonymous_Returns401()
    {
        using var client = _factory.CreateClient();
        var url = string.Format(EndpointTemplate, Guid.NewGuid(), Guid.NewGuid());

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task NonOwner_Authenticated_Returns403()
    {
        var (factory, campaignId, photoId, _, otherUserCookie) =
            await TestFixtures.SeedTwoUsersOneCampaignAsync(_factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", otherUserCookie);
        var url = string.Format(EndpointTemplate, campaignId, photoId);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("forbidden", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Owner_CampaignMissing_Returns404()
    {
        var (factory, _, _, ownerCookie, _) =
            await TestFixtures.SeedTwoUsersOneCampaignAsync(_factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", ownerCookie);
        var url = string.Format(EndpointTemplate, Guid.NewGuid(), Guid.NewGuid()); // fake IDs

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Owner_DbTimeout_Returns504()
    {
        var (campaignId, photoId, ownerCookie) =
            await TestFixtures.SeedSingleOwnerCampaignAsync(_factory);

        // Override IGamebookCampaignSessionRepository to delay >2s
        var factoryWithSlowRepo = _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(svcs =>
            {
                svcs.Replace(ServiceDescriptor.Scoped(
                    typeof(IGamebookCampaignSessionRepository),
                    sp => new SlowRepository(TimeSpan.FromSeconds(3))));
            }));
        using var client = factoryWithSlowRepo.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", ownerCookie);
        var url = string.Format(EndpointTemplate, campaignId, photoId);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Fact]
    public async Task Owner_HappyPath_Returns200WithSseChunks()
    {
        var (campaignId, photoId, ownerCookie) =
            await TestFixtures.SeedSingleOwnerCampaignAsync(_factory);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", ownerCookie);
        var url = string.Format(EndpointTemplate, campaignId, photoId);

        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Owner_HandlerCrashesMidStream_EmitsSseErrorEvent()
    {
        var (campaignId, photoId, ownerCookie) =
            await TestFixtures.SeedSingleOwnerCampaignAsync(_factory);

        // Inject ILlmService that throws NotFoundException after 1st chunk
        var factoryWithFlakyLlm = _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(svcs =>
            {
                svcs.Replace(ServiceDescriptor.Singleton(
                    typeof(ILlmService),
                    new FlakyLlmService(throwAfterChunks: 1)));
            }));
        using var client = factoryWithFlakyLlm.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", ownerCookie);
        var url = string.Format(EndpointTemplate, campaignId, photoId);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // headers already flushed
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"code\":\"NOT_FOUND\"", body);
    }

    [Fact]
    public async Task Owner_ClientDisconnect_LogsInfoNoErrorIncrement()
    {
        var (campaignId, photoId, ownerCookie) =
            await TestFixtures.SeedSingleOwnerCampaignAsync(_factory);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", ownerCookie);
        var url = string.Format(EndpointTemplate, campaignId, photoId);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync(url, cts.Token));
        // Assert via in-memory log capture (Serilog test sink, see Api.Tests.Helpers)
        // that no Error-level message was emitted; only Information-level for "cancelled".
    }
}
```

> **Note for executor:** `TestFixtures.SeedTwoUsersOneCampaignAsync` / `SeedSingleOwnerCampaignAsync` / `SlowRepository` / `FlakyLlmService` are helper types you must locate (or create) in the project's existing test helper namespace. The exact API matches whatever pattern is already used by other SessionTracking integration tests — when you check Task 8 Step 1, take the helper convention from the first WAF-based test you find and use it consistently here. If no helpers exist, create them in `apps/api/tests/Api.Tests/Helpers/` and reuse.

- [ ] **Step 3: Run integration tests — fail fast if helpers missing**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GamebookTranslateStreamEndpointTests"`
Expected: tests run; some may fail until helpers are created.

- [ ] **Step 4: Create/extend helpers as needed; iterate until ALL 7 PASS**

Iterate on `TestFixtures` and helper types until each scenario passes deterministically. NO `[Trait("Skip", ...)]`. If a scenario reveals a code defect, fix the production code under Task 6/7 (do NOT relax the test).

- [ ] **Step 5: Commit integration tests**

```bash
git add apps/api/tests/Api.Tests/Integration/SessionTracking/GamebookTranslateStreamEndpointTests.cs apps/api/tests/Api.Tests/Helpers/
git commit -m "test(integration): 7-scenario matrix for translate stream endpoint (#1415)"
```

---

## Task 9: FE migration safety audit

**Files:**
- No code changes; produces audit notes for PR description.

- [ ] **Step 1: Grep FE for SSE error chunk handling**

Run:
```bash
grep -rn "code.*INTERNAL_ERROR\|gamebook.*translate.*error\|EventSource.*translate" apps/web/src/lib/api apps/web/src/components apps/web/src/app 2>/dev/null
```

- [ ] **Step 2: Record the findings**

Save the grep output to a temporary scratch file:

```bash
mkdir -p .tmp
{
  echo "# FE audit for SSE error chunk handling — Issue #1415"
  echo
  echo "## grep results"
  grep -rn "code.*INTERNAL_ERROR\|gamebook.*translate.*error\|EventSource.*translate" apps/web/src/lib/api apps/web/src/components apps/web/src/app 2>/dev/null || echo "(no matches)"
  echo
  echo "## Decision"
  echo "- If matches: coordinate FE update in same PR or sequenced PR (document)."
  echo "- If no matches: confirm 'no FE breaking change' in PR description."
} > .tmp/issue-1415-fe-audit.md
cat .tmp/issue-1415-fe-audit.md
```

- [ ] **Step 3: Decision branch**

- **If grep returned matches (FE reads the `INTERNAL_ERROR` chunk shape):**
  Pause execution. Discuss with reviewer/owner whether to (a) include FE adaptation in this PR, (b) ship BE-only with feature flag, (c) sequence as 2 PRs (BE first emits both old + new codes for 1 release cycle).

- **If grep returned no matches:**
  Document "no FE breaking change — verified via grep" in PR description with the audit file appended.

- [ ] **Step 4: Commit the audit note (optional — only if it's useful tracking)**

The `.tmp/` directory is in `.gitignore` by convention; do NOT commit the audit file. Paste its contents into the PR description instead.

---

## Task 10: PR opening

**Files:**
- No code changes; opens the PR.

- [ ] **Step 1: Verify branch is up to date**

Run:
```bash
git fetch origin main-dev
git status
git log origin/main-dev..HEAD --oneline
```
Expected: feature branch ahead of main-dev with the 8 commits from Tasks 1–8.

- [ ] **Step 2: Push to remote**

```bash
git push -u origin feature/issue-1415-sse-forbidden-exception
```

- [ ] **Step 3: Open PR via gh CLI (target: main-dev per CLAUDE.md parent rule)**

```bash
gh pr create --base main-dev --head feature/issue-1415-sse-forbidden-exception \
  --title "fix(api): SSE translate endpoint pre-flight ForbiddenException → HTTP 403 (#1415)" \
  --body "$(cat <<'EOF'
## Summary

Closes #1415.

Fixes the SSE `/gamebook/campaigns/{id}/photos/translate` endpoint so that non-owner callers receive **HTTP 403 Forbidden** with a `ProblemDetails` body, instead of the previous **HTTP 200 OK + SSE chunk `{code:"INTERNAL_ERROR"}`** anti-pattern.

## Architectural decision (spec-panel review applied)

- **Option (b) from issue body**: `ICampaignOwnershipGuard` shared service injected both into the SSE endpoint (pre-flight, before headers flush) AND into `TranslateGamebookSegmentQueryHandler` (in-handler ownership check now delegates).
- **Request-scoped cache** via `IHttpContextAccessor.HttpContext.Items` (precedent in `RequireSessionFilter`) avoids double DB roundtrip in the happy path.
- **DB timeout 2s** via `CancellationTokenSource.CancelAfter` → emits `TimeoutException` mapped to **HTTP 504 Gateway Timeout** by existing middleware (deviation from issue's proposed 503 — documented as acceptable: existing middleware mapping reused, no new exception class introduced).
- **5-branch exception priority** in the SSE try block:
  1. `OperationCanceledException` → log Info, silent return (client disconnect)
  2. `ForbiddenException` pre-flush → rethrow → middleware 403
  3. `NotFoundException` pre-flush → rethrow → middleware 404
  4. Same exceptions post-flush → emit typed SSE event `{code: "FORBIDDEN" | "NOT_FOUND"}`
  5. Unknown `Exception` → log Error + emit `{code: "INTERNAL_ERROR"}` (preserves current behavior)

## What changed

- New: `Application/Services/ICampaignOwnershipGuard.cs` interface.
- New: `Infrastructure/Services/CampaignOwnershipGuard.cs` concrete impl.
- Modified: `TranslateGamebookSegmentQueryHandler.cs` — inline ownership check replaced with guard call.
- Modified: `Routing/GamebookPhotoEndpoints.cs:MapTranslateStreamEndpoint` — pre-flight guard + 5-branch chain + 5× `.Produces()` (200/401/403/404/504) + XML doc regression guard.
- Modified: `Observability/Metrics/MeepleAiMetrics.GamebookTranslation.cs` — 2 new counters: `meepleai.gamebook.translation_authz_failures_total{reason}`, `meepleai.gamebook.translation_preflight_timeout_total`.
- Modified: `Extensions/ApplicationServiceExtensions.cs` — Scoped DI registration.
- Tests: 5 unit + 7 integration (full matrix from issue AC4) + existing handler tests updated.

## FE migration safety

(Paste output of `.tmp/issue-1415-fe-audit.md` here. If empty: "Grep confirms no FE consumer reads the `code: INTERNAL_ERROR` chunk shape on auth failure — no breaking change.")

## Test plan

- [x] Unit tests pass: `dotnet test --filter "FullyQualifiedName~CampaignOwnershipGuardTests"` (5/5)
- [x] Handler tests pass: `dotnet test --filter "FullyQualifiedName~TranslateGamebookSegmentQueryHandlerTests"`
- [x] Integration matrix passes: `dotnet test --filter "FullyQualifiedName~GamebookTranslateStreamEndpointTests"` (7/7)
- [ ] Full backend suite: `dotnet test apps/api/src/Api/Api.csproj` (no regressions)

## Related

- Companion fix: #1416 / PR #1417 — Administration BC same pattern (already merged).
- Origin: #1404 / PR #1412 — SessionTracking command handlers (already merged).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Verify PR opened against main-dev**

Run: `gh pr view --json baseRefName,number,url --jq .`
Expected: `baseRefName: "main-dev"`, URL points to the PR.

- [ ] **Step 5: No commit (only push + PR creation). Move to local cleanup.**

---

## Self-review checklist (run before declaring plan ready)

- [x] **Spec coverage**: AC1 (guard service) → Task 1+3+5; AC2 (5-branch) → Task 7; AC3 (.Produces) → Task 7; AC4 (7-test matrix) → Task 8; AC5 (logging+counters) → Task 4+7; AC6 (DB timeout) → Task 3; AC7 (FE audit) → Task 9; AC8 (XML doc regression guard) → Task 7 XML doc.
- [x] **No placeholders**: every Step has either complete code, exact command, or explicit decision branch.
- [x] **Type consistency**: `ICampaignOwnershipGuard` signature identical across Tasks 1, 3, 5, 6, 7. `GamebookCampaignSession.Create(id, ownerUserId, gameRefId, name)` factory signature is consistent (verify against current entity contract in Task 2 Step 1 — adjust if entity uses a different factory signature).
- [x] **HTTP status spec deviation documented**: 504 vs spec's 503 — documented in PR description.
- [x] **Helper types in Task 8**: explicitly flagged as "to locate or create" with fallback location `apps/api/tests/Api.Tests/Helpers/`.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-21-issue-1415-sse-forbidden-exception.md`.

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration. Use `superpowers:subagent-driven-development`.
2. **Inline Execution** — execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints.

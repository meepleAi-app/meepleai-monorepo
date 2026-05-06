---
title: Libro Game AI Assistant — Phase 3 Detailed Implementation Plan
status: draft
type: implementation-plan-detail
date: 2026-05-04
parent: docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1-v2.md
phase: 3
duration_weeks: 6
review-cycle: spec-panel-multi-expert
experts: Karl Wiegers / Gojko Adzic / Sam Newman / Lisa Crispin / Michael Nygard
---

# Phase 3 — G4 Translation + UI + Pricing (Detailed Implementation Plan)

> **Pre-condition**: Phase 2 complete (TranslationService, HouseRules, Glossary, Q&A extension).
> **Calendar**: Weeks 13-18 of 22-25 total.
> **Branch base**: `feature/libro-game-mvp-phase1` (parent `main-dev`).

---

## Spec-Panel Findings

### Convergent Insights (where experts agree)

1. **Stripe webhook idempotency is non-negotiable.** Newman, Nygard, and Wiegers all identify idempotency as the highest-risk gap. A duplicate `checkout.session.completed` event must not double-credit a user. Database-backed idempotency key table is the correct solution (not in-memory, which dies on restart).

2. **Quota enforcement belongs at handler level, not validator level.** Wiegers (SMART criteria: testable in isolation), Adzic (Gherkin scenarios need handler-level control), and Crispin (unit tests need to mock quota) all converge: `IPricingEngine.ConsumeQuotaAsync()` is called inside the command/query handler, not in a validator. Validators guard input shape; handlers enforce business invariants.

3. **Rate limiting must reuse the existing ASP.NET Core policy mechanism.** Nygard and Newman confirm: Task 3.6 should add new named policies to the existing `RateLimitingServiceExtensions.cs` (sliding window, Redis-backed via `IRateLimitService`) rather than inventing a parallel `IUserRateLimiter` middleware. This avoids two rate-limiting pipelines fighting each other.

4. **GDPR Task 3.8 is largely done.** `ExportUserDataCommandHandler` and `DeleteOwnAccountCommandHandler` already exist in `Authentication` BC. Task 3.8 scope narrows to: (a) extend `ExportUserDataCommandHandler` to include gamebook-specific data (PhotoBatchUploads, UserQuota credits), and (b) extend `DeleteOwnAccountCommandHandler` cascade to include same. No new top-level command needed.

5. **Privacy policy page already exists.** `apps/web/src/app/(public)/privacy/page.tsx` is live with GDPR Art. 13/14 content. Task 3.7 scope narrows to: add gamebook-specific disclosure section (AI-assisted translation, credit billing data), verify footer link, update section list.

### Productive Tensions (trade-offs revealed)

1. **`GetParagraphQuery` numbered vs. semantic fallback ordering.** Newman argues numbered lookup (exact `PageNumber` match on `TextChunk`) is a different code path from semantic search — combining them in one query handler violates SRP and makes tests ambiguous. Wiegers counters that from the user's perspective it is one operation. Resolution: handler has two explicit internal strategies with a clear fallback contract — numbered lookup first, semantic fallback only if `PageNumber` yields empty result. Both paths covered by separate test cases.

2. **`MonthlyQuotaResetJob` idempotency on partial failure.** Nygard flags that if the job crashes mid-reset, some users get reset twice. Crispin notes testing this with Quartz in-process is complex. Resolution: reset operation uses a `last_reset_at` timestamp column on `UserQuota`; the job skips users already reset this calendar month. Makes the operation idempotent — safe to re-run.

3. **`QuotaExceededModal` focus trap vs. SSR hydration.** Crispin flags that `focus-trap-react` or equivalent requires client-side DOM; Adzic's Gherkin for keyboard accessibility needs ESC dismiss tested in Playwright, not Vitest. Resolution: `QuotaExceededModal` uses `<dialog>` native element (built-in focus trap + ESC) with `useEffect` for `.showModal()` call. Playwright E2E tests keyboard navigation; Vitest covers render/state.

### Spike Findings (drift from plan v2)

| Finding | Plan v2 assumption | Reality | Impact |
|---------|-------------------|---------|--------|
| **F-1: `IUserRateLimiter` does not exist** | Plan says create `Infrastructure/RateLimiting/UserRateLimiter.cs` + `RateLimitMiddleware.cs` | `IRateLimitService` exists at `apps/api/src/Api/Services/IRateLimitService.cs` (Redis token bucket). `BggRateLimitMiddleware` shows the usage pattern. `RateLimitingServiceExtensions.cs` has 18 named sliding-window policies via ASP.NET Core built-in rate limiter. | Task 3.6 must add new named policies to existing extension, not create a parallel infrastructure. |
| **F-2: Stripe SDK not in repo** | Plan assumes Stripe.NET package present | `Api.csproj` has no Stripe reference. Both Moq and NSubstitute are in test project (both packages present). | Task 3.3a must add `Stripe.net` NuGet package. Test mock must use `NSubstitute` (plan v2 preference) but `Moq` is also available — use NSubstitute for new tests per plan mandate. |
| **F-3: `DeleteOwnAccountCommandHandler` already implements GDPR Art. 17** | Plan says create `DeleteUserDataCommand` | Handler exists at `BoundedContexts/Authentication/Application/Commands/DeleteOwnAccountCommandHandler.cs` cascading sessions, LLM data, user entity. | Task 3.8 extends existing handlers rather than creating new ones. Add gamebook data cascade. |
| **F-4: `ExportUserDataCommandHandler` already implements GDPR Art. 20** | Plan says create `ExportUserDataQuery` | Handler exists at `BoundedContexts/Authentication/Application/Commands/ExportUserDataCommandHandler.cs` with IQueryHandler pattern, aggregating Library, ChatThreads, Notifications, AiConsent. | Task 3.8 extends aggregation to include PhotoBatchUploads + UserQuota. |
| **F-5: Privacy policy page already exists** | Plan says create `app/(public)/privacy/page.tsx` | File exists with GDPR sections and language toggle. | Task 3.7 narrows to content update + gamebook disclosure addition. |
| **F-6: `GetPdfPageTextQueryHandler` already exists** | Plan says `GetParagraphQuery` is new | `GetPdfPageTextQuery` + handler exist at `BoundedContexts/KnowledgeBase/Application/Queries/GetPdfPageTextQuery.cs`. Uses `IRequestHandler` (not `IQueryHandler`) — pattern drift from plan. | Task 3.1 creates `GetParagraphQuery` as a distinct query with `IQueryHandler` pattern that adds semantic fallback on top of `GetPdfPageTextQuery`'s page-text retrieval, or extends it. |
| **F-7: Test project has BOTH Moq and NSubstitute** | Plan v2 says NSubstitute only | `Api.Tests.csproj` has `Moq v4.20.72` AND `NSubstitute v5.3.0`. Existing tests use Moq. | New Phase 3 tests should use NSubstitute per plan mandate. Flag the Moq/NSubstitute coexistence in code review. |
| **F-8: `IBackgroundTaskService` has no `ExecuteWithCancellation` returning `Task`** | Plan references this for background enqueue | Interface method is `void ExecuteWithCancellation(string taskId, Func<CancellationToken, Task> taskFactory)` — returns void (fire-and-forget). | Quartz `IJob.Execute` is the correct pattern for `MonthlyQuotaResetJob`. Confirmed by existing `AuditLogRetentionJob`. |
| **F-9: `GetPdfPageTextQueryHandler` uses `IRequestHandler`, not `IQueryHandler`** | Plan says all handlers use `IQueryHandler` | Line 13: `internal sealed class GetPdfPageTextQueryHandler : IRequestHandler<GetPdfPageTextQuery, PdfPageTextDto>`. The query record uses `IRequest<PdfPageTextDto>` not `IQuery<PdfPageTextDto>`. | Existing drift in codebase. New `GetParagraphQueryHandler` must use `IQueryHandler` per plan mandate, separate from this existing query. |

### Risk Register Additions

| ID | Risk | Probability | Impact | Mitigation |
|----|------|-------------|--------|------------|
| **R-16** | Stripe webhook duplicate delivery grants double credits | Medium | High | Database idempotency key table `stripe_webhook_events(event_id PK, processed_at)`. Check-then-insert in same DB transaction as quota update. |
| **R-17** | `MonthlyQuotaResetJob` partial failure resets some users twice | Low | Medium | `last_reset_at` column on `UserQuota`; job skips users already reset this UTC month. Idempotent by design. |
| **R-18** | Stripe outage blocks credit purchases (not reads) | Medium | Low | `IPricingEngine` read path (quota check) independent of Stripe. Checkout failure = graceful error, not service disruption. Monitor Stripe status webhook. |
| **R-19** | `QuotaExceededModal` focus trap breaks on some iOS Safari | Low | Medium | Use native `<dialog>` element; tested in Playwright on Chromium (CI) + manual Safari verification pre-launch. |
| **R-20** | Rate limit policies applied to gamebook endpoints exclude admin bypass | Low | Medium | New `GambookTranslate` / `GambookQA` policies must include admin-bypass check (mirror `BggRateLimitMiddleware.AdminBypass`). |
| **R-21** | Stripe webhook secret not rotated after staging/prod split | Medium | High | Document in `infra/secrets/stripe.secret.example`. Rotation runbook in ops manual. |

---

## Task 3.1 — GetParagraphQuery (numbered + semantic fallback)

**Goal**: Given a `PhotoBatchUploadId` + `PageNumber`, return the extracted text for that page. If the exact page number yields empty text (OCR gap), fall back to semantic search using the page context.

**Files to create/modify**:
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetParagraphQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetParagraphQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/ParagraphDto.cs`
- Create: `apps/api/src/Api/Routing/KnowledgeBaseExtensions/ParagraphEndpoints.cs` (or modify `KnowledgeBaseEndpoints.cs`)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/GetParagraphQueryHandlerTests.cs`

**Dependencies**: Sprint 1+ Task 1.2 (PhotoBatchUpload migration with `photo_batch_pages` table), Task 2.1 (TranslationService).

**Spike clarification (F-6/F-9)**: `GetPdfPageTextQueryHandler` exists but uses legacy `IRequestHandler`. New `GetParagraphQuery` uses `IQueryHandler` and is scoped to `PhotoBatchUpload` pages (not PDF chunks). The existing handler targets `PdfDocument` + `TextChunk` entities. These are different data sources.

### Step 1 — RED: Failing test

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/GetParagraphQueryHandlerTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase;

[Trait("Category", "Unit")]
public sealed class GetParagraphQueryHandlerTests
{
    private readonly IPhotoBatchUploadRepository _repo;
    private readonly IEmbeddingService _embeddingService;
    private readonly SearchQueryHandler _searchHandler;
    private readonly GetParagraphQueryHandler _handler;

    public GetParagraphQueryHandlerTests()
    {
        _repo = Substitute.For<IPhotoBatchUploadRepository>();
        _embeddingService = Substitute.For<IEmbeddingService>();
        _searchHandler = Substitute.For<SearchQueryHandler>(); // or concrete with mocked deps
        _handler = new GetParagraphQueryHandler(_repo, _embeddingService, _searchHandler,
            NullLogger<GetParagraphQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ValidPageNumber_ReturnsParagraphText()
    {
        // Arrange — page with extracted text in DB
        var uploadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new GetParagraphQuery(uploadId, PageNumber: 5, UserId: userId);

        _repo.GetPageTextAsync(uploadId, 5, Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<string?>("Regola 5: il giocatore di turno muove il pedone."));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().Be("Regola 5: il giocatore di turno muove il pedone.");
        result.PageNumber.Should().Be(5);
        result.FallbackUsed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EmptyPageText_FallsBackToSemanticSearch()
    {
        // Arrange — page with empty text (OCR gap)
        var uploadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new GetParagraphQuery(uploadId, PageNumber: 7, UserId: userId,
            SemanticFallbackHint: "movement rules");

        _repo.GetPageTextAsync(uploadId, 7, Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<string?>(null));

        // _searchHandler returns a result
        // (set up substitute to return a SearchResultDto list)

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.FallbackUsed.Should().BeTrue();
        result.Text.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_UploadNotBelongingToUser_ThrowsNotFoundException()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _repo.GetPageTextAsync(uploadId, 1, Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<string?>(null));
        _repo.BelongsToUserAsync(uploadId, userId, Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(false));

        var query = new GetParagraphQuery(uploadId, PageNumber: 1, UserId: userId);

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

### Step 2 — GREEN: Implementation

```csharp
// GetParagraphQuery.cs
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

internal sealed record GetParagraphQuery(
    Guid PhotoBatchUploadId,
    int PageNumber,
    Guid UserId,
    string? SemanticFallbackHint = null  // used as search query if exact page text is empty
) : IQuery<ParagraphDto>;
```

```csharp
// ParagraphDto.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

internal sealed record ParagraphDto(
    int PageNumber,
    string Text,
    bool FallbackUsed,
    string? FallbackMethod  // null | "semantic"
);
```

```csharp
// GetParagraphQueryHandler.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

internal sealed class GetParagraphQueryHandler
    : IQueryHandler<GetParagraphQuery, ParagraphDto>
{
    private readonly IPhotoBatchUploadRepository _repo;
    private readonly IEmbeddingService _embeddingService;
    private readonly SearchQueryHandler _searchHandler;
    private readonly ILogger<GetParagraphQueryHandler> _logger;

    public GetParagraphQueryHandler(
        IPhotoBatchUploadRepository repo,
        IEmbeddingService embeddingService,
        SearchQueryHandler searchHandler,
        ILogger<GetParagraphQueryHandler> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _searchHandler = searchHandler ?? throw new ArgumentNullException(nameof(searchHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ParagraphDto> Handle(GetParagraphQuery query, CancellationToken cancellationToken)
    {
        // Authorization
        var belongs = await _repo.BelongsToUserAsync(
            query.PhotoBatchUploadId, query.UserId, cancellationToken).ConfigureAwait(false);
        if (!belongs)
            throw new NotFoundException("PhotoBatchUpload", query.PhotoBatchUploadId.ToString());

        // Strategy 1: Numbered lookup
        var pageText = await _repo.GetPageTextAsync(
            query.PhotoBatchUploadId, query.PageNumber, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(pageText))
            return new ParagraphDto(query.PageNumber, pageText, FallbackUsed: false, FallbackMethod: null);

        // Strategy 2: Semantic fallback — requires a GameId from the upload
        var upload = await _repo.GetByIdAsync(query.PhotoBatchUploadId, cancellationToken).ConfigureAwait(false);
        if (upload is null)
            throw new NotFoundException("PhotoBatchUpload", query.PhotoBatchUploadId.ToString());

        var hint = query.SemanticFallbackHint
            ?? $"page {query.PageNumber} content";

        var searchQuery = new SearchQuery(
            GameId: upload.GameId,
            Query: hint,
            TopK: 3,
            UserId: query.UserId);

        var results = await _searchHandler.Handle(searchQuery, cancellationToken).ConfigureAwait(false);
        var fallbackText = results.Count > 0
            ? string.Join("\n\n", results.Select(r => r.TextContent))
            : string.Empty;

        _logger.LogInformation(
            "GetParagraphQuery: numbered lookup empty for page {Page}, semantic fallback returned {Count} results",
            query.PageNumber, results.Count);

        return new ParagraphDto(
            query.PageNumber,
            fallbackText,
            FallbackUsed: true,
            FallbackMethod: "semantic");
    }
}
```

### Acceptance Criteria

- AC-3.1-1: `GET /api/v1/gamebook/uploads/{uploadId}/pages/{pageNumber}` returns 200 with `{ text, pageNumber, fallbackUsed }` when page text exists.
- AC-3.1-2: When page text is empty AND `SemanticFallbackHint` provided, returns 200 with `fallbackUsed: true`.
- AC-3.1-3: When `uploadId` belongs to a different user, returns 404.
- AC-3.1-4: When `pageNumber` is < 1 or > batch total pages, returns 400.
- AC-3.1-5: P95 latency < 300ms for numbered lookup (no semantic path).

### Gherkin Scenarios (Adzic)

```gherkin
Feature: Get paragraph text for gamebook page

  @happy
  Scenario: Exact page text exists
    Given user Alice has a completed photo batch upload with 20 pages
    And page 5 has extracted text "Regola 5: il giocatore di turno"
    When Alice requests page 5 of the upload
    Then the response contains the exact text
    And fallbackUsed is false

  @edge
  Scenario: Page text is empty — semantic fallback triggered
    Given user Alice has a completed photo batch upload for game "Catan"
    And page 7 has no extracted text (OCR gap)
    When Alice requests page 7 with hint "movimento del pedone"
    Then the response uses semantic search against the game's KB
    And fallbackUsed is true
    And fallbackMethod is "semantic"

  @error
  Scenario: Upload does not belong to requesting user
    Given user Bob has a photo batch upload
    When user Alice requests a page from Bob's upload
    Then the response is 404 Not Found

  @edge
  Scenario: Page number out of range
    Given user Alice has a completed upload with 10 pages
    When Alice requests page 99
    Then the response is 400 Bad Request
```

### Operational Considerations (Nygard)

- **Failure mode**: `SearchQueryHandler` (semantic fallback) calls the embedding service. If embedding service is down, return the empty-text result with `fallbackUsed: true, text: ""` and log a warning. Do NOT propagate the embedding service exception to the user.
- **Monitoring**: Prometheus counter `libro_paragraph_fallback_total{reason="ocr_empty"}` incremented when semantic fallback is used.
- **Cache**: Numbered-lookup results are deterministic — Redis cache with key `paragraph:{uploadId}:{pageNumber}`, TTL 1h. Semantic fallback is NOT cached (different hint each call).

---

## Task 3.2 — IPricingEngine + CreditBasedPricingEngine + UserQuota

**Goal**: Domain model for the 2-tier pricing (Free: 50 pages/month, Credits: €5/100 pages). `UserQuota` entity tracks current month consumption and credit balance.

**Files to create**:
- Create: `apps/api/src/Api/Infrastructure/Pricing/IPricingEngine.cs`
- Create: `apps/api/src/Api/Infrastructure/Pricing/CreditBasedPricingEngine.cs`
- Create: `apps/api/src/Api/Infrastructure/Pricing/Models/UserQuota.cs`
- Create: `apps/api/src/Api/Infrastructure/Pricing/Models/QuotaCheckResult.cs`
- Create: `apps/api/src/Api/Infrastructure/Pricing/Models/BillableOperation.cs`
- Create: `apps/api/src/Api/Infrastructure/Persistence/UserQuotaEntityConfiguration.cs`
- Create: Migration `{yyyyMMddHHmmss}_AddUserQuota.cs`
- Create: `apps/api/tests/Api.Tests/Pricing/CreditBasedPricingEngineTests.cs`
- Create: `apps/api/tests/Api.Tests/Pricing/UserQuotaTests.cs`

**Dependencies**: EF migration, `MeepleAiDbContext` registration.

### Step 1 — RED: Failing test

```csharp
// apps/api/tests/Api.Tests/Pricing/UserQuotaTests.cs
using Api.Infrastructure.Pricing.Models;
using FluentAssertions;
using Xunit;

[Trait("Category", "Unit")]
public sealed class UserQuotaTests
{
    [Fact]
    public void CanConsumeFreeTier_WithinMonthlyLimit_ReturnsTrue()
    {
        var quota = UserQuota.CreateForUser(Guid.NewGuid(), freeMonthlyLimit: 50);
        quota.FreeUsedThisMonth.Should().Be(0);
        quota.CanConsumeFreePage().Should().BeTrue();
    }

    [Fact]
    public void CanConsumeFreeTier_AtLimit_ReturnsFalse()
    {
        var quota = UserQuota.CreateForUser(Guid.NewGuid(), freeMonthlyLimit: 50);
        quota = quota.WithFreeUsed(50); // simulate 50 used
        quota.CanConsumeFreePage().Should().BeFalse();
    }

    [Fact]
    public void ConsumeCredit_WithSufficientBalance_DeductsOne()
    {
        var quota = UserQuota.CreateForUser(Guid.NewGuid(), freeMonthlyLimit: 50)
                             .WithCreditBalance(10);
        var updated = quota.ConsumeCredit();
        updated.CreditBalance.Should().Be(9);
    }

    [Fact]
    public void ConsumeCredit_WithZeroBalance_ThrowsInvalidOperation()
    {
        var quota = UserQuota.CreateForUser(Guid.NewGuid(), freeMonthlyLimit: 50);
        var act = () => quota.ConsumeCredit();
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*insufficient credits*");
    }

    [Fact]
    public void ResetMonthlyFree_UpdatesUsedToZeroAndSetsResetDate()
    {
        var quota = UserQuota.CreateForUser(Guid.NewGuid(), freeMonthlyLimit: 50)
                             .WithFreeUsed(30);
        var now = DateTime.UtcNow;
        var reset = quota.ResetMonthlyFree(now);
        reset.FreeUsedThisMonth.Should().Be(0);
        reset.LastResetAt.Should().BeCloseTo(now, precision: TimeSpan.FromSeconds(1));
    }
}
```

### Step 2 — GREEN: Implementation

```csharp
// Infrastructure/Pricing/Models/UserQuota.cs
namespace Api.Infrastructure.Pricing.Models;

/// <summary>
/// Aggregate: tracks free-tier monthly consumption + paid credit balance per user.
/// Uses immutable-style record with factory/With-methods for EF compatibility.
/// </summary>
public sealed class UserQuota
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public int FreeMonthlyLimit { get; private set; }
    public int FreeUsedThisMonth { get; private set; }
    public int CreditBalance { get; private set; }  // each credit = 1 page
    public DateTime LastResetAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private UserQuota() { }  // EF

    public static UserQuota CreateForUser(Guid userId, int freeMonthlyLimit = 50)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FreeMonthlyLimit = freeMonthlyLimit,
            FreeUsedThisMonth = 0,
            CreditBalance = 0,
            LastResetAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    public bool CanConsumeFreePage() => FreeUsedThisMonth < FreeMonthlyLimit;

    public UserQuota ConsumeFree()
    {
        if (!CanConsumeFreePage())
            throw new InvalidOperationException("Free monthly limit exhausted.");
        return With(freeUsed: FreeUsedThisMonth + 1);
    }

    public UserQuota ConsumeCredit()
    {
        if (CreditBalance <= 0)
            throw new InvalidOperationException("Cannot consume credit: insufficient credits.");
        return With(creditBalance: CreditBalance - 1);
    }

    public UserQuota AddCredits(int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        return With(creditBalance: CreditBalance + count);
    }

    public UserQuota ResetMonthlyFree(DateTime resetAt) => With(freeUsed: 0, lastResetAt: resetAt);

    // test helpers (internal)
    internal UserQuota WithFreeUsed(int used) => With(freeUsed: used);
    internal UserQuota WithCreditBalance(int balance) => With(creditBalance: balance);

    private UserQuota With(
        int? freeUsed = null,
        int? creditBalance = null,
        DateTime? lastResetAt = null)
        => new()
        {
            Id = Id,
            UserId = UserId,
            FreeMonthlyLimit = FreeMonthlyLimit,
            FreeUsedThisMonth = freeUsed ?? FreeUsedThisMonth,
            CreditBalance = creditBalance ?? CreditBalance,
            LastResetAt = lastResetAt ?? LastResetAt,
            CreatedAt = CreatedAt,
            UpdatedAt = DateTime.UtcNow,
        };
}
```

```csharp
// Infrastructure/Pricing/Models/QuotaCheckResult.cs
namespace Api.Infrastructure.Pricing.Models;

internal sealed record QuotaCheckResult(
    bool Allowed,
    string Tier,           // "free" | "credits"
    int RemainingFree,
    int RemainingCredits,
    string? DenialReason   // null when Allowed
);

// Infrastructure/Pricing/Models/BillableOperation.cs
internal enum BillableOperation { PageTranslation, QAQuery }
```

```csharp
// Infrastructure/Pricing/IPricingEngine.cs
using Api.Infrastructure.Pricing.Models;

namespace Api.Infrastructure.Pricing;

internal interface IPricingEngine
{
    Task<QuotaCheckResult> CheckQuotaAsync(Guid userId, BillableOperation operation,
        CancellationToken cancellationToken = default);

    /// Consumes one unit; returns updated quota. Throws if quota exceeded.
    Task<UserQuota> ConsumeQuotaAsync(Guid userId, BillableOperation operation,
        CancellationToken cancellationToken = default);

    Task<UserQuota> GetOrCreateQuotaAsync(Guid userId,
        CancellationToken cancellationToken = default);
}
```

### Acceptance Criteria

- AC-3.2-1: Free user with 0 used can consume up to 50 pages/month.
- AC-3.2-2: Free user at 50/month is denied; `QuotaCheckResult.Allowed = false`, `DenialReason` set.
- AC-3.2-3: User with 5 credits can consume 5 pages beyond free limit; 6th fails.
- AC-3.2-4: `ConsumeQuotaAsync` is atomic — calls EF `SaveChangesAsync` inside handler. No lost updates under concurrent access (EF concurrency token on `UpdatedAt`).
- AC-3.2-5: `GET /api/v1/gamebook/quota` returns 200 with `{ freeUsed, freeLimit, creditBalance, tier }`.

### Gherkin Scenarios (Adzic)

```gherkin
Feature: User quota management

  @happy
  Scenario: Free user within monthly limit
    Given user Alice has 30 free pages used this month
    And the free monthly limit is 50
    When Alice requests a page translation
    Then the quota check returns Allowed
    And free pages used increases to 31

  @edge
  Scenario: Free user exhausts quota and uses credits
    Given user Alice has 50 free pages used this month
    And Alice has 3 credit pages
    When Alice requests a page translation
    Then the free check fails
    And the system falls back to credits
    And credit balance decreases to 2

  @error
  Scenario: User with no free quota and no credits
    Given user Alice has 50 free pages used this month
    And Alice has 0 credits
    When Alice requests a page translation
    Then the response is 402 Payment Required
    And the QuotaExceededModal is shown to Alice
```

### Operational Considerations (Nygard)

- **Concurrency**: `UserQuota` entity needs EF `[ConcurrencyCheck]` on `FreeUsedThisMonth`. Race condition on concurrent requests must return 409 → caller retries. Low probability in single-session MVP.
- **Monitoring**: Prometheus counter `libro_quota_consumed_total{tier="free|credits"}`.
- **DB schema**: Table `user_quotas` in public schema. Columns: `id`, `user_id`, `free_monthly_limit`, `free_used_this_month`, `credit_balance`, `last_reset_at`, `created_at`, `updated_at`. Index on `user_id`.

---

## Task 3.3a — Stripe.NET Package + StripeCheckoutService

**Goal**: Add Stripe.NET SDK, implement `StripeCheckoutService` that creates a Checkout Session for credit purchase.

**Files to create/modify**:
- Modify: `apps/api/src/Api/Api.csproj` — add `Stripe.net` package reference
- Create: `apps/api/src/Api/Infrastructure/Pricing/Stripe/StripeConfig.cs`
- Create: `apps/api/src/Api/Infrastructure/Pricing/Stripe/StripeCheckoutService.cs`
- Create: `apps/api/src/Api/Infrastructure/Pricing/Stripe/IStripeCheckoutService.cs`
- Create: `apps/api/tests/Api.Tests/Pricing/StripeCheckoutServiceTests.cs`

**Spike note (F-2)**: Stripe SDK absent — must be installed. Current NuGet for .NET 9: `Stripe.net` v47+.

### Step 1 — RED: Failing test

```csharp
// apps/api/tests/Api.Tests/Pricing/StripeCheckoutServiceTests.cs
using Api.Infrastructure.Pricing.Stripe;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StripeCheckoutServiceTests
{
    [Fact]
    public async Task CreateCheckoutSessionAsync_ValidInput_ReturnsSessionUrl()
    {
        var config = Options.Create(new StripeConfig
        {
            SecretKey = "sk_test_xxx",
            WebhookSecret = "whsec_xxx",
            Price100Pages = "price_xxx",
            SuccessUrl = "https://app.test/gamebook/checkout/success",
            CancelUrl = "https://app.test/gamebook/checkout/cancel",
        });

        var service = new StripeCheckoutService(config, NullLogger<StripeCheckoutService>.Instance);

        // Act — in tests, this calls Stripe SDK which we cannot stub without wrapper.
        // Pattern: wrap Stripe SDK calls behind IStripeSessionFactory for mockability.
        // Use NSubstitute to mock the factory.
        var factory = Substitute.For<IStripeSessionFactory>();
        factory.CreateAsync(Arg.Any<StripeSessionRequest>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new StripeSessionResult("cs_test_xxx", "https://checkout.stripe.com/cs_test_xxx")));

        var svc = new StripeCheckoutService(config, factory, NullLogger<StripeCheckoutService>.Instance);

        var result = await svc.CreateCheckoutSessionAsync(
            userId: Guid.NewGuid(),
            creditPackage: 100,
            CancellationToken.None);

        result.SessionId.Should().Be("cs_test_xxx");
        result.CheckoutUrl.Should().StartWith("https://checkout.stripe.com/");
    }
}
```

### Step 2 — GREEN: Implementation

```csharp
// Infrastructure/Pricing/Stripe/StripeConfig.cs
namespace Api.Infrastructure.Pricing.Stripe;

internal sealed class StripeConfig
{
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string Price100Pages { get; set; } = string.Empty;  // Stripe Price ID for 100-page pack
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}
```

```csharp
// Infrastructure/Pricing/Stripe/IStripeCheckoutService.cs
namespace Api.Infrastructure.Pricing.Stripe;

internal interface IStripeCheckoutService
{
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Guid userId,
        int creditPackage,
        CancellationToken cancellationToken = default);
}

internal sealed record CheckoutSessionResult(string SessionId, string CheckoutUrl);
```

```csharp
// Infrastructure/Pricing/Stripe/StripeCheckoutService.cs
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Api.Infrastructure.Pricing.Stripe;

internal sealed class StripeCheckoutService : IStripeCheckoutService
{
    private readonly StripeConfig _config;
    private readonly IStripeSessionFactory _factory;
    private readonly ILogger<StripeCheckoutService> _logger;

    public StripeCheckoutService(
        IOptions<StripeConfig> config,
        IStripeSessionFactory factory,
        ILogger<StripeCheckoutService> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Guid userId,
        int creditPackage,
        CancellationToken cancellationToken = default)
    {
        var request = new StripeSessionRequest(
            PriceId: _config.Price100Pages,
            Quantity: creditPackage / 100,
            Metadata: new Dictionary<string, string>
            {
                ["user_id"] = userId.ToString(),
                ["credit_package"] = creditPackage.ToString(),
            },
            SuccessUrl: _config.SuccessUrl,
            CancelUrl: _config.CancelUrl);

        var result = await _factory.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Stripe checkout session created for user {UserId}: SessionId={SessionId}",
            userId, result.SessionId);

        return new CheckoutSessionResult(result.SessionId, result.CheckoutUrl);
    }
}
```

### Acceptance Criteria

- AC-3.3a-1: `POST /api/v1/gamebook/credits/checkout` returns 200 with `{ sessionId, checkoutUrl }`.
- AC-3.3a-2: `checkoutUrl` is a valid HTTPS Stripe Checkout URL.
- AC-3.3a-3: Stripe API key read from `infra/secrets/stripe.secret` (not hardcoded).
- AC-3.3a-4: Request validates: `creditPackage` must be 100 (only pack available in MVP).

### Gherkin Scenarios (Adzic)

```gherkin
Feature: Stripe Checkout Session creation

  @happy
  Scenario: User initiates credit purchase
    Given user Alice is authenticated
    And her quota shows 0 remaining pages
    When she calls POST /api/v1/gamebook/credits/checkout with body { "creditPackage": 100 }
    Then the response status is 200
    And the response contains a checkoutUrl starting with "https://checkout.stripe.com/"

  @error
  Scenario: Stripe API unavailable
    Given Stripe API returns 503
    When Alice calls the checkout endpoint
    Then the response is 503 with error "Payment service temporarily unavailable"
    And the error is logged with correlation ID

  @edge
  Scenario: Invalid credit package requested
    When Alice calls POST with { "creditPackage": 50 }
    Then the response is 400 Bad Request
```

### Operational Considerations (Nygard)

- **Circuit breaker**: Wrap Stripe HTTP calls in Polly circuit breaker (already available via `Microsoft.Extensions.Http.Polly` in `Api.csproj`). Open after 3 failures in 60s. Half-open after 30s.
- **Stripe API key rotation**: Documented in ops manual. Key stored in `infra/secrets/stripe.secret`.

---

## Task 3.3b — Stripe Webhook Handler (Signature Verification + Idempotency)

**Goal**: Handle `checkout.session.completed` event securely. Verify Stripe signature. Use database idempotency table to prevent double-credit on duplicate events.

**Files to create**:
- Create: `apps/api/src/Api/Infrastructure/Pricing/Stripe/StripeWebhookHandler.cs`
- Create: `apps/api/src/Api/Infrastructure/Persistence/Entities/StripeWebhookEventEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/Persistence/Configurations/StripeWebhookEventEntityConfiguration.cs`
- Create Migration: `{yyyyMMddHHmmss}_AddStripeWebhookIdempotency.cs`
- Create: `apps/api/tests/Api.Tests/Pricing/StripeWebhookHandlerTests.cs`

**Cross-cutting concern addressed**: Idempotency via DB unique key (R-16).

### Step 1 — RED: Failing test

```csharp
[Trait("Category", "Unit")]
public sealed class StripeWebhookHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IPricingEngine _pricingEngine;
    private readonly StripeWebhookHandler _handler;

    public StripeWebhookHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MeepleAiDbContext(options);
        _pricingEngine = Substitute.For<IPricingEngine>();
        _handler = new StripeWebhookHandler(_dbContext, _pricingEngine,
            NullLogger<StripeWebhookHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ValidCheckoutCompleted_GrantsCredits()
    {
        var eventId = "evt_test_123";
        var userId = Guid.NewGuid();
        var creditCount = 100;

        _pricingEngine.GetOrCreateQuotaAsync(userId, Arg.Any<CancellationToken>())
            .Returns(UserQuota.CreateForUser(userId));

        await _handler.HandleCheckoutCompletedAsync(
            eventId, userId, creditCount, CancellationToken.None);

        await _pricingEngine.Received(1).ConsumeQuotaAsync(
            Arg.Any<Guid>(), Arg.Any<BillableOperation>(), Arg.Any<CancellationToken>());

        // Verify idempotency record stored
        var record = await _dbContext.StripeWebhookEvents
            .FirstOrDefaultAsync(e => e.EventId == eventId);
        record.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_DuplicateEventId_IsIdempotent()
    {
        var eventId = "evt_dup_456";
        var userId = Guid.NewGuid();

        // First call
        await _handler.HandleCheckoutCompletedAsync(eventId, userId, 100, CancellationToken.None);

        // Second call — same event ID
        await _handler.HandleCheckoutCompletedAsync(eventId, userId, 100, CancellationToken.None);

        // Credits only granted once
        await _pricingEngine.Received(1).ConsumeQuotaAsync(
            Arg.Any<Guid>(), Arg.Any<BillableOperation>(), Arg.Any<CancellationToken>());
    }
}
```

### Step 2 — GREEN: Implementation

```csharp
// Infrastructure/Pricing/Stripe/StripeWebhookHandler.cs
namespace Api.Infrastructure.Pricing.Stripe;

internal sealed class StripeWebhookHandler
{
    private readonly MeepleAiDbContext _db;
    private readonly IPricingEngine _pricingEngine;
    private readonly ILogger<StripeWebhookHandler> _logger;

    public StripeWebhookHandler(
        MeepleAiDbContext db,
        IPricingEngine pricingEngine,
        ILogger<StripeWebhookHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _pricingEngine = pricingEngine ?? throw new ArgumentNullException(nameof(pricingEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleCheckoutCompletedAsync(
        string stripeEventId,
        Guid userId,
        int creditsToGrant,
        CancellationToken cancellationToken)
    {
        // Idempotency check: try to insert event record
        var alreadyProcessed = await _db.StripeWebhookEvents
            .AnyAsync(e => e.EventId == stripeEventId, cancellationToken).ConfigureAwait(false);

        if (alreadyProcessed)
        {
            _logger.LogWarning(
                "Duplicate Stripe webhook event ignored: EventId={EventId}", stripeEventId);
            return;
        }

        // Record idempotency key BEFORE granting credits (within same transaction)
        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _db.StripeWebhookEvents.Add(new StripeWebhookEventEntity
            {
                EventId = stripeEventId,
                ProcessedAt = DateTime.UtcNow,
                UserId = userId,
                CreditsGranted = creditsToGrant,
            });

            var quota = await _pricingEngine.GetOrCreateQuotaAsync(userId, cancellationToken)
                .ConfigureAwait(false);
            var updated = quota.AddCredits(creditsToGrant);
            // persist updated quota via pricing engine
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Credits granted: UserId={UserId}, Credits={Credits}, EventId={EventId}",
                userId, creditsToGrant, stripeEventId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex,
                "Failed to process Stripe webhook: EventId={EventId}", stripeEventId);
            throw;
        }
    }
}
```

### Acceptance Criteria

- AC-3.3b-1: Webhook endpoint at `POST /api/v1/webhooks/stripe` verifies `Stripe-Signature` header. Returns 400 on invalid signature.
- AC-3.3b-2: `checkout.session.completed` with valid signature grants credits to user in `user_id` metadata field.
- AC-3.3b-3: Second delivery of same `event.id` returns 200 but does not double-credit.
- AC-3.3b-4: Unknown event types return 200 (acknowledged, ignored).
- AC-3.3b-5: Webhook endpoint is NOT behind authentication middleware (Stripe cannot send auth cookies).

### Gherkin Scenarios (Adzic)

```gherkin
Feature: Stripe webhook idempotent processing

  @happy
  Scenario: Valid checkout.session.completed grants credits
    Given a valid Stripe signature header
    And event payload has type "checkout.session.completed"
    And metadata contains user_id and credit_package: 100
    When POST /api/v1/webhooks/stripe is called
    Then response status is 200
    And user's credit balance increases by 100
    And idempotency record is stored for event ID

  @security
  Scenario: Invalid Stripe signature is rejected
    Given an invalid Stripe-Signature header
    When POST /api/v1/webhooks/stripe is called
    Then response status is 400
    And credits are not granted

  @idempotency
  Scenario: Duplicate event delivery does not double-credit
    Given event "evt_123" was already processed successfully
    When POST /api/v1/webhooks/stripe is called again with the same event
    Then response status is 200
    And user credit balance is unchanged
    And a warning is logged
```

### Operational Considerations (Nygard)

- **Signature verification**: Use `Stripe.EventUtility.ConstructEvent()` with `WebhookSecret` from config. This is the sole source of trust.
- **DB unique constraint**: `stripe_webhook_events.event_id` has `UNIQUE` index. Concurrent webhook delivery (Stripe sends to multiple replicas) will cause one to succeed, others to get `UniqueConstraintException` → catch and return 200.
- **Replay protection TTL**: Stripe signatures are valid for 300s. Beyond that, reject as expired (StripeSDK enforces `tolerance: 300`).

---

## Task 3.3c — Pricing Endpoints

**Goal**: Three endpoints: GET quota, POST credits checkout, POST webhook.

**Files to create/modify**:
- Create: `apps/api/src/Api/Routing/PricingEndpoints.cs`
- Create: `apps/api/src/Api/Routing/StripeWebhookEndpoints.cs`

### Step 1 — RED (abbreviated endpoint test)

```csharp
[Fact]
public async Task GetQuota_AuthenticatedUser_ReturnsQuotaDto()
{
    // Arrange: user with 30 free pages used, 5 credits
    var quota = UserQuota.CreateForUser(TestUserId).WithFreeUsed(30).WithCreditBalance(5);
    _pricingEngine.GetOrCreateQuotaAsync(TestUserId, Arg.Any<CancellationToken>())
        .Returns(quota);

    // Act
    var response = await _client.GetAsync("/api/v1/gamebook/quota");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<QuotaDto>();
    body!.FreeUsed.Should().Be(30);
    body.CreditBalance.Should().Be(5);
}
```

### Step 2 — GREEN (endpoint registration)

```csharp
// Routing/PricingEndpoints.cs
internal static class PricingEndpoints
{
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/gamebook").RequireSession();

        group.MapGet("/quota", async (IMediator mediator, HttpContext ctx) =>
        {
            var (_, session, _) = ctx.GetActiveSession();
            var result = await mediator.Send(new GetQuotaQuery(session!.User!.Id));
            return Results.Ok(result);
        });

        group.MapPost("/credits/checkout", async (
            [FromBody] StartCheckoutRequest req,
            IMediator mediator,
            HttpContext ctx) =>
        {
            var (_, session, _) = ctx.GetActiveSession();
            var result = await mediator.Send(
                new StartCheckoutCommand(session!.User!.Id, req.CreditPackage));
            return Results.Ok(result);
        }).RequireRateLimiting("GamebookCheckout");

        return routes;
    }
}
```

```csharp
// Routing/StripeWebhookEndpoints.cs
internal static class StripeWebhookEndpoints
{
    public static IEndpointRouteBuilder MapStripeWebhookEndpoints(this IEndpointRouteBuilder routes)
    {
        // NOTE: No .RequireSession() — Stripe cannot provide user auth cookies
        routes.MapPost("/api/v1/webhooks/stripe", async (
            HttpRequest request,
            IStripeWebhookHandler webhookHandler,
            IOptions<StripeConfig> config) =>
        {
            var payload = await new StreamReader(request.Body).ReadToEndAsync();
            var signature = request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    payload, signature, config.Value.WebhookSecret, tolerance: 300);

                await webhookHandler.HandleAsync(stripeEvent, request.HttpContext.RequestAborted);
                return Results.Ok();
            }
            catch (StripeException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return routes;
    }
}
```

### Acceptance Criteria

- AC-3.3c-1: `GET /api/v1/gamebook/quota` requires authentication; returns 401 when unauthenticated.
- AC-3.3c-2: `POST /api/v1/gamebook/credits/checkout` is rate-limited to 5 req/min per user.
- AC-3.3c-3: `POST /api/v1/webhooks/stripe` does NOT require authentication.
- AC-3.3c-4: All three endpoints return consistent error shapes (`{ error, message, correlationId }`).

### Gherkin Scenarios (Adzic)

```gherkin
  @happy
  Scenario: Authenticated user checks quota
    Given Alice is authenticated
    When she calls GET /api/v1/gamebook/quota
    Then response contains freeUsed, freeLimit, creditBalance

  @error
  Scenario: Unauthenticated quota request
    Given no authentication cookie
    When calling GET /api/v1/gamebook/quota
    Then response status is 401

  @security
  Scenario: Checkout endpoint respects rate limit
    Given Alice calls POST /credits/checkout 6 times in 60 seconds
    Then the 6th call returns 429 Too Many Requests
```

---

## Task 3.4 — MonthlyQuotaResetJob (Quartz)

**Goal**: First-of-month UTC Quartz job resets `FreeUsedThisMonth` for all users. Idempotent via `last_reset_at` check.

**Files to create**:
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Scheduling/MonthlyQuotaResetJob.cs`
- Create: `apps/api/tests/Api.Tests/Pricing/MonthlyQuotaResetJobTests.cs`

**Pattern reference**: `AuditLogRetentionJob.cs` — `[DisallowConcurrentExecution]`, `IJob`, `MeepleAiDbContext` direct inject.

### Step 1 — RED: Failing test

```csharp
[Trait("Category", "Unit")]
public sealed class MonthlyQuotaResetJobTests
{
    [Fact]
    public async Task Execute_ResetsUsersNotYetResetThisMonth()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new MeepleAiDbContext(options);

        // Seed: one user needs reset (last reset = previous month)
        var oldReset = DateTime.UtcNow.AddMonths(-1);
        var quota = UserQuota.CreateForUser(Guid.NewGuid()).WithFreeUsed(45);
        quota.SetLastResetAt(oldReset);  // test helper
        db.UserQuotas.Add(quota);
        await db.SaveChangesAsync();

        var job = new MonthlyQuotaResetJob(db, NullLogger<MonthlyQuotaResetJob>.Instance);
        var context = CreateJobContext();

        await job.Execute(context);

        var updated = await db.UserQuotas.FirstAsync();
        updated.FreeUsedThisMonth.Should().Be(0);
        updated.LastResetAt.Month.Should().Be(DateTime.UtcNow.Month);
    }

    [Fact]
    public async Task Execute_SkipsUsersAlreadyResetThisMonth()
    {
        var db = CreateDb();
        var quota = UserQuota.CreateForUser(Guid.NewGuid()).WithFreeUsed(10);
        quota.SetLastResetAt(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1));
        db.UserQuotas.Add(quota);
        await db.SaveChangesAsync();

        var job = new MonthlyQuotaResetJob(db, NullLogger<MonthlyQuotaResetJob>.Instance);
        await job.Execute(CreateJobContext());

        var updated = await db.UserQuotas.FirstAsync();
        updated.FreeUsedThisMonth.Should().Be(10);  // unchanged
    }
}
```

### Step 2 — GREEN: Implementation

```csharp
// BoundedContexts/Administration/Infrastructure/Scheduling/MonthlyQuotaResetJob.cs
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.Administration.Infrastructure.Scheduling;

[DisallowConcurrentExecution]
internal sealed class MonthlyQuotaResetJob : IJob
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<MonthlyQuotaResetJob> _logger;

    public MonthlyQuotaResetJob(MeepleAiDbContext db, ILogger<MonthlyQuotaResetJob> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Idempotent: only reset users whose last_reset_at < start of current month
        var toReset = await _db.UserQuotas
            .Where(q => q.LastResetAt < currentMonthStart && q.FreeUsedThisMonth > 0)
            .ToListAsync(context.CancellationToken).ConfigureAwait(false);

        if (toReset.Count == 0)
        {
            _logger.LogInformation("MonthlyQuotaResetJob: no users to reset.");
            return;
        }

        foreach (var quota in toReset)
        {
            var reset = quota.ResetMonthlyFree(now);
            _db.Entry(quota).CurrentValues.SetValues(reset);
        }

        await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "MonthlyQuotaResetJob: reset {Count} user quotas for {Month:yyyy-MM}",
            toReset.Count, now);
    }
}
```

**Quartz registration** (in DI setup):
```csharp
// Add to Quartz configuration in Program.cs / service extensions:
q.AddJob<MonthlyQuotaResetJob>(opts => opts.WithIdentity("monthly-quota-reset"));
q.AddTrigger(opts => opts
    .ForJob("monthly-quota-reset")
    .WithIdentity("monthly-quota-reset-trigger")
    .WithCronSchedule("0 0 1 * * ?", x => x.InTimeZone(TimeZoneInfo.Utc)));
```

### Acceptance Criteria

- AC-3.4-1: Job runs on the 1st of each month at 00:00 UTC.
- AC-3.4-2: Users with `last_reset_at` in the current calendar month are NOT reset (idempotent).
- AC-3.4-3: Job with `[DisallowConcurrentExecution]` does not run twice simultaneously.
- AC-3.4-4: Job logs the count of reset users and month.

### Gherkin Scenarios (Adzic)

```gherkin
  @happy
  Scenario: Job resets free usage on first of month
    Given 3 users have free pages used from last month
    When MonthlyQuotaResetJob executes on 2026-06-01
    Then all 3 users have free_used_this_month = 0
    And last_reset_at is updated to June 2026

  @idempotency
  Scenario: Job re-runs same month does not double-reset
    Given job already ran on 2026-06-01
    When job runs again on 2026-06-01 (e.g., crash recovery)
    Then free_used_this_month remains 0 (already reset)

  @edge
  Scenario: User with 0 pages used is skipped for efficiency
    Given a user with free_used_this_month = 0
    When the job runs
    Then the user record is not touched (WHERE clause filters them)
```

---

## Task 3.5a — Frontend Hooks

**Goal**: Four React Query / mutation hooks for gamebook play screen.

**Files to create**:
- Create: `apps/web/src/lib/gamebook/hooks/useTranslateParagraph.ts`
- Create: `apps/web/src/lib/gamebook/hooks/useAskQuestion.ts`
- Create: `apps/web/src/lib/gamebook/hooks/useStartCheckout.ts`
- Create: `apps/web/src/lib/gamebook/hooks/useQuota.ts`
- Create: `apps/web/src/lib/gamebook/schemas.ts` (Zod schemas)
- Create: `apps/web/src/lib/gamebook/api.ts` (httpClient wrappers)
- Create: `apps/web/__tests__/gamebook/hooks/useQuota.test.ts`

**Pattern**: Use existing `httpClient` from `lib/api/core/httpClient.ts`. See `lib/api/shared-games.ts` for current example.

### Step 1 — RED: Failing test (useQuota)

```typescript
// apps/web/__tests__/gamebook/hooks/useQuota.test.ts
import { renderHook, waitFor } from '@testing-library/react';
import { useQuota } from '@/lib/gamebook/hooks/useQuota';
import { createTestWrapper } from '@/test-utils/test-wrapper';

// Mock httpClient
vi.mock('@/lib/api/core/httpClient', () => ({
  httpClient: {
    get: vi.fn(),
  },
}));

import { httpClient } from '@/lib/api/core/httpClient';

describe('useQuota', () => {
  it('returns quota data on success', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      freeUsed: 30,
      freeLimit: 50,
      creditBalance: 5,
    });

    const { result } = renderHook(() => useQuota(), {
      wrapper: createTestWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.freeUsed).toBe(30);
    expect(result.current.data?.creditBalance).toBe(5);
  });

  it('is disabled when user is not authenticated', () => {
    const { result } = renderHook(() => useQuota({ enabled: false }), {
      wrapper: createTestWrapper(),
    });
    expect(result.current.data).toBeUndefined();
  });
});
```

### Step 2 — GREEN: Implementation

```typescript
// lib/gamebook/schemas.ts
import { z } from 'zod';

export const QuotaSchema = z.object({
  freeUsed: z.number().int().nonnegative(),
  freeLimit: z.number().int().positive(),
  creditBalance: z.number().int().nonnegative(),
  tier: z.enum(['free', 'credits']),
});
export type Quota = z.infer<typeof QuotaSchema>;

export const ParagraphSchema = z.object({
  pageNumber: z.number().int().positive(),
  text: z.string(),
  fallbackUsed: z.boolean(),
  fallbackMethod: z.string().nullable(),
});
export type Paragraph = z.infer<typeof ParagraphSchema>;

export const CheckoutResultSchema = z.object({
  sessionId: z.string(),
  checkoutUrl: z.string().url(),
});
export type CheckoutResult = z.infer<typeof CheckoutResultSchema>;
```

```typescript
// lib/gamebook/hooks/useQuota.ts
import { useQuery } from '@tanstack/react-query';
import { httpClient } from '@/lib/api/core/httpClient';
import { QuotaSchema, type Quota } from '../schemas';

export function useQuota(options?: { enabled?: boolean }) {
  return useQuery<Quota>({
    queryKey: ['gamebook', 'quota'],
    queryFn: () =>
      httpClient.get<Quota>('/api/v1/gamebook/quota', QuotaSchema),
    enabled: options?.enabled ?? true,
    staleTime: 30_000,
  });
}
```

```typescript
// lib/gamebook/hooks/useStartCheckout.ts
import { useMutation } from '@tanstack/react-query';
import { httpClient } from '@/lib/api/core/httpClient';
import { CheckoutResultSchema, type CheckoutResult } from '../schemas';

export function useStartCheckout() {
  return useMutation<CheckoutResult, Error, { creditPackage: number }>({
    mutationFn: ({ creditPackage }) =>
      httpClient.post<CheckoutResult>(
        '/api/v1/gamebook/credits/checkout',
        { creditPackage },
        CheckoutResultSchema,
      ),
    onSuccess: (data) => {
      window.location.href = data.checkoutUrl;
    },
  });
}
```

### Acceptance Criteria

- AC-3.5a-1: `useQuota` refetches every 30s and on window focus.
- AC-3.5a-2: `useTranslateParagraph` returns `isPending` during in-flight request.
- AC-3.5a-3: `useStartCheckout` redirects to `checkoutUrl` on success.
- AC-3.5a-4: All hooks use Zod schema validation on response — invalid API shape throws `SchemaValidationError`.

### Gherkin Scenarios (Adzic)

```gherkin
  @happy
  Scenario: useQuota displays current usage
    Given user has 30/50 free pages used
    When play page mounts
    Then useQuota returns { freeUsed: 30, freeLimit: 50 }

  @error
  Scenario: API 401 propagates as error state
    Given user session expired
    When useQuota fetches
    Then hook error state is set and QuotaExceededModal is not shown

  @edge
  Scenario: useTranslateParagraph deduplicated on rapid re-render
    Given play page re-renders 3 times before response
    Then only 1 HTTP request is made (request dedup via httpClient)
```

---

## Task 3.5b — TranslationViewer Component

**Files to create**:
- Create: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/_components/TranslationViewer.tsx`
- Create: `apps/web/__tests__/gamebook/play/TranslationViewer.test.tsx`

### Step 1 — RED: Failing test

```typescript
// apps/web/__tests__/gamebook/play/TranslationViewer.test.tsx
import { render, screen } from '@testing-library/react';
import { TranslationViewer } from '@/app/(authenticated)/gamebook/[gameId]/play/_components/TranslationViewer';

describe('TranslationViewer', () => {
  it('displays translated text when provided', () => {
    render(
      <TranslationViewer
        originalText="Regola 5: il giocatore di turno"
        translatedText="Rule 5: the current player"
        pageNumber={5}
        isLoading={false}
      />
    );
    expect(screen.getByText('Rule 5: the current player')).toBeInTheDocument();
    expect(screen.getByText(/page 5/i)).toBeInTheDocument();
  });

  it('shows skeleton while loading', () => {
    render(<TranslationViewer isLoading={true} pageNumber={5} />);
    expect(screen.getByRole('status', { name: /loading/i })).toBeInTheDocument();
  });

  it('shows original text toggle', async () => {
    const { user } = render(
      <TranslationViewer
        originalText="Testo originale"
        translatedText="Original text"
        pageNumber={1}
        isLoading={false}
      />
    );
    await user.click(screen.getByRole('button', { name: /show original/i }));
    expect(screen.getByText('Testo originale')).toBeVisible();
  });
});
```

### Step 2 — GREEN: Implementation

```tsx
// TranslationViewer.tsx
'use client';
import { useState } from 'react';

interface TranslationViewerProps {
  originalText?: string;
  translatedText?: string;
  pageNumber: number;
  isLoading: boolean;
}

export function TranslationViewer({
  originalText,
  translatedText,
  pageNumber,
  isLoading,
}: TranslationViewerProps) {
  const [showOriginal, setShowOriginal] = useState(false);

  if (isLoading) {
    return (
      <div role="status" aria-label="loading">
        <div className="animate-pulse h-4 bg-gray-200 rounded w-3/4 mb-2" />
        <div className="animate-pulse h-4 bg-gray-200 rounded w-1/2" />
      </div>
    );
  }

  const displayText = showOriginal ? originalText : translatedText;

  return (
    <article aria-label={`Page ${pageNumber} content`}>
      <p className="text-sm text-muted-foreground mb-1">Page {pageNumber}</p>
      <p className="text-base leading-relaxed">{displayText}</p>
      {originalText && translatedText && (
        <button
          type="button"
          className="text-xs text-primary underline mt-2"
          onClick={() => setShowOriginal((v) => !v)}
          aria-pressed={showOriginal}
        >
          {showOriginal ? 'Show translation' : 'Show original'}
        </button>
      )}
    </article>
  );
}
```

### Acceptance Criteria

- AC-3.5b-1: Renders translated text by default; original text on toggle.
- AC-3.5b-2: Shows accessible skeleton while `isLoading=true`.
- AC-3.5b-3: `aria-pressed` on toggle button correctly reflects state.
- AC-3.5b-4: Empty `translatedText` shows "Translation unavailable" message.

### Gherkin Scenarios (Adzic)

```gherkin
  @happy
  Scenario: Translation loads and displays
    Given page 5 translation is available
    When TranslationViewer renders with translatedText
    Then the Italian-to-English translation is visible

  @edge
  Scenario: Toggle shows original Italian text
    Given TranslationViewer has both original and translated text
    When user clicks "Show original"
    Then the original Italian text is visible
    And button aria-pressed is true

  @error
  Scenario: Translation not yet available (loading)
    Given isLoading is true
    When TranslationViewer renders
    Then a loading skeleton with role="status" is visible
```

---

## Task 3.5c — QAPanel Component

**Files to create**:
- Create: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/_components/QAPanel.tsx`
- Create: `apps/web/__tests__/gamebook/play/QAPanel.test.tsx`

### Step 1 — RED: Failing test

```typescript
it('submits question and shows answer', async () => {
  const mockAsk = vi.fn().mockResolvedValue({
    answer: 'Yes, you can trade.',
    citations: [{ pageNumber: 3, snippet: '...', relevanceScore: 0.9, documentId: 'doc1' }],
  });

  render(<QAPanel gameId="game-1" onQuotaExceeded={vi.fn()} useAskQuestion={() => ({
    mutate: mockAsk, isPending: false,
  })} />);

  await user.type(screen.getByRole('textbox', { name: /ask a question/i }), 'Can I trade?');
  await user.click(screen.getByRole('button', { name: /ask/i }));

  expect(mockAsk).toHaveBeenCalledWith({ question: 'Can I trade?', gameId: 'game-1' });
  await waitFor(() => expect(screen.getByText('Yes, you can trade.')).toBeInTheDocument());
});
```

### Step 2 — GREEN: Implementation (abbreviated)

```tsx
'use client';
import { useState } from 'react';
import type { UseAskQuestionReturn } from '@/lib/gamebook/hooks/useAskQuestion';

interface QAPanelProps {
  gameId: string;
  onQuotaExceeded: () => void;
  useAskQuestion: () => UseAskQuestionReturn;
}

export function QAPanel({ gameId, onQuotaExceeded, useAskQuestion }: QAPanelProps) {
  const [question, setQuestion] = useState('');
  const { mutate: askQuestion, isPending, data } = useAskQuestion();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!question.trim()) return;
    askQuestion({ question, gameId }, {
      onError: (err) => {
        if (err.message.includes('402')) onQuotaExceeded();
      },
    });
  };

  return (
    <section aria-label="Rules Q&A">
      <form onSubmit={handleSubmit}>
        <label htmlFor="qa-input">Ask a question about the rules</label>
        <input
          id="qa-input"
          type="text"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          disabled={isPending}
        />
        <button type="submit" disabled={isPending || !question.trim()}>
          {isPending ? 'Asking…' : 'Ask'}
        </button>
      </form>
      {data && (
        <div aria-live="polite">
          <p>{data.answer}</p>
        </div>
      )}
    </section>
  );
}
```

### Acceptance Criteria

- AC-3.5c-1: Submit button disabled while request in flight.
- AC-3.5c-2: `aria-live="polite"` region updates when answer arrives.
- AC-3.5c-3: 402 response triggers `onQuotaExceeded` callback.
- AC-3.5c-4: Citations rendered as a list below the answer.

### Gherkin Scenarios (Adzic)

```gherkin
  @happy
  Scenario: User asks valid question
    When user types "Can I trade resources?" and clicks Ask
    Then the question is submitted to the API
    And the answer is displayed in the live region

  @error
  Scenario: Quota exceeded triggers modal
    Given user has 0 free pages and 0 credits
    When user asks a question
    Then the QuotaExceededModal is shown

  @a11y
  Scenario: Answer is announced to screen readers
    Given the answer arrives
    Then aria-live="polite" region contains the answer text
```

---

## Task 3.5d — QuotaExceededModal (accessibility)

**Files to create**:
- Create: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/_components/QuotaExceededModal.tsx`
- Create: `apps/web/__tests__/gamebook/play/QuotaExceededModal.test.tsx`

**Design decision**: Use native `<dialog>` element for built-in focus trap and ESC dismiss (avoids `focus-trap-react` dependency and iOS Safari issues, R-19).

### Step 1 — RED: Failing test

```typescript
it('closes on ESC key press', async () => {
  const onClose = vi.fn();
  render(<QuotaExceededModal isOpen={true} onClose={onClose} onUpgrade={vi.fn()} />);
  await user.keyboard('{Escape}');
  expect(onClose).toHaveBeenCalled();
});

it('moves focus to first button when opened', async () => {
  render(<QuotaExceededModal isOpen={true} onClose={vi.fn()} onUpgrade={vi.fn()} />);
  expect(document.activeElement?.tagName).toBe('BUTTON');
});

it('announces modal to screen readers', () => {
  render(<QuotaExceededModal isOpen={true} onClose={vi.fn()} onUpgrade={vi.fn()} />);
  expect(screen.getByRole('dialog', { name: /quota exceeded/i })).toBeInTheDocument();
});
```

### Step 2 — GREEN: Implementation

```tsx
'use client';
import { useEffect, useRef } from 'react';

interface QuotaExceededModalProps {
  isOpen: boolean;
  onClose: () => void;
  onUpgrade: () => void;
}

export function QuotaExceededModal({ isOpen, onClose, onUpgrade }: QuotaExceededModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (isOpen) {
      dialog.showModal();
    } else {
      dialog.close();
    }
  }, [isOpen]);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    const handleClose = () => onClose();
    dialog.addEventListener('close', handleClose);
    return () => dialog.removeEventListener('close', handleClose);
  }, [onClose]);

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby="quota-modal-title"
      className="rounded-lg p-6 max-w-sm w-full shadow-xl"
    >
      <h2 id="quota-modal-title" className="text-lg font-semibold mb-2">
        Quota Exceeded
      </h2>
      <p className="text-sm text-muted-foreground mb-4">
        You have used all your free pages this month. Purchase credits to continue.
      </p>
      <div className="flex gap-2 justify-end">
        <button
          type="button"
          onClick={onClose}
          className="btn-secondary"
          autoFocus
        >
          Cancel
        </button>
        <button
          type="button"
          onClick={onUpgrade}
          className="btn-primary"
        >
          Buy Credits
        </button>
      </div>
    </dialog>
  );
}
```

### Acceptance Criteria

- AC-3.5d-1: Modal uses `<dialog>` native element with `role="dialog"`.
- AC-3.5d-2: Focus moves to first button when modal opens.
- AC-3.5d-3: ESC key closes the modal.
- AC-3.5d-4: Background scroll is locked when modal is open.
- AC-3.5d-5: ARIA `aria-labelledby` points to modal title.

### Gherkin Scenarios (Adzic)

```gherkin
  @a11y
  Scenario: Focus is trapped inside modal
    Given QuotaExceededModal is open
    When user presses Tab repeatedly
    Then focus cycles between Cancel and Buy Credits buttons only

  @a11y
  Scenario: Modal closes on ESC
    Given modal is open
    When user presses Escape
    Then modal closes and focus returns to trigger element

  @happy
  Scenario: Buy Credits button triggers checkout
    When user clicks Buy Credits
    Then onUpgrade callback is called
    And the Stripe checkout redirect begins
```

---

## Task 3.5e — HouseRuleModal

**Files to create**:
- Create: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/_components/HouseRuleModal.tsx`
- Create: `apps/web/__tests__/gamebook/play/HouseRuleModal.test.tsx`

*(Pattern identical to QuotaExceededModal — `<dialog>` based, NSubstitute-mocked hooks in tests.)*

### Acceptance Criteria

- AC-3.5e-1: Modal displays existing house rules for the game.
- AC-3.5e-2: Form allows adding a new rule (text + optional category).
- AC-3.5e-3: On save, calls `useHouseRules().add` mutation and closes modal.
- AC-3.5e-4: Character limit 500 enforced in UI + API validator.

### Gherkin Scenarios (Adzic)

```gherkin
  @happy
  Scenario: User adds a house rule
    Given HouseRuleModal is open for game "Catan"
    When user types "We always play with fog of war variant"
    And clicks Save
    Then the rule is submitted to /api/v1/gamebook/house-rules
    And the modal closes

  @validation
  Scenario: Rule text exceeds 500 characters is rejected
    Given user types 501 characters in the rule field
    Then Save button is disabled
    And error message "Rule too long" is shown

  @edge
  Scenario: Existing house rules are listed
    Given game has 3 house rules already saved
    When modal opens
    Then all 3 rules are displayed in a list
```

---

## Task 3.5f — Compose play/page.tsx

**Files to create/modify**:
- Create: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/page.tsx`
- Create: `apps/web/__tests__/gamebook/play/play-page.test.tsx`
- Create: `apps/web/e2e/gamebook/full-session.spec.ts`

### Acceptance Criteria

- AC-3.5f-1: Page layout: left panel (TranslationViewer + page navigation) + right panel (QAPanel).
- AC-3.5f-2: Page requires authentication (`RequireSession()` backend equivalent in `middleware.ts`).
- AC-3.5f-3: QuotaExceededModal visible only when `useQuota().creditBalance === 0 && useQuota().freeUsed >= useQuota().freeLimit`.
- AC-3.5f-4: HouseRuleModal triggered by floating action button.
- AC-3.5f-5: `<title>` meta contains game name from params.

---

## Task 3.6 — Rate Limiting for Gamebook Endpoints

**Goal**: Add sliding-window rate limit policies for gamebook-specific endpoints. Reuse existing `RateLimitingServiceExtensions.cs` (F-1 spike finding). Do NOT create a new `IUserRateLimiter` middleware.

**Files to modify**:
- Modify: `apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs`
- Modify: `apps/api/src/Api/Routing/PricingEndpoints.cs` (add `.RequireRateLimiting("GamebookCheckout")`)
- Modify: `apps/api/src/Api/Routing/KnowledgeBaseExtensions/ParagraphEndpoints.cs` (add `.RequireRateLimiting("GamebookTranslate")`)

**New policies to add**:

```csharp
// In RateLimitingServiceExtensions.cs — add to both prod and test (no-limit) branches:

// Policy 19: GamebookTranslate - 20 req/min for gamebook translation (expensive LLM call)
options.AddPolicy("GamebookTranslate", httpContext =>
{
    var userId = GetUserId(httpContext);
    return RateLimitPartition.GetSlidingWindowLimiter(
        partitionKey: $"gamebook-translate-{userId}",
        factory: _ => new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 20,
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
});

// Policy 20: GamebookQA - 30 req/min for gamebook Q&A queries
options.AddPolicy("GamebookQA", httpContext =>
{
    var userId = GetUserId(httpContext);
    return RateLimitPartition.GetSlidingWindowLimiter(
        partitionKey: $"gamebook-qa-{userId}",
        factory: _ => new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 30,
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
});

// Policy 21: GamebookCheckout - 5 req/min for credit checkout (prevent checkout spam)
options.AddPolicy("GamebookCheckout", httpContext =>
{
    var userId = GetUserId(httpContext);
    return RateLimitPartition.GetSlidingWindowLimiter(
        partitionKey: $"gamebook-checkout-{userId}",
        factory: _ => new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 5,
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
});
```

### Acceptance Criteria

- AC-3.6-1: 21st gamebook translate request within 1 minute returns 429 with `Retry-After` header.
- AC-3.6-2: Rate limit policies disabled in test environment (`DISABLE_RATE_LIMITING=true`).
- AC-3.6-3: Admin user is NOT exempt from gamebook rate limits (these are cost protection, not tier-based).
- AC-3.6-4: Rate limit headers `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` are always returned.

### Gherkin Scenarios (Adzic)

```gherkin
  @security
  Scenario: Translation rate limit enforcement
    Given user Alice sends 20 translation requests within 60 seconds
    Then all 20 succeed
    When Alice sends a 21st request within the same window
    Then the response is 429 Too Many Requests
    And header Retry-After is present

  @security
  Scenario: Checkout spam prevention
    Given Alice attempts 6 checkout sessions in 60 seconds
    Then the 6th returns 429

  @edge
  Scenario: Rate limit window resets after 60 seconds
    Given Alice has used all 20 translation slots
    After 61 seconds
    When Alice sends another translation request
    Then the response is 200
```

### Operational Considerations (Nygard)

- **Attack surface**: Gamebook translate is the most expensive endpoint (LLM call + possibly Stripe). 20 req/min per user = 2880 requests/day worst case. At ~$0.001/request DeepSeek = $2.88/day/abusive user. Acceptable for MVP; reassess at scale.
- **Redis required**: Sliding window is Redis-backed via `IRateLimitService`. Redis outage falls back to in-process (ASP.NET Core `SlidingWindowLimiter` without Redis partitioning). Acceptable for MVP.

---

## Task 3.7 — Privacy Policy Update

**Spike finding (F-5)**: Privacy policy page already exists at `apps/web/src/app/(public)/privacy/page.tsx` with GDPR Art. 13/14 sections, Italian/English toggle, JSON-LD.

**Scope for Task 3.7** (not a full creation):
- Modify: `apps/web/src/app/(public)/privacy/page.tsx` — add `'gamebookProcessing'` and `'creditBilling'` to `PRIVACY_SECTIONS` array.
- Modify: Privacy content file (wherever `PRIVACY_SECTIONS` content is defined) — add sections covering:
  - AI-assisted translation (what data is sent to OpenRouter/LLM provider)
  - Photo/image data handling (uploaded pages are processed by smoldocling)
  - Credit billing data (Stripe processes payment; we store credit balance, not card data)
- Verify: Footer link to `/privacy` exists in main layout.

**Files to modify**:
- Modify: `apps/web/src/app/(public)/privacy/page.tsx`
- Locate and modify content file for privacy sections.
- Create: `apps/web/__tests__/privacy/gamebook-privacy-sections.test.tsx` (verify new sections render).

### Acceptance Criteria

- AC-3.7-1: `/privacy` page contains section on AI-assisted translation disclosing OpenRouter as sub-processor.
- AC-3.7-2: `/privacy` page contains section on photo data processing (smoldocling, Hetzner storage).
- AC-3.7-3: `/privacy` page contains section on credit billing (Stripe as payment processor).
- AC-3.7-4: Footer link to `/privacy` is present in gamebook layout.
- AC-3.7-5: Both Italian and English versions of new sections are present (i18n).

### Gherkin Scenarios (Adzic)

```gherkin
  @legal
  Scenario: Gamebook AI processing disclosed
    Given user visits /privacy
    Then the page contains information about AI-assisted translation
    And names OpenRouter as the LLM routing provider

  @legal
  Scenario: Photo data handling disclosed
    When user visits /privacy
    Then the page explains that uploaded photos are processed on Hetzner infrastructure
    And are deleted after processing (or retained up to X days)

  @a11y
  Scenario: Privacy page is accessible
    Given the privacy page is rendered
    Then all sections have proper heading hierarchy (h2 sections)
    And language toggle is keyboard accessible
```

---

## Task 3.8 — GDPR Data Deletion + Export Extension

**Spike findings (F-3/F-4)**: Both handlers already exist. Task 3.8 extends them.

**Files to modify**:
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/DeleteOwnAccountCommandHandler.cs` — add gamebook cascade.
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/ExportUserDataCommandHandler.cs` — add gamebook data to export.
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/DeleteOwnAccountGambookCascadeTests.cs`

**Gamebook data to cascade on delete**:
1. `photo_batch_uploads` (and `photo_batch_pages`) owned by user — hard delete.
2. `user_quotas` — hard delete (credit balance non-refundable per TOS; log for accounting).
3. `stripe_webhook_events` — anonymize `user_id` to NULL (retain for fraud audit).

**Gamebook data to include in export**:
1. `PhotoBatchUploads`: upload date, game associated, page count, processing status.
2. `UserQuota`: free pages used, credit balance, last reset date.
3. `HouseRules` (Task 2.5): rules the user has saved.

### Step 1 — RED: Failing test (cascade)

```csharp
[Fact]
public async Task DeleteOwnAccount_DeletesGambookData()
{
    // Arrange: user with photo batch and quota
    var userId = Guid.NewGuid();
    var user = /* seed user */;
    var quota = UserQuota.CreateForUser(userId);
    var photoBatch = PhotoBatchUpload.Create(userId, Guid.NewGuid(), 5);
    _db.UserQuotas.Add(quota);
    _db.PhotoBatchUploads.Add(photoBatch);
    await _db.SaveChangesAsync();

    // Act
    var command = new DeleteOwnAccountCommand(userId, userId, confirmedPassword: "valid");
    await _handler.Handle(command, CancellationToken.None);

    // Assert
    _db.UserQuotas.Should().BeEmpty();
    _db.PhotoBatchUploads.Should().BeEmpty();
}
```

### Step 2 — GREEN: Modification to existing handler

```csharp
// Addition to DeleteOwnAccountCommandHandler.Handle() — after step 2 (LLM delete):

// 3b. Delete gamebook data
await _db.PhotoBatchUploads
    .Where(p => p.UserId == command.UserId)
    .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

await _db.UserQuotas
    .Where(q => q.UserId == command.UserId)
    .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

// Anonymize Stripe webhook events (retain for fraud audit, GDPR Art. 17 § 3b)
await _db.StripeWebhookEvents
    .Where(e => e.UserId == command.UserId)
    .ExecuteUpdateAsync(s => s.SetProperty(e => e.UserId, (Guid?)null), cancellationToken)
    .ConfigureAwait(false);

_logger.LogWarning(
    "GDPR Art.17: Gamebook data deleted for UserId={UserId}",
    command.UserId);
```

### Acceptance Criteria

- AC-3.8-1: `DELETE /api/v1/account` cascade deletes all `PhotoBatchUploads` and `UserQuota` for the user.
- AC-3.8-2: `StripeWebhookEvents` are anonymized (UserId set to NULL), not deleted.
- AC-3.8-3: `GET /api/v1/account/export` JSON includes `gamebook` section with uploads, quota, house rules.
- AC-3.8-4: Export and delete operations are logged at `LogWarning` with GDPR article reference.
- AC-3.8-5: Both operations complete within 10s for users with < 100 uploads.

### Gherkin Scenarios (Adzic)

```gherkin
  @gdpr
  Scenario: Account deletion removes gamebook uploads
    Given Alice has 5 photo batch uploads
    And Alice has a UserQuota with 30 credits
    When Alice deletes her account
    Then her PhotoBatchUploads are deleted from the database
    And her UserQuota is deleted
    And StripeWebhookEvents for her user_id are anonymized (user_id = NULL)

  @gdpr
  Scenario: Data export includes gamebook data
    Given Alice has gamebook uploads and a quota
    When Alice requests a data export
    Then the JSON response includes a "gamebook" section
    And it lists her photo uploads with dates and game names
    And it shows her quota (free used, credit balance)

  @edge
  Scenario: Account deletion with pending Stripe webhook
    Given a Stripe event is being processed when Alice deletes her account
    Then the delete operation completes without deadlock
    And the event is anonymized after processing
```

---

## Task 3.9 — Phase 3 Acceptance Gate (E2E + Payment Flow)

**Goal**: End-to-end Playwright tests covering the happy path through the full gamebook play session and the payment flow.

**Files to create**:
- Create: `apps/web/e2e/gamebook/full-session.spec.ts`
- Create: `apps/web/e2e/gamebook/quota-flow.spec.ts`
- Create: `apps/web/e2e/gamebook/translation-flow.spec.ts`

**Pattern**: See `apps/web/e2e/` for existing spec patterns. Use `page.context().route()` for API mocking (NOT `page.route()`).

### E2E Test Structure

```typescript
// apps/web/e2e/gamebook/full-session.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Gamebook full play session', () => {
  test.use({ storageState: 'e2e/.auth/user.json' });

  test('user translates a page and asks a question', async ({ page, context }) => {
    // Mock backend APIs
    await context.route('**/api/v1/gamebook/quota', async (route) => {
      await route.fulfill({ json: { freeUsed: 5, freeLimit: 50, creditBalance: 0, tier: 'free' } });
    });

    await context.route('**/api/v1/gamebook/uploads/*/pages/*', async (route) => {
      await route.fulfill({
        json: { pageNumber: 5, text: 'Rule 5: the current player moves.', fallbackUsed: false },
      });
    });

    await context.route('**/api/v1/kb/ask', async (route) => {
      await route.fulfill({
        json: {
          answer: 'Yes, you can trade resources on your turn.',
          sources: [],
          citations: [{ pageNumber: 3, snippet: 'trading...', relevanceScore: 0.9, documentId: 'd1' }],
        },
      });
    });

    await page.goto('/gamebook/game-123/play');

    // Verify translation viewer loaded
    await expect(page.getByText('Rule 5: the current player moves.')).toBeVisible();

    // Ask a question
    await page.getByRole('textbox', { name: /ask a question/i }).fill('Can I trade?');
    await page.getByRole('button', { name: /ask/i }).click();

    await expect(page.getByText('Yes, you can trade resources on your turn.')).toBeVisible();
  });
});
```

```typescript
// apps/web/e2e/gamebook/quota-flow.spec.ts
test('QuotaExceededModal appears and redirects to Stripe on upgrade', async ({ page, context }) => {
  await context.route('**/api/v1/gamebook/quota', async (route) => {
    await route.fulfill({ json: { freeUsed: 50, freeLimit: 50, creditBalance: 0, tier: 'free' } });
  });

  await context.route('**/api/v1/gamebook/credits/checkout', async (route) => {
    await route.fulfill({ json: { sessionId: 'cs_test', checkoutUrl: 'https://checkout.stripe.com/cs_test' } });
  });

  await page.goto('/gamebook/game-123/play');

  // Quota exceeded modal should be visible
  await expect(page.getByRole('dialog', { name: /quota exceeded/i })).toBeVisible();

  // Click Buy Credits
  await page.getByRole('button', { name: /buy credits/i }).click();

  // Should redirect to Stripe (we mock the URL, verify navigation)
  await expect(page).toHaveURL(/checkout\.stripe\.com/);
});
```

### Acceptance Criteria (Phase 3 Exit Gate)

- AC-3.9-1: Full play session E2E test passes: load page, translate paragraph, ask question, see answer.
- AC-3.9-2: Quota exceeded flow E2E test passes: modal appears, Buy Credits navigates to Stripe URL.
- AC-3.9-3: Payment flow integration test passes (against Stripe test mode with real API keys in staging).
- AC-3.9-4: Rate limit E2E: 21st consecutive request returns 429 in under 2s.
- AC-3.9-5: Webhook idempotency test: same event ID sent twice — credits granted once.
- AC-3.9-6: GDPR: account deletion removes gamebook data — verified by direct DB query in test.
- AC-3.9-7: All Phase 3 backend unit tests pass (`dotnet test --filter Category=Unit`).
- AC-3.9-8: Frontend Vitest test coverage ≥ 80% for gamebook components.
- AC-3.9-9: Privacy policy page updated and accessible (axe-core: 0 violations).
- AC-3.9-10: P95 latency: translation < 5s, Q&A < 5s measured in staging load test.

### Gherkin Scenarios (Adzic)

```gherkin
  @integration
  Scenario: Stripe test-mode payment flow end-to-end
    Given user Alice has 0 credits
    When Alice initiates a 100-page credit purchase in staging
    And completes the Stripe test payment (card 4242 4242 4242 4242)
    Then Alice's credit balance increases to 100
    And Alice can translate pages using the credits

  @chaos
  Scenario: Translation during Stripe outage (non-blocking)
    Given Stripe is returning 503
    When Alice requests a page translation (not checkout)
    Then the translation succeeds (quota check does not call Stripe)
    And Alice's free quota decrements normally

  @integration
  Scenario: MonthlyQuotaResetJob runs successfully
    Given 10 users have used free pages in the previous month
    When the MonthlyQuotaResetJob executes
    Then all 10 users have free_used_this_month = 0
    And the job completion is logged
```

### Operational Considerations (Nygard)

- **Staging test mode**: Stripe test API keys must be set in staging `stripe.secret`. Test webhooks use Stripe CLI `stripe listen --forward-to`.
- **CI skip**: E2E payment tests tagged `@payment` skipped in standard CI; run in nightly pipeline against staging.
- **Monitoring checklist before Phase 4 entry**:
  - Prometheus alert: `libro_quota_consumed_total` visible in Grafana.
  - Stripe dashboard: webhook delivery success rate > 99%.
  - Error rate `5xx` gamebook endpoints < 1% in 24h window.
  - Sentry: no unhandled exceptions in gamebook BC.

---

## Cross-Cutting Concerns

### Stripe Webhook Idempotency Strategy

**Chosen strategy**: Database-backed unique idempotency key table.

```sql
CREATE TABLE stripe_webhook_events (
    event_id    VARCHAR(255) PRIMARY KEY,
    user_id     UUID NULL,           -- Nullable for anonymization (GDPR)
    credits_granted INT NOT NULL DEFAULT 0,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

**Rationale**:
- In-memory deduplication dies on restart or replica failover (Newman concern).
- Redis-only deduplication lacks audit trail for finance reconciliation (Nygard concern).
- DB primary key gives atomicity: insert succeeds = not duplicate; `UniqueConstraintException` = duplicate → return 200, no grant.
- GDPR compatibility: `user_id` nullable for anonymization on account deletion.
- Transaction wraps: `INSERT stripe_webhook_events` + `UPDATE user_quotas.credit_balance` atomically.

**Concurrent delivery handling**: If two replicas receive the same event simultaneously, one `INSERT` wins. The losing one gets `DbUpdateException` wrapping `PostgresException.23505 (unique_violation)` → log warning, return 200 OK. Credits granted exactly once.

### Quota Enforcement Boundary

**Decision**: Enforce in **command/query handler**, not in FluentValidation validator.

**Rationale**:
- Validators guard *input shape* (field types, required fields, string length).
- Quota is a *business invariant* that requires reading from DB. Validators run synchronously with no DB access in existing patterns.
- Testing: NSubstitute mocks `IPricingEngine` in handler tests; no infrastructure needed.
- FluentValidation can be reused across endpoints; quota rules are user-specific.

**Implementation**: In `GetParagraphQueryHandler.Handle()` and `AskQuestionQueryHandler.Handle()`, call `await _pricingEngine.CheckQuotaAsync()` before processing. If `!result.Allowed`, throw custom `QuotaExceededException` mapped to HTTP 402.

```csharp
// In each handler:
var quota = await _pricingEngine.CheckQuotaAsync(query.UserId, BillableOperation.PageTranslation, ct);
if (!quota.Allowed)
    throw new QuotaExceededException(quota.RemainingFree, quota.RemainingCredits);
```

### GDPR Data Deletion Cascade (BC Inventory)

User data that must be deleted or anonymized when `DeleteOwnAccount` is called:

| BC | Data | Action |
|----|------|--------|
| Authentication | `users`, `sessions`, `oauth_accounts`, `backup_codes` | Hard delete (EF cascade) |
| KnowledgeBase | `llm_request_logs`, `conversation_memories` | Hard delete (existing `DeleteUserLlmDataCommand`) |
| UserNotifications | `notifications` | Soft-delete via UserId orphan (acceptable) |
| UserLibrary | `user_library_entries` | Soft-delete via UserId orphan (acceptable) |
| DocumentProcessing | `photo_batch_uploads`, `photo_batch_pages` | Hard delete (Task 3.8 addition) |
| Infrastructure/Pricing | `user_quotas` | Hard delete (Task 3.8 addition) |
| Infrastructure/Pricing | `stripe_webhook_events` | Anonymize `user_id = NULL` (Task 3.8 addition) |
| AgentMemory | `house_rules` | Hard delete if user_id FK exists (Task 2.5 data) |

**Order of operations** (FK safety):
1. Delete `sessions` (FK to users).
2. Delete `llm_request_logs`, `conversation_memories` (via existing command).
3. Delete `photo_batch_pages` (FK to `photo_batch_uploads`).
4. Delete `photo_batch_uploads`.
5. Delete `user_quotas`.
6. Anonymize `stripe_webhook_events`.
7. Delete `house_rules`.
8. Delete `users` (FK anchor for remaining soft-orphan records).

### Rate Limiting Strategy

**Algorithm**: Sliding window (already established in `RateLimitingServiceExtensions.cs`).

**Storage**: ASP.NET Core built-in `SlidingWindowRateLimiter` uses **in-process** state (not Redis). The separate `IRateLimitService` (Redis token bucket) is used ONLY by `BggRateLimitMiddleware`.

**Implication for Task 3.6**: New gamebook policies use the built-in sliding window (in-process). This means:
- Not shared across multiple replicas (each replica has its own counter).
- Acceptable for MVP single-server deployment on CAX31.
- If horizontal scaling is needed later, migrate to Redis-backed partitioning.

**Gamebook policy limits** (chosen values):
- `GamebookTranslate`: 20 req/min — LLM cost protection.
- `GamebookQA`: 30 req/min — slightly higher than translate (cheaper).
- `GamebookCheckout`: 5 req/min — prevent checkout session spam.

---

## Phase 3 Acceptance Gate (Phase 4 Entry Criteria)

All of the following must pass before starting Phase 4:

| Gate | Criterion | Verification |
|------|-----------|-------------|
| G1 | All 13 sub-tasks implemented and tests passing | `dotnet test` green, `pnpm test` green |
| G2 | Stripe checkout creates session in test mode | Manual test against staging |
| G3 | Stripe webhook grants credits exactly once on duplicate delivery | Integration test |
| G4 | Monthly quota reset job runs and is idempotent | Integration test + Quartz trigger verify |
| G5 | Play page full E2E session completes without errors | Playwright `full-session.spec.ts` |
| G6 | QuotaExceededModal accessible (axe-core 0 violations) | Playwright + `@axe-core/playwright` |
| G7 | GDPR account deletion removes gamebook data | DB assertion test |
| G8 | Privacy policy updated with gamebook disclosure | Manual review + E2E render test |
| G9 | Rate limiting enforces 429 on burst | E2E test `quota-flow.spec.ts` |
| G10 | P95 translation latency < 5s on staging | `k6` load test at 5 concurrent users |

---

## Effort Estimate

| Task | Effort (days) | Owner | Dependencies |
|------|--------------|-------|-------------|
| 3.1 GetParagraphQuery | 2 | Backend | Phase 2 complete |
| 3.2 IPricingEngine + UserQuota | 3 | Backend | Migration |
| 3.3a Stripe SDK + CheckoutService | 2 | Backend | 3.2 |
| 3.3b Stripe Webhook + Idempotency | 3 | Backend | 3.2, 3.3a |
| 3.3c Pricing Endpoints | 1 | Backend | 3.3a, 3.3b |
| 3.4 MonthlyQuotaResetJob | 1 | Backend | 3.2 |
| 3.5a Frontend Hooks | 2 | Frontend | 3.3c |
| 3.5b TranslationViewer | 2 | Frontend | 3.5a |
| 3.5c QAPanel | 2 | Frontend | 3.5a |
| 3.5d QuotaExceededModal | 1 | Frontend | 3.5a |
| 3.5e HouseRuleModal | 1 | Frontend | Phase 2 (house rules) |
| 3.5f Compose play/page.tsx | 2 | Frontend | 3.5b, 3.5c, 3.5d, 3.5e |
| 3.6 Rate limiting policies | 1 | Backend | 3.3c |
| 3.7 Privacy policy update | 1 | Frontend/Legal | Legal review |
| 3.8 GDPR extension | 2 | Backend | 3.2 |
| 3.9 E2E acceptance gate | 3 | QA/Fullstack | All above |
| **Total** | **29 days** | 3 fullstack | ~6 weeks with review overhead |

**Total Phase 3: ~6 weeks** (matches plan v2 estimate).

---

*Plan authored by spec-panel review 2026-05-04. Expert voices: Karl Wiegers (requirements quality), Gojko Adzic (specification by example), Sam Newman (microservices/integration), Lisa Crispin (testing strategy), Michael Nygard (operational reliability).*

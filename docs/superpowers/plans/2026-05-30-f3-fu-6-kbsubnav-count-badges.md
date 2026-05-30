# F3-FU-6 KbSubNav Count Badges — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add wired count badges to the `KbSubNav` for the "Processing Queue" and "Feedback" tabs in the admin Knowledge Base section, fed by a single polled endpoint.

**Architecture:** New `GET /api/v1/admin/kb/nav-counts` endpoint returns 2 counts: active jobs (`Queued+Processing+Failed`) + feedback last 7 days. React Query polling (30s + window-focus refetch). New `KbCountBadge` component rendered next to label of those 2 tabs only.

**Tech Stack:** .NET 9 MediatR + EF Core + Postgres; Next.js + React Query + Tailwind; xUnit + NSubstitute + FluentAssertions + Testcontainers; Vitest + Testing Library + Playwright.

**Spec reference:** `docs/superpowers/specs/2026-05-30-f3-fu-6-kbsubnav-count-badges-design.md`

**Branch:** `feature/issue-1655-kbsubnav-count-badges` (already created, parent `main-dev`)

**Issue:** [#1655](https://github.com/meepleAi-app/meepleai-monorepo/issues/1655) — P3

---

## Task 1: Add `CountByStatusesAsync` to `IProcessingJobRepository`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Repositories/IProcessingJobRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/ProcessingJobRepository.cs`
- Test (new): `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/ProcessingJobRepositoryCountByStatusesTests.cs`

Context: `Status` column is stored as a **string** in DB (see existing `CountByStatusAsync` impl at line 97 of `ProcessingJobRepository.cs` — uses `status.ToString()` for comparison). The new method accepts a list of `JobStatus` enum values and uses `Contains` on serialized strings.

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/ProcessingJobRepositoryCountByStatusesTests.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1655")]
public sealed class ProcessingJobRepositoryCountByStatusesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProcessingJobRepository CreateRepo(out Api.Infrastructure.MeepleAiDbContext db)
    {
        db = TestDbContextFactory.CreateInMemoryDbContext();
        var collector = Substitute.For<IDomainEventCollector>();
        return new ProcessingJobRepository(db, collector);
    }

    private static ProcessingJobEntity NewJob(JobStatus status) => new()
    {
        Id = Guid.NewGuid(),
        PdfDocumentId = Guid.NewGuid(),
        Status = status.ToString(),
        Priority = 0,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        EnqueuedBy = Guid.NewGuid(),
    };

    [Fact]
    public async Task CountByStatusesAsync_NoMatches_ReturnsZero()
    {
        var repo = CreateRepo(out var db);
        db.ProcessingJobs.AddRange(NewJob(JobStatus.Completed), NewJob(JobStatus.Cancelled));
        await db.SaveChangesAsync(Ct);

        var count = await repo.CountByStatusesAsync(
            new[] { JobStatus.Queued, JobStatus.Processing, JobStatus.Failed }, Ct);

        count.Should().Be(0);
    }

    [Fact]
    public async Task CountByStatusesAsync_MultipleStatuses_CountsAcrossAll()
    {
        var repo = CreateRepo(out var db);
        db.ProcessingJobs.AddRange(
            NewJob(JobStatus.Queued),
            NewJob(JobStatus.Queued),
            NewJob(JobStatus.Processing),
            NewJob(JobStatus.Failed),
            NewJob(JobStatus.Completed),     // not counted
            NewJob(JobStatus.Cancelled));    // not counted
        await db.SaveChangesAsync(Ct);

        var count = await repo.CountByStatusesAsync(
            new[] { JobStatus.Queued, JobStatus.Processing, JobStatus.Failed }, Ct);

        count.Should().Be(4);
    }

    [Fact]
    public async Task CountByStatusesAsync_EmptyStatusList_ReturnsZero()
    {
        var repo = CreateRepo(out var db);
        db.ProcessingJobs.Add(NewJob(JobStatus.Queued));
        await db.SaveChangesAsync(Ct);

        var count = await repo.CountByStatusesAsync(Array.Empty<JobStatus>(), Ct);

        count.Should().Be(0);
    }

    [Fact]
    public async Task CountByStatusesAsync_SingleStatus_CountsOnlyThatStatus()
    {
        var repo = CreateRepo(out var db);
        db.ProcessingJobs.AddRange(
            NewJob(JobStatus.Failed),
            NewJob(JobStatus.Failed),
            NewJob(JobStatus.Queued));
        await db.SaveChangesAsync(Ct);

        var count = await repo.CountByStatusesAsync(new[] { JobStatus.Failed }, Ct);

        count.Should().Be(2);
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

Run from `apps/api`:
```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ProcessingJobRepositoryCountByStatusesTests"
```

Expected: build error `'IProcessingJobRepository' does not contain a definition for 'CountByStatusesAsync'`.

- [ ] **Step 3: Add the method to the interface**

In `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Repositories/IProcessingJobRepository.cs`, add **before** the closing brace:

```csharp
    /// <summary>
    /// Count jobs whose status is in the supplied set.
    /// Returns 0 if the set is empty.
    /// Issue #1655: KbSubNav count badges.
    /// </summary>
    Task<int> CountByStatusesAsync(
        IReadOnlyList<JobStatus> statuses,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implement the method**

In `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/ProcessingJobRepository.cs`, add right after the existing `CountByStatusAsync` (around line 103):

```csharp
    public async Task<int> CountByStatusesAsync(
        IReadOnlyList<JobStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        if (statuses is null || statuses.Count == 0)
            return 0;

        var statusStrings = statuses.Select(s => s.ToString()).ToArray();
        return await DbContext.ProcessingJobs
            .AsNoTracking()
            .Where(j => statusStrings.Contains(j.Status))
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
```

- [ ] **Step 5: Run the test to confirm it passes**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ProcessingJobRepositoryCountByStatusesTests"
```

Expected: 4 passed.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Repositories/IProcessingJobRepository.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/ProcessingJobRepository.cs \
        apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/ProcessingJobRepositoryCountByStatusesTests.cs
git commit -m "feat(processing-queue): #1655 add CountByStatusesAsync repository method"
```

---

## Task 2: Add `CountSinceAsync` to `IKbUserFeedbackRepository`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IKbUserFeedbackRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/KbUserFeedbackRepository.cs`
- Test (new): `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/KbUserFeedbackRepositoryCountSinceTests.cs`

Context: `KbUserFeedback.CreatedAt` is `DateTime` (not `DateTimeOffset`); existing methods on this repository accept `DateTime?`.

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/KbUserFeedbackRepositoryCountSinceTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1655")]
public sealed class KbUserFeedbackRepositoryCountSinceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static KbUserFeedback MakeFeedback(DateTime createdAt)
    {
        // Domain factory sets CreatedAt = DateTime.UtcNow; we set it via reflection
        // for deterministic test scenarios (no public setter).
        var fb = KbUserFeedback.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            chatSessionId: Guid.NewGuid(),
            messageId: Guid.NewGuid(),
            outcome: "helpful",
            comment: null);
        var prop = typeof(KbUserFeedback).GetProperty(nameof(KbUserFeedback.CreatedAt))!;
        prop.SetValue(fb, createdAt);
        return fb;
    }

    [Fact]
    public async Task CountSinceAsync_NoFeedback_ReturnsZero()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var repo = new KbUserFeedbackRepository(db);

        var count = await repo.CountSinceAsync(DateTime.UtcNow.AddDays(-7), Ct);

        count.Should().Be(0);
    }

    [Fact]
    public async Task CountSinceAsync_IncludesFeedbackOnAndAfterSince()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var now = DateTime.UtcNow;
        var since = now.AddDays(-7);

        db.KbUserFeedbacks.AddRange(
            MakeFeedback(now),                          // within window
            MakeFeedback(now.AddDays(-3)),              // within window
            MakeFeedback(since),                        // exactly at boundary -> included
            MakeFeedback(now.AddDays(-10)),             // before window
            MakeFeedback(now.AddDays(-30)));            // before window
        await db.SaveChangesAsync(Ct);

        var repo = new KbUserFeedbackRepository(db);
        var count = await repo.CountSinceAsync(since, Ct);

        count.Should().Be(3);
    }

    [Fact]
    public async Task CountSinceAsync_AllOutcomesCount()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var now = DateTime.UtcNow;

        var helpful = MakeFeedback(now.AddDays(-1));
        var notHelpful = KbUserFeedback.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "not_helpful", null);
        typeof(KbUserFeedback).GetProperty(nameof(KbUserFeedback.CreatedAt))!
            .SetValue(notHelpful, now.AddDays(-2));

        db.KbUserFeedbacks.AddRange(helpful, notHelpful);
        await db.SaveChangesAsync(Ct);

        var repo = new KbUserFeedbackRepository(db);
        var count = await repo.CountSinceAsync(now.AddDays(-7), Ct);

        count.Should().Be(2);
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~KbUserFeedbackRepositoryCountSinceTests"
```

Expected: build error `'IKbUserFeedbackRepository' does not contain a definition for 'CountSinceAsync'`.

- [ ] **Step 3: Add the method to the interface**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IKbUserFeedbackRepository.cs`, add before the closing brace:

```csharp
    /// <summary>
    /// Count feedback rows whose CreatedAt is on or after the given moment.
    /// Issue #1655: KbSubNav count badges.
    /// </summary>
    Task<int> CountSinceAsync(DateTime since, CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implement the method**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/KbUserFeedbackRepository.cs`, add right after `CountByGameIdAsync` (around line 48):

```csharp
    public Task<int> CountSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        return _db.KbUserFeedbacks
            .AsNoTracking()
            .Where(f => f.CreatedAt >= since)
            .CountAsync(cancellationToken);
    }
```

- [ ] **Step 5: Run the test to confirm it passes**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~KbUserFeedbackRepositoryCountSinceTests"
```

Expected: 3 passed.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IKbUserFeedbackRepository.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/KbUserFeedbackRepository.cs \
        apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/KbUserFeedbackRepositoryCountSinceTests.cs
git commit -m "feat(kb): #1655 add CountSinceAsync repository method"
```

---

## Task 3: Create `KbNavCountsDto`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbNavCountsDto.cs`

No test needed for a pure data record. It will be validated indirectly via Task 4 (handler) and Task 6 (endpoint).

- [ ] **Step 1: Create the DTO**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Counts displayed as badges on the admin KbSubNav.
/// Issue #1655 (F3-FU-6).
/// </summary>
public sealed record KbNavCountsDto(
    int ProcessingQueue,
    int Feedback7d,
    DateTimeOffset AsOf
);
```

- [ ] **Step 2: Verify the project builds**

```bash
dotnet build apps/api/src/Api/Api.csproj
```

Expected: build succeeded.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbNavCountsDto.cs
git commit -m "feat(kb): #1655 add KbNavCountsDto"
```

---

## Task 4: Create `GetKbNavCountsQuery` + Handler (unit-tested)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/GetKbNavCountsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/GetKbNavCountsQueryHandler.cs`
- Test (new): `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/GetKbNavCountsQueryHandlerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/GetKbNavCountsQueryHandlerTests.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1655")]
public sealed class GetKbNavCountsQueryHandlerTests
{
    private readonly IProcessingJobRepository _jobs = Substitute.For<IProcessingJobRepository>();
    private readonly IKbUserFeedbackRepository _feedback = Substitute.For<IKbUserFeedbackRepository>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero));
    private readonly GetKbNavCountsQueryHandler _sut;

    public GetKbNavCountsQueryHandlerTests()
    {
        _sut = new GetKbNavCountsQueryHandler(_jobs, _feedback, _clock);
    }

    [Fact]
    public async Task Handle_ReturnsCountsFromBothRepositories()
    {
        _jobs.CountByStatusesAsync(Arg.Any<IReadOnlyList<JobStatus>>(), Arg.Any<CancellationToken>())
            .Returns(7);
        _feedback.CountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(23);

        var result = await _sut.Handle(new GetKbNavCountsQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.ProcessingQueue.Should().Be(7);
        result.Feedback7d.Should().Be(23);
        result.AsOf.Should().Be(_clock.GetUtcNow());
    }

    [Fact]
    public async Task Handle_PassesActiveStatusesToProcessingRepo()
    {
        await _sut.Handle(new GetKbNavCountsQuery(), CancellationToken.None);

        await _jobs.Received(1).CountByStatusesAsync(
            Arg.Is<IReadOnlyList<JobStatus>>(s =>
                s.Count == 3 &&
                s.Contains(JobStatus.Queued) &&
                s.Contains(JobStatus.Processing) &&
                s.Contains(JobStatus.Failed)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesNowMinus7DaysToFeedbackRepo()
    {
        await _sut.Handle(new GetKbNavCountsQuery(), CancellationToken.None);

        var expectedSince = _clock.GetUtcNow().UtcDateTime.AddDays(-7);
        await _feedback.Received(1).CountSinceAsync(
            Arg.Is<DateTime>(d => Math.Abs((d - expectedSince).TotalMilliseconds) < 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RunsCountQueriesInParallel()
    {
        var queueTcs = new TaskCompletionSource<int>();
        _jobs.CountByStatusesAsync(Arg.Any<IReadOnlyList<JobStatus>>(), Arg.Any<CancellationToken>())
            .Returns(queueTcs.Task);
        _feedback.CountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var task = _sut.Handle(new GetKbNavCountsQuery(), CancellationToken.None);

        // Give the scheduler a chance to enter both awaits
        await Task.Yield();

        // If the handler called repos sequentially it would still be on the first await
        // and the feedback call would never have happened.
        await _feedback.Received(1).CountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());

        queueTcs.SetResult(1);
        var result = await task;

        result.ProcessingQueue.Should().Be(1);
        result.Feedback7d.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PropagatesProcessingRepoException()
    {
        _jobs.CountByStatusesAsync(Arg.Any<IReadOnlyList<JobStatus>>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("boom"));
        _feedback.CountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var act = () => _sut.Handle(new GetKbNavCountsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _jobs.CountByStatusesAsync(Arg.Any<IReadOnlyList<JobStatus>>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new OperationCanceledException(cts.Token));
        _feedback.CountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new OperationCanceledException(cts.Token));

        var act = () => _sut.Handle(new GetKbNavCountsQuery(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetKbNavCountsQueryHandlerTests"
```

Expected: build error — query/handler don't exist.

- [ ] **Step 3: Create the query**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/GetKbNavCountsQuery.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts;

/// <summary>
/// Returns counts for KbSubNav badges: active processing jobs + feedback last 7 days.
/// Issue #1655 (F3-FU-6).
/// </summary>
public sealed record GetKbNavCountsQuery() : IRequest<KbNavCountsDto>;
```

- [ ] **Step 4: Create the handler**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/GetKbNavCountsQueryHandler.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts;

internal sealed class GetKbNavCountsQueryHandler
    : IRequestHandler<GetKbNavCountsQuery, KbNavCountsDto>
{
    private static readonly JobStatus[] ActiveStatuses =
        [JobStatus.Queued, JobStatus.Processing, JobStatus.Failed];

    private static readonly TimeSpan FeedbackWindow = TimeSpan.FromDays(7);

    private readonly IProcessingJobRepository _jobs;
    private readonly IKbUserFeedbackRepository _feedback;
    private readonly TimeProvider _clock;

    public GetKbNavCountsQueryHandler(
        IProcessingJobRepository jobs,
        IKbUserFeedbackRepository feedback,
        TimeProvider clock)
    {
        _jobs = jobs;
        _feedback = feedback;
        _clock = clock;
    }

    public async Task<KbNavCountsDto> Handle(GetKbNavCountsQuery request, CancellationToken cancellationToken)
    {
        var asOf = _clock.GetUtcNow();
        var since = asOf.UtcDateTime - FeedbackWindow;

        var queueTask = _jobs.CountByStatusesAsync(ActiveStatuses, cancellationToken);
        var feedbackTask = _feedback.CountSinceAsync(since, cancellationToken);

        await Task.WhenAll(queueTask, feedbackTask).ConfigureAwait(false);

        return new KbNavCountsDto(
            ProcessingQueue: await queueTask.ConfigureAwait(false),
            Feedback7d: await feedbackTask.ConfigureAwait(false),
            AsOf: asOf);
    }
}
```

- [ ] **Step 5: Run the tests to confirm they pass**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetKbNavCountsQueryHandlerTests"
```

Expected: 6 passed.

If `Microsoft.Extensions.Time.Testing` is not referenced by `Api.Tests.csproj`, add it via `<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="9.0.0" />` (verify exact version against `Directory.Packages.props`).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/ \
        apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/
git commit -m "feat(kb): #1655 add GetKbNavCountsQuery + handler"
```

---

## Task 5: Integration test the handler against real Postgres

**Files:**
- Test (new): `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/Integration/GetKbNavCountsQueryHandlerIntegrationTests.cs`

This test wires the handler against a real Postgres via Testcontainers (rule: "acceptance tests must exercise real pipeline").

- [ ] **Step 1: Write the failing test**

Create the file:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts.Integration;

[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1655")]
public sealed class GetKbNavCountsQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _db = null!;

    public GetKbNavCountsQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"kbnav_counts_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        _db = new MeepleAiDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _db?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task Handle_RealPostgres_CountsActiveJobsAndRecentFeedback()
    {
        // Seed jobs: 2 Queued + 1 Processing + 1 Failed = 4 active; 3 Completed + 1 Cancelled = excluded
        var now = DateTime.UtcNow;
        var jobs = new[]
        {
            NewJob(JobStatus.Queued, now), NewJob(JobStatus.Queued, now),
            NewJob(JobStatus.Processing, now),
            NewJob(JobStatus.Failed, now),
            NewJob(JobStatus.Completed, now), NewJob(JobStatus.Completed, now), NewJob(JobStatus.Completed, now),
            NewJob(JobStatus.Cancelled, now),
        };
        _db.ProcessingJobs.AddRange(jobs);

        // Seed feedback: 5 within 7d + 2 older
        var clock = new FakeTimeProvider(new DateTimeOffset(now, TimeSpan.Zero));
        foreach (var minus in new[] { 0, 1, 3, 5, 6 })
            _db.KbUserFeedbacks.Add(MakeFeedback(now.AddDays(-minus)));
        foreach (var minus in new[] { 10, 30 })
            _db.KbUserFeedbacks.Add(MakeFeedback(now.AddDays(-minus)));

        await _db.SaveChangesAsync();

        var collector = Substitute.For<IDomainEventCollector>();
        var jobsRepo = new ProcessingJobRepository(_db, collector);
        var fbRepo = new KbUserFeedbackRepository(_db);
        var sut = new GetKbNavCountsQueryHandler(jobsRepo, fbRepo, clock);

        var result = await sut.Handle(new GetKbNavCountsQuery(), CancellationToken.None);

        result.ProcessingQueue.Should().Be(4);
        result.Feedback7d.Should().Be(5);
        result.AsOf.Should().Be(clock.GetUtcNow());
    }

    private static ProcessingJobEntity NewJob(JobStatus status, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        PdfDocumentId = Guid.NewGuid(),
        Status = status.ToString(),
        Priority = 0,
        CreatedAt = new DateTimeOffset(nowUtc, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(nowUtc, TimeSpan.Zero),
        EnqueuedBy = Guid.NewGuid(),
    };

    private static KbUserFeedback MakeFeedback(DateTime createdAt)
    {
        var fb = KbUserFeedback.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "helpful", null);
        typeof(KbUserFeedback).GetProperty(nameof(KbUserFeedback.CreatedAt))!
            .SetValue(fb, createdAt);
        return fb;
    }
}
```

- [ ] **Step 2: Run the integration test**

Requires Docker running. From `apps/api`:
```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetKbNavCountsQueryHandlerIntegrationTests"
```

Expected: 1 passed (Testcontainers spins up Postgres, runs migrations, seeds, asserts).

If the test class file location does not compile due to project structure (e.g., test project does not auto-discover subfolders), verify against an existing `Integration/` test under `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Handlers/Integration/`.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/Integration/
git commit -m "test(kb): #1655 add integration test for GetKbNavCountsQueryHandler"
```

---

## Task 6: Add endpoint + HTTP integration tests (401/403/200)

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`
- Test (new): `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Endpoints/AdminKbNavCountsEndpointIntegrationTests.cs`

- [ ] **Step 1: Write the failing tests**

Create the test file:

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

namespace Api.Tests.BoundedContexts.KnowledgeBase.Endpoints;

[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1655")]
public sealed class AdminKbNavCountsEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _anonClient = null!;
    private HttpClient _editorClient = null!;
    private HttpClient _adminClient = null!;
    private string _editorToken = null!;
    private string _adminToken = null!;

    public AdminKbNavCountsEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"kbnav_endpoint_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();

        (_, _adminToken) = await TestSessionHelper.CreateSuperAdminSessionAsync(db);
        (_, _editorToken) = await TestSessionHelper.CreateEditorSessionAsync(db);

        _anonClient = _factory.CreateClient();
        _editorClient = _factory.CreateClient();
        _adminClient = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _anonClient?.Dispose();
        _editorClient?.Dispose();
        _adminClient?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private static HttpRequestMessage NewRequest(string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/kb/nav-counts");
        req.Headers.Add("Cookie", $"meepleai_session={token}");
        return req;
    }

    [Fact]
    public async Task Get_WithoutSession_Returns401()
    {
        var response = await _anonClient.GetAsync("/api/v1/admin/kb/nav-counts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WithEditorSession_Returns403()
    {
        var response = await _editorClient.SendAsync(NewRequest(_editorToken));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_WithAdminSession_Returns200WithDto()
    {
        var response = await _adminClient.SendAsync(NewRequest(_adminToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<KbNavCountsDto>();

        dto.Should().NotBeNull();
        dto!.ProcessingQueue.Should().BeGreaterThanOrEqualTo(0);
        dto.Feedback7d.Should().BeGreaterThanOrEqualTo(0);
        dto.AsOf.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AdminKbNavCountsEndpointIntegrationTests"
```

Expected: tests fail with 404 (endpoint not registered).

- [ ] **Step 3: Add the endpoint**

In `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`, add to the `using` block at top:
```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts;
```

Then add the endpoint inside `MapAdminKnowledgeBaseEndpoints`, right after the existing `/vector-stats` mapping (around line 35):

```csharp
        // GET /api/v1/admin/kb/nav-counts — Issue #1655 F3-FU-6
        kbGroup.MapGet("/nav-counts", async (IMediator mediator, CancellationToken ct) =>
        {
            var counts = await mediator.Send(new GetKbNavCountsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(counts);
        })
        .WithName("GetKbNavCounts")
        .WithSummary("Counts for KbSubNav badges (active queue + feedback last 7d).")
        .Produces<KbNavCountsDto>(200);
```

Also add `using Api.BoundedContexts.KnowledgeBase.Application.DTOs;` if not already present (for `KbNavCountsDto`).

- [ ] **Step 4: Run the tests to confirm they pass**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AdminKbNavCountsEndpointIntegrationTests"
```

Expected: 3 passed.

- [ ] **Step 5: Run full Api.Tests suite to confirm no regression**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AdminKnowledgeBaseEndpoints|FullyQualifiedName~KbSubNav|FullyQualifiedName~Issue\"1655\""
```

Expected: zero failures across the touched areas.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs \
        apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Endpoints/AdminKbNavCountsEndpointIntegrationTests.cs
git commit -m "feat(admin-kb): #1655 GET /api/v1/admin/kb/nav-counts endpoint"
```

---

## Task 7: Frontend API client — add `getNavCounts`

**Files:**
- Modify: `apps/web/src/lib/api/admin-knowledge-base.ts` (or whichever file currently exports `api.adminKnowledgeBase.*`)

Context: before editing, locate the existing client. Run:
```bash
grep -rn "adminKnowledgeBase" apps/web/src/lib --include="*.ts"
```
Use the file that already exports the `adminKnowledgeBase` object. If none exists, create `apps/web/src/lib/api/admin-knowledge-base.ts` and ensure it is re-exported from `apps/web/src/lib/api/index.ts` (or `api.ts`).

- [ ] **Step 1: Inspect existing client shape**

```bash
grep -A 5 "adminKnowledgeBase" apps/web/src/lib/api/*.ts | head -40
```

Identify the existing file, its export style (named object vs. namespace), and the helper used for fetches (`apiFetch`, `fetchJson`, etc.).

- [ ] **Step 2: Add the type + method**

In the located file, add the type definition near the other DTO interfaces:

```ts
export interface KbNavCountsDto {
  processingQueue: number;
  feedback7d: number;
  asOf: string;
}
```

And add the method into the `adminKnowledgeBase` export object (use whichever fetch helper the file already uses — example with a generic `apiFetch`):

```ts
getNavCounts: ({ signal }: { signal?: AbortSignal } = {}) =>
  apiFetch<KbNavCountsDto>('/api/v1/admin/kb/nav-counts', { signal }),
```

If a different convention is used in the file (e.g., axios-style or a `request()` helper with method+body), match that style.

- [ ] **Step 3: Verify TypeScript compiles**

```bash
cd apps/web && pnpm typecheck
```

Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/
git commit -m "feat(api-client): #1655 add adminKnowledgeBase.getNavCounts"
```

---

## Task 8: Create `useKbNavCounts` hook with tests

**Files:**
- Create: `apps/web/src/hooks/admin/useKbNavCounts.ts`
- Test (new): `apps/web/src/hooks/admin/__tests__/useKbNavCounts.test.ts`

Pre-check: confirm `apps/web/src/hooks/admin/` exists. If not, create the dir.

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/hooks/admin/__tests__/useKbNavCounts.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';

import { useKbNavCounts } from '../useKbNavCounts';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: {
    adminKnowledgeBase: {
      getNavCounts: vi.fn(),
    },
  },
}));

const getNavCounts = vi.mocked(api.adminKnowledgeBase.getNavCounts);

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useKbNavCounts', () => {
  beforeEach(() => {
    getNavCounts.mockReset();
  });

  it('returns queue and feedback from a successful fetch', async () => {
    getNavCounts.mockResolvedValueOnce({
      processingQueue: 7,
      feedback7d: 23,
      asOf: '2026-05-30T12:00:00Z',
    });

    const { result } = renderHook(() => useKbNavCounts(), { wrapper });

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.queue).toBe(7);
    expect(result.current.feedback).toBe(23);
    expect(result.current.isError).toBe(false);
  });

  it('exposes loading=true and undefined values before first settle', () => {
    getNavCounts.mockImplementation(() => new Promise(() => {}));

    const { result } = renderHook(() => useKbNavCounts(), { wrapper });

    expect(result.current.loading).toBe(true);
    expect(result.current.queue).toBeUndefined();
    expect(result.current.feedback).toBeUndefined();
  });

  it('sets isError=true when the fetch throws', async () => {
    getNavCounts.mockRejectedValueOnce(new Error('boom'));

    const { result } = renderHook(() => useKbNavCounts(), { wrapper });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.queue).toBeUndefined();
    expect(result.current.feedback).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
cd apps/web && pnpm test src/hooks/admin/__tests__/useKbNavCounts.test.ts
```

Expected: error — `useKbNavCounts` not found.

- [ ] **Step 3: Create the hook**

Create `apps/web/src/hooks/admin/useKbNavCounts.ts`:

```ts
import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';

export interface KbNavCounts {
  readonly queue: number | undefined;
  readonly feedback: number | undefined;
  readonly loading: boolean;
  readonly isError: boolean;
}

export function useKbNavCounts(): KbNavCounts {
  const query = useQuery({
    queryKey: ['admin', 'kb', 'nav-counts'] as const,
    queryFn: ({ signal }) => api.adminKnowledgeBase.getNavCounts({ signal }),
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
    staleTime: 15_000,
    retry: 1,
    placeholderData: keepPreviousData,
  });

  return {
    queue: query.data?.processingQueue,
    feedback: query.data?.feedback7d,
    loading: query.isLoading,
    isError: query.isError,
  };
}
```

- [ ] **Step 4: Run the tests to confirm they pass**

```bash
cd apps/web && pnpm test src/hooks/admin/__tests__/useKbNavCounts.test.ts
```

Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/admin/
git commit -m "feat(admin-kb): #1655 add useKbNavCounts hook"
```

---

## Task 9: Create `KbCountBadge` component with tests

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/KbCountBadge.tsx`
- Test (new): `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbCountBadge.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbCountBadge.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { KbCountBadge } from '../KbCountBadge';

describe('KbCountBadge', () => {
  it('renders a loading skeleton when loading and count is undefined', () => {
    const { container } = render(
      <KbCountBadge count={undefined} loading={true} testId="badge" />
    );
    expect(container.querySelector('[data-testid="badge-loading"]')).toBeInTheDocument();
  });

  it('renders 0 with muted style when count is 0', () => {
    render(<KbCountBadge count={0} loading={false} testId="badge" />);
    const badge = screen.getByTestId('badge');
    expect(badge).toHaveTextContent('0');
    expect(badge.className).toContain('bg-muted');
  });

  it('renders 23 with amber style when count is greater than 0', () => {
    render(<KbCountBadge count={23} loading={false} testId="badge" />);
    const badge = screen.getByTestId('badge');
    expect(badge).toHaveTextContent('23');
    expect(badge.className).toContain('bg-amber-500/15');
  });

  it('renders "99+" when count exceeds 99', () => {
    render(<KbCountBadge count={150} loading={false} testId="badge" />);
    expect(screen.getByTestId('badge')).toHaveTextContent('99+');
  });

  it('falls back to 0 with muted style when count is undefined and not loading', () => {
    render(<KbCountBadge count={undefined} loading={false} testId="badge" />);
    const badge = screen.getByTestId('badge');
    expect(badge).toHaveTextContent('0');
    expect(badge.className).toContain('bg-muted');
  });

  it('applies the title attribute from the tooltip prop', () => {
    render(
      <KbCountBadge count={1} loading={false} tooltip="hello" testId="badge" />
    );
    expect(screen.getByTestId('badge')).toHaveAttribute('title', 'hello');
  });

  it('omits data-testid when not provided', () => {
    const { container } = render(<KbCountBadge count={1} loading={false} />);
    expect(container.querySelector('[data-testid]')).toBeNull();
  });
});
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
cd apps/web && pnpm test src/components/admin/knowledge-base/explorer/__tests__/KbCountBadge.test.tsx
```

Expected: import error — module not found.

- [ ] **Step 3: Create the component**

Create `apps/web/src/components/admin/knowledge-base/explorer/KbCountBadge.tsx`:

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber accent (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import type { JSX } from 'react';

interface KbCountBadgeProps {
  readonly count: number | undefined;
  readonly loading: boolean;
  readonly tooltip?: string;
  readonly testId?: string;
}

export function KbCountBadge({
  count,
  loading,
  tooltip,
  testId,
}: KbCountBadgeProps): JSX.Element {
  if (loading && count === undefined) {
    return (
      <span
        aria-hidden="true"
        data-testid={testId ? `${testId}-loading` : undefined}
        className="ml-1.5 inline-block h-4 w-6 rounded-full bg-muted animate-pulse"
      />
    );
  }

  const safe = count ?? 0;
  const display = safe > 99 ? '99+' : safe.toString();
  const isActive = safe > 0;

  return (
    <span
      aria-label={`${safe} elementi`}
      title={tooltip}
      data-testid={testId}
      className={[
        'ml-1.5 inline-flex items-center justify-center min-w-[1.5rem] h-4 px-1.5 rounded-full',
        'text-[10px] font-bold tabular-nums leading-none',
        isActive
          ? 'bg-amber-500/15 text-amber-700 dark:text-amber-300'
          : 'bg-muted text-muted-foreground',
      ].join(' ')}
    >
      {display}
    </span>
  );
}
```

- [ ] **Step 4: Run the tests to confirm they pass**

```bash
cd apps/web && pnpm test src/components/admin/knowledge-base/explorer/__tests__/KbCountBadge.test.tsx
```

Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/KbCountBadge.tsx \
        apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbCountBadge.test.tsx
git commit -m "feat(admin-kb): #1655 add KbCountBadge component"
```

---

## Task 10: Wire `KbCountBadge` into `KbSubNav` + extend existing tests

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/KbSubNav.tsx`
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbSubNav.test.tsx`

- [ ] **Step 1: Extend the test file**

Open `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbSubNav.test.tsx` and **add at the top** of the imports:

```tsx
import { vi } from 'vitest';
```
(if not already imported, which it already is)

Then add the hook mock right after the `next/navigation` mock:

```tsx
const mockUseKbNavCounts = vi.fn();
vi.mock('@/hooks/admin/useKbNavCounts', () => ({
  useKbNavCounts: () => mockUseKbNavCounts(),
}));
```

And in `beforeEach`, reset both mocks and set a default:

```tsx
beforeEach(() => {
  mockPathname.mockReset();
  mockUseKbNavCounts.mockReset().mockReturnValue({
    queue: undefined,
    feedback: undefined,
    loading: false,
    isError: false,
  });
});
```

Add 4 new tests at the bottom of the `describe('KbSubNav', ...)` block:

```tsx
  it('renders queue badge with count from hook', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    mockUseKbNavCounts.mockReturnValue({ queue: 7, feedback: 23, loading: false, isError: false });
    render(<KbSubNav />);
    expect(screen.getByTestId('kb-nav-badge-queue')).toHaveTextContent('7');
  });

  it('renders feedback badge with count from hook', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    mockUseKbNavCounts.mockReturnValue({ queue: 7, feedback: 23, loading: false, isError: false });
    render(<KbSubNav />);
    expect(screen.getByTestId('kb-nav-badge-feedback')).toHaveTextContent('23');
  });

  it('does NOT render badges on non-counted tabs', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    mockUseKbNavCounts.mockReturnValue({ queue: 5, feedback: 5, loading: false, isError: false });
    render(<KbSubNav />);
    // 8 tabs total but only 2 with badges
    const badges = screen.queryAllByTestId(/^kb-nav-badge-/);
    expect(badges).toHaveLength(2);
  });

  it('renders skeleton when loading and counts are undefined', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    mockUseKbNavCounts.mockReturnValue({ queue: undefined, feedback: undefined, loading: true, isError: false });
    const { container } = render(<KbSubNav />);
    expect(container.querySelector('[data-testid="kb-nav-badge-queue-loading"]')).toBeInTheDocument();
    expect(container.querySelector('[data-testid="kb-nav-badge-feedback-loading"]')).toBeInTheDocument();
  });
```

- [ ] **Step 2: Run the tests to confirm new ones fail**

```bash
cd apps/web && pnpm test src/components/admin/knowledge-base/explorer/__tests__/KbSubNav.test.tsx
```

Expected: original 7 tests pass; 4 new ones fail (badges not rendered).

- [ ] **Step 3: Modify `KbSubNav.tsx` to wire the badges**

Replace the **entire body** of `apps/web/src/components/admin/knowledge-base/explorer/KbSubNav.tsx` with:

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber accent + zinc dark palette (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { KbCountBadge } from './KbCountBadge';
import { useKbNavCounts } from '@/hooks/admin/useKbNavCounts';

const KB_BASE = '/admin/knowledge-base';

type KbTabKind = 'queue' | 'feedback';

interface KbTab {
  readonly label: string;
  readonly href: string;
  readonly kind?: KbTabKind;
}

const TABS: ReadonlyArray<KbTab> = [
  { label: 'Explorer', href: KB_BASE },
  { label: 'Vector Collections', href: `${KB_BASE}/vectors` },
  { label: 'Processing Queue', href: `${KB_BASE}/queue`, kind: 'queue' },
  { label: 'RAG Pipeline', href: `${KB_BASE}/pipeline` },
  { label: 'Embedding', href: `${KB_BASE}/embedding` },
  { label: 'Feedback', href: `${KB_BASE}/feedback`, kind: 'feedback' },
  { label: 'Settings', href: `${KB_BASE}/settings` },
  { label: 'Snapshots', href: `${KB_BASE}/snapshots` },
];

const TOOLTIPS: Record<KbTabKind, string> = {
  queue: 'Job attivi (queued, in elaborazione o falliti)',
  feedback: 'Feedback ricevuti negli ultimi 7 giorni',
};

function isActive(tabHref: string, pathname: string): boolean {
  if (tabHref === KB_BASE) return pathname === KB_BASE;
  return pathname === tabHref || pathname.startsWith(`${tabHref}/`);
}

/**
 * KB sub-nav: 8 tab-link a route reali. La sezione attiva è derivata dal
 * pathname corrente (App Router). Vive dentro `knowledge-base/layout.tsx` e
 * wrappa Explorer + le 7 tool-page esistenti.
 *
 * Issue #1655: wired count badges on Processing Queue + Feedback tabs.
 */
export function KbSubNav() {
  const pathname = usePathname();
  const { queue, feedback, loading } = useKbNavCounts();

  return (
    <nav
      aria-label="Knowledge Base sezioni"
      className="border-b border-border/60 dark:border-zinc-700/60 -mx-6 px-6 mb-6 overflow-x-auto"
    >
      <ul className="flex gap-1 min-w-max">
        {TABS.map(tab => {
          const active = isActive(tab.href, pathname);
          const count = tab.kind === 'queue' ? queue : tab.kind === 'feedback' ? feedback : undefined;
          return (
            <li key={tab.href}>
              <Link
                href={tab.href}
                aria-current={active ? 'page' : undefined}
                className={[
                  'inline-flex items-center px-3 py-2 text-sm font-medium border-b-2 -mb-px transition-colors',
                  active
                    ? 'border-amber-500 text-foreground'
                    : 'border-transparent text-muted-foreground hover:text-foreground hover:border-border',
                ].join(' ')}
              >
                {tab.label}
                {tab.kind && (
                  <KbCountBadge
                    count={count}
                    loading={loading}
                    tooltip={TOOLTIPS[tab.kind]}
                    testId={`kb-nav-badge-${tab.kind}`}
                  />
                )}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
```

- [ ] **Step 4: Run all KbSubNav tests to confirm everything passes**

```bash
cd apps/web && pnpm test src/components/admin/knowledge-base/explorer/__tests__/KbSubNav.test.tsx
```

Expected: 11 passed (7 original + 4 new).

- [ ] **Step 5: Run the admin KB suite to check for regressions**

```bash
cd apps/web && pnpm test src/components/admin/knowledge-base
```

Expected: zero failures.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/KbSubNav.tsx \
        apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbSubNav.test.tsx
git commit -m "feat(admin-kb): #1655 wire count badges into KbSubNav"
```

---

## Task 11: E2E smoke test (Playwright)

**Files:**
- Test (new): `apps/web/e2e/admin-kb-navcounts.spec.ts`

- [ ] **Step 1: Inspect an existing admin Playwright spec for auth pattern**

```bash
ls apps/web/e2e/ | grep -i admin
```

Read one of the existing admin E2E specs to learn how `superadmin` login is performed (often via fixtures or storage state). Match the pattern.

- [ ] **Step 2: Write the smoke spec**

Create `apps/web/e2e/admin-kb-navcounts.spec.ts`:

```ts
import { test, expect } from '@playwright/test';

test.describe('admin KB sub-nav count badges (#1655)', () => {
  test('shows queue and feedback badges with numeric content', async ({ page }) => {
    // NOTE: replace this with the project's existing superadmin login helper.
    // Search for previous admin specs (e.g., apps/web/e2e/admin-*.spec.ts) for the canonical pattern.
    await page.goto('/admin/knowledge-base');

    const queueBadge = page.getByTestId('kb-nav-badge-queue');
    const feedbackBadge = page.getByTestId('kb-nav-badge-feedback');

    await expect(queueBadge).toBeVisible();
    await expect(feedbackBadge).toBeVisible();

    // Numeric or "99+" or em-dash on error
    await expect(queueBadge).toHaveText(/^\d+\+?$|^—$/);
    await expect(feedbackBadge).toHaveText(/^\d+\+?$|^—$/);
  });
});
```

If a project-specific superadmin auth fixture exists (e.g., `loginAsSuperAdmin(page)` exported from `apps/web/e2e/fixtures/auth.ts`), import it and call it before `page.goto`.

- [ ] **Step 3: Run the spec**

```bash
cd apps/web && pnpm test:e2e admin-kb-navcounts
```

Expected: 1 passed. If the test cannot authenticate, look at the most recently merged admin E2E spec for the canonical login flow and copy it.

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/admin-kb-navcounts.spec.ts
git commit -m "test(admin-kb): #1655 e2e smoke for KbSubNav count badges"
```

---

## Task 12: Push branch + open PR to `main-dev`

- [ ] **Step 1: Run full backend + frontend tests one last time**

Backend (from `apps/api`):
```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Issue\"1655\"|FullyQualifiedName~AdminKnowledgeBase|FullyQualifiedName~KbSubNav|FullyQualifiedName~GetKbNavCounts|FullyQualifiedName~ProcessingJobRepository|FullyQualifiedName~KbUserFeedbackRepository"
```

Frontend (from `apps/web`):
```bash
pnpm typecheck && pnpm lint && pnpm test
```

Expected: all passing, zero lint errors.

- [ ] **Step 2: Push the branch**

```bash
git push -u origin feature/issue-1655-kbsubnav-count-badges
```

- [ ] **Step 3: Open the PR to `main-dev`** (NOT `main`!)

```bash
gh pr create --base main-dev --title "feat(admin-kb): #1655 F3-FU-6 KbSubNav count badges (Queue/Feedback)" --body "$(cat <<'EOF'
## Summary
Closes #1655 (F3-FU-6). Adds wired count badges to the admin Knowledge Base sub-nav for the **Processing Queue** and **Feedback** tabs.

- **Queue badge**: count of jobs in `Queued + Processing + Failed`
- **Feedback badge**: count of feedback rows with `created_at >= NOW() - 7 days`
- Single endpoint `GET /api/v1/admin/kb/nav-counts` polled every 30s + on window focus via React Query
- Visual: pill badge — amber when `> 0`, muted when `0`, `99+` cap, tooltip explains the metric

Closes the last open follow-up of the F3 KB Explorer cluster (PR #1649). 5/6 follow-up already merged (#1650, #1651, #1652, #1653, #1654/#1697).

## Test plan
- [x] Unit tests: `CountByStatusesAsync` repo (4), `CountSinceAsync` repo (3), `GetKbNavCountsQueryHandler` (6)
- [x] Integration test: handler against Postgres via Testcontainers
- [x] Integration tests: endpoint (401 anon, 403 editor, 200 admin)
- [x] Frontend unit: `KbCountBadge` (7), `useKbNavCounts` (3), `KbSubNav` (11 = 7 existing + 4 new)
- [x] E2E smoke: badges visible with numeric content on `/admin/knowledge-base`
- [x] No regressions on existing KbSubNav tests

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Capture the PR URL**

Return the PR URL printed by `gh pr create` so the user can review.

- [ ] **Step 5: After PR merge — local cleanup**

(Do NOT run before the PR is merged.) Per CLAUDE.md branch hygiene rule:
```bash
git checkout main-dev && git pull && git branch -D feature/issue-1655-kbsubnav-count-badges
```

The remote branch is auto-deleted on merge per repo policy.

---

## Self-Review Notes (post-write)

**Spec coverage check** against `docs/superpowers/specs/2026-05-30-f3-fu-6-kbsubnav-count-badges-design.md`:

| Spec section | Task |
|---|---|
| DTO `KbNavCountsDto` | Task 3 |
| Query + Handler | Task 4 |
| Repository extensions | Task 1, Task 2 |
| Endpoint registration | Task 6 |
| API client `getNavCounts` | Task 7 |
| Hook `useKbNavCounts` | Task 8 |
| Component `KbCountBadge` | Task 9 |
| Wiring into `KbSubNav` | Task 10 |
| Error handling (fail-fast BE) | Tasks 4–6 (covered by tests) |
| Error handling (FE keepPreviousData) | Task 8 |
| 6 unit handler tests | Task 4 |
| Integration handler test | Task 5 |
| 3 endpoint integration tests | Task 6 |
| 7 KbCountBadge tests | Task 9 |
| 3 useKbNavCounts tests | Task 8 |
| 4 extra KbSubNav tests | Task 10 |
| 1 E2E smoke | Task 11 |

**Placeholder scan**: no `TBD`, `TODO`, `implement later`. Two notes ("verify file path X" and "match the project's auth helper") describe **how to locate** existing infrastructure that the engineer must discover — these are not work placeholders, they are exploration directives.

**Type consistency**: `KbNavCountsDto` properties `ProcessingQueue/Feedback7d/AsOf` (BE) → `processingQueue/feedback7d/asOf` (FE) — System.Text.Json camelCase serialization handles this; FE interface matches. `useKbNavCounts` returns `queue/feedback/loading/isError` (renamed for ergonomics); component prop `count` matches in both consumer call sites.

**Branch hygiene**: branch was created from `main-dev` in the brainstorming phase (commit `590c04646`); spec already committed there. All tasks commit incrementally to the same branch.

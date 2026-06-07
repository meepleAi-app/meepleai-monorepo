using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Unit tests for the <see cref="CatalogSyncRun"/> aggregate root (#1861, F4-A6 BE).
/// Covers the run lifecycle state machine, counter invariants, and error capture.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSyncRunTests
{
    private const string Title = "BGG full sync";

    // ============================================================
    // 1. Enqueue
    // ============================================================

    [Fact]
    public void Enqueue_WithCronTrigger_CreatesQueuedRunWithDefaults()
    {
        var run = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, Title, triggeredBy: null);

        run.Id.Should().NotBe(Guid.Empty);
        run.Provider.Should().Be(CatalogSyncProvider.BggApi);
        run.Status.Should().Be(CatalogSyncStatus.Queued);
        run.Title.Should().Be(Title);
        run.TriggeredByUserId.Should().BeNull();
        run.ItemsAdded.Should().Be(0);
        run.ItemsUpdated.Should().Be(0);
        run.ItemsFailed.Should().Be(0);
        run.ErrorCode.Should().BeNull();
        run.ErrorDetail.Should().BeNull();
        run.LogTailJsonPath.Should().BeNull();
        run.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        run.StartedAt.Should().BeNull();
        run.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Enqueue_WithUserTrigger_StoresUserId()
    {
        var userId = Guid.NewGuid();

        var run = CatalogSyncRun.Enqueue(CatalogSyncProvider.Manual, "Manual: Wingspan", triggeredBy: userId);

        run.TriggeredByUserId.Should().Be(userId);
    }

    [Fact]
    public void Enqueue_WithEmptyTitle_Throws()
    {
        var act = () => CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "", null);

        act.Should().Throw<ArgumentException>().WithMessage("*Title*");
    }

    [Fact]
    public void Enqueue_WithWhitespaceTitle_Throws()
    {
        var act = () => CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "   ", null);

        act.Should().Throw<ArgumentException>().WithMessage("*Title*");
    }

    [Fact]
    public void Enqueue_WithOverLongTitle_Throws()
    {
        var longTitle = new string('a', 201);

        var act = () => CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, longTitle, null);

        act.Should().Throw<ArgumentException>().WithMessage("*200*");
    }

    [Fact]
    public void Enqueue_WithEmptyGuidTriggeredBy_Throws()
    {
        var act = () => CatalogSyncRun.Enqueue(CatalogSyncProvider.Manual, Title, Guid.Empty);

        act.Should().Throw<ArgumentException>().WithMessage("*Guid.Empty*");
    }

    // ============================================================
    // 2. MarkRunning
    // ============================================================

    [Fact]
    public void MarkRunning_FromQueued_TransitionsToRunning_StampsStartedAt()
    {
        var run = QueuedRun();

        run.MarkRunning();

        run.Status.Should().Be(CatalogSyncStatus.Running);
        run.StartedAt.Should().NotBeNull();
        run.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        run.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void MarkRunning_WhenAlreadyRunning_Throws()
    {
        var run = RunningRun();

        var act = () => run.MarkRunning();

        act.Should().Throw<InvalidCatalogSyncRunTransitionException>()
            .Which.CurrentStatus.Should().Be(CatalogSyncStatus.Running);
    }

    [Fact]
    public void MarkRunning_FromSuccess_Throws()
    {
        var run = RunningRun();
        run.Complete();

        var act = () => run.MarkRunning();

        act.Should().Throw<InvalidCatalogSyncRunTransitionException>();
    }

    // ============================================================
    // 3. Counters
    // ============================================================

    [Fact]
    public void RecordItemsAdded_WhileRunning_IncrementsCounter()
    {
        var run = RunningRun();

        run.RecordItemsAdded(5);
        run.RecordItemsAdded(3);

        run.ItemsAdded.Should().Be(8);
    }

    [Fact]
    public void RecordItemsUpdated_WhileRunning_IncrementsCounter()
    {
        var run = RunningRun();

        run.RecordItemsUpdated(847);

        run.ItemsUpdated.Should().Be(847);
    }

    [Fact]
    public void RecordItemsFailed_WhileRunning_IncrementsCounter()
    {
        var run = RunningRun();

        run.RecordItemsFailed(14);

        run.ItemsFailed.Should().Be(14);
    }

    [Fact]
    public void RecordItems_WithNegativeCount_Throws()
    {
        var run = RunningRun();

        var actAdded = () => run.RecordItemsAdded(-1);
        var actUpdated = () => run.RecordItemsUpdated(-1);
        var actFailed = () => run.RecordItemsFailed(-1);

        actAdded.Should().Throw<ArgumentOutOfRangeException>();
        actUpdated.Should().Throw<ArgumentOutOfRangeException>();
        actFailed.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RecordItems_WhenQueued_Throws()
    {
        var run = QueuedRun();

        var act = () => run.RecordItemsAdded();

        act.Should().Throw<InvalidCatalogSyncRunTransitionException>()
            .Which.CurrentStatus.Should().Be(CatalogSyncStatus.Queued);
    }

    [Fact]
    public void RecordItems_AfterTerminal_Throws()
    {
        var run = RunningRun();
        run.Complete();

        var act = () => run.RecordItemsAdded();

        act.Should().Throw<InvalidCatalogSyncRunTransitionException>();
    }

    // ============================================================
    // 4. Complete (Running → Success)
    // ============================================================

    [Fact]
    public void Complete_FromRunning_TransitionsToSuccess_StampsCompletedAt()
    {
        var run = RunningRun();

        run.Complete();

        run.Status.Should().Be(CatalogSyncStatus.Success);
        run.CompletedAt.Should().NotBeNull();
        run.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        run.ErrorCode.Should().BeNull();
        run.ErrorDetail.Should().BeNull();
    }

    [Fact]
    public void Complete_FromQueued_Throws()
    {
        var run = QueuedRun();

        var act = () => run.Complete();

        act.Should().Throw<InvalidCatalogSyncRunTransitionException>();
    }

    // ============================================================
    // 5. Fail (Queued | Running → Failed)
    // ============================================================

    [Fact]
    public void Fail_FromRunning_CapturesErrorAndStampsCompletedAt()
    {
        var run = RunningRun();

        run.Fail("BGG_API_RATE_LIMIT_429", "4 retry esauriti");

        run.Status.Should().Be(CatalogSyncStatus.Failed);
        run.ErrorCode.Should().Be("BGG_API_RATE_LIMIT_429");
        run.ErrorDetail.Should().Be("4 retry esauriti");
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_FromQueued_CapturesErrorWithoutStartedAt()
    {
        var run = QueuedRun();

        run.Fail("PIPELINE_ERROR", "Worker crashed before pickup");

        run.Status.Should().Be(CatalogSyncStatus.Failed);
        run.StartedAt.Should().BeNull();
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_FromTerminal_Throws()
    {
        var run = RunningRun();
        run.Complete();

        var act = () => run.Fail("CODE", "detail");

        act.Should().Throw<InvalidCatalogSyncRunTransitionException>();
    }

    [Fact]
    public void Fail_WithEmptyErrorCode_Throws()
    {
        var run = RunningRun();

        var act = () => run.Fail("", "detail");

        act.Should().Throw<ArgumentException>().WithMessage("*Error code*");
    }

    [Fact]
    public void Fail_WithEmptyErrorDetail_Throws()
    {
        var run = RunningRun();

        var act = () => run.Fail("CODE", "");

        act.Should().Throw<ArgumentException>().WithMessage("*Error detail*");
    }

    // ============================================================
    // 6. TimeOut (Running → TimedOut)
    // ============================================================

    [Fact]
    public void TimeOut_FromRunning_CapturesDetailWithSyncTimeoutCode()
    {
        var run = RunningRun();

        run.TimeOut("Exceeded 10min watchdog");

        run.Status.Should().Be(CatalogSyncStatus.TimedOut);
        run.ErrorCode.Should().Be("SYNC_TIMEOUT");
        run.ErrorDetail.Should().Be("Exceeded 10min watchdog");
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void TimeOut_FromQueued_Throws()
    {
        var run = QueuedRun();

        var act = () => run.TimeOut("detail");

        act.Should().Throw<InvalidCatalogSyncRunTransitionException>();
    }

    // ============================================================
    // 7. AttachLogTail
    // ============================================================

    [Fact]
    public void AttachLogTail_WhileRunning_SetsPath()
    {
        var run = RunningRun();

        run.AttachLogTail("data/catalog-sync-logs/abc.log");

        run.LogTailJsonPath.Should().Be("data/catalog-sync-logs/abc.log");
    }

    [Fact]
    public void AttachLogTail_AfterComplete_SetsPath()
    {
        var run = RunningRun();
        run.Complete();

        run.AttachLogTail("blob://logs/run-1.log");

        run.LogTailJsonPath.Should().Be("blob://logs/run-1.log");
    }

    [Fact]
    public void AttachLogTail_FromQueued_Throws()
    {
        var run = QueuedRun();

        var act = () => run.AttachLogTail("data/logs/x.log");

        act.Should().Throw<InvalidCatalogSyncRunTransitionException>();
    }

    [Fact]
    public void AttachLogTail_WithEmptyPath_Throws()
    {
        var run = RunningRun();

        var act = () => run.AttachLogTail("");

        act.Should().Throw<ArgumentException>();
    }

    // ============================================================
    // 8. Reconstitute (repository hydration)
    // ============================================================

    [Fact]
    public void Reconstitute_HydratesAllProperties()
    {
        var id = Guid.NewGuid();
        var triggeredBy = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-3);
        var startedAt = createdAt.AddSeconds(2);
        var completedAt = startedAt.AddMinutes(4);

        var run = CatalogSyncRun.Reconstitute(
            id: id,
            provider: CatalogSyncProvider.CsvImport,
            status: CatalogSyncStatus.Success,
            title: "CSV bulk: designers-curation-v3.csv",
            triggeredByUserId: triggeredBy,
            itemsAdded: 142,
            itemsUpdated: 0,
            itemsFailed: 0,
            errorCode: null,
            errorDetail: null,
            logTailJsonPath: "data/catalog-sync-logs/abc.log",
            createdAt: createdAt,
            startedAt: startedAt,
            completedAt: completedAt);

        run.Id.Should().Be(id);
        run.Provider.Should().Be(CatalogSyncProvider.CsvImport);
        run.Status.Should().Be(CatalogSyncStatus.Success);
        run.Title.Should().Be("CSV bulk: designers-curation-v3.csv");
        run.TriggeredByUserId.Should().Be(triggeredBy);
        run.ItemsAdded.Should().Be(142);
        run.LogTailJsonPath.Should().Be("data/catalog-sync-logs/abc.log");
        run.CreatedAt.Should().Be(createdAt);
        run.StartedAt.Should().Be(startedAt);
        run.CompletedAt.Should().Be(completedAt);
    }

    // ============================================================
    // Test fixtures
    // ============================================================

    private static CatalogSyncRun QueuedRun(CatalogSyncProvider provider = CatalogSyncProvider.BggApi)
        => CatalogSyncRun.Enqueue(provider, Title, triggeredBy: null);

    private static CatalogSyncRun RunningRun(CatalogSyncProvider provider = CatalogSyncProvider.BggApi)
    {
        var run = QueuedRun(provider);
        run.MarkRunning();
        return run;
    }
}

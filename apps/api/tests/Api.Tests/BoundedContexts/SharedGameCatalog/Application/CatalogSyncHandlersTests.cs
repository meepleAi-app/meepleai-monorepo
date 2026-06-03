using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Unit tests for the 4 catalog-sync CQRS handlers (#1861 Phase 3).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSyncHandlersTests
{
    // ============================================================
    // TriggerCatalogSyncCommandHandler
    // ============================================================

    [Fact]
    public async Task Trigger_NoRunningRun_CreatesQueuedRunAndReturnsId()
    {
        var repo = new Mock<ICatalogSyncRunRepository>();
        repo.Setup(r => r.GetCurrentRunningAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CatalogSyncRun?)null);
        var uow = new Mock<IUnitOfWork>();
        var handler = new TriggerCatalogSyncCommandHandler(
            repo.Object, uow.Object, NullLogger<TriggerCatalogSyncCommandHandler>.Instance);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(
            new TriggerCatalogSyncCommand(CatalogSyncProvider.BggApi, userId),
            CancellationToken.None);

        result.RunId.Should().NotBe(Guid.Empty);
        result.Status.Should().Be("queued");
        repo.Verify(r => r.AddAsync(
            It.Is<CatalogSyncRun>(run =>
                run.Provider == CatalogSyncProvider.BggApi
                && run.Status == CatalogSyncStatus.Queued
                && run.TriggeredByUserId == userId
                && run.Title == "BGG full sync"),
            It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Trigger_RunAlreadyRunning_ThrowsConflictException()
    {
        var existingRun = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "in-flight", null);
        existingRun.MarkRunning();

        var repo = new Mock<ICatalogSyncRunRepository>();
        repo.Setup(r => r.GetCurrentRunningAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existingRun);
        var uow = new Mock<IUnitOfWork>();
        var handler = new TriggerCatalogSyncCommandHandler(
            repo.Object, uow.Object, NullLogger<TriggerCatalogSyncCommandHandler>.Instance);

        var act = () => handler.Handle(
            new TriggerCatalogSyncCommand(CatalogSyncProvider.BggApi, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already in progress*");
        repo.Verify(r => r.AddAsync(It.IsAny<CatalogSyncRun>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Trigger_ConstructorRejectsNullDependencies()
    {
        var act1 = () => new TriggerCatalogSyncCommandHandler(
            null!, Mock.Of<IUnitOfWork>(), NullLogger<TriggerCatalogSyncCommandHandler>.Instance);
        var act2 = () => new TriggerCatalogSyncCommandHandler(
            Mock.Of<ICatalogSyncRunRepository>(), null!, NullLogger<TriggerCatalogSyncCommandHandler>.Instance);
        var act3 = () => new TriggerCatalogSyncCommandHandler(
            Mock.Of<ICatalogSyncRunRepository>(), Mock.Of<IUnitOfWork>(), null!);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
        act3.Should().Throw<ArgumentNullException>();
    }

    // ============================================================
    // TriggerCatalogSyncCommandValidator
    // ============================================================

    [Fact]
    public void Validator_AcceptsValidCommand()
    {
        var v = new TriggerCatalogSyncCommandValidator();
        var result = v.Validate(new TriggerCatalogSyncCommand(CatalogSyncProvider.BggApi, Guid.NewGuid()));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_RejectsEmptyUserId()
    {
        var v = new TriggerCatalogSyncCommandValidator();
        var result = v.Validate(new TriggerCatalogSyncCommand(CatalogSyncProvider.BggApi, Guid.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TriggeredByUserId");
    }

    [Fact]
    public void Validator_RejectsInvalidProviderEnum()
    {
        var v = new TriggerCatalogSyncCommandValidator();
        var result = v.Validate(new TriggerCatalogSyncCommand((CatalogSyncProvider)99, Guid.NewGuid()));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Provider");
    }

    // ============================================================
    // GetCatalogSyncStatusQueryHandler
    // ============================================================

    [Fact]
    public async Task Status_RunRunning_ReturnsRunningWithCurrentRun()
    {
        var current = MakeRunning("currently active");
        var repo = new Mock<ICatalogSyncRunRepository>();
        repo.Setup(r => r.GetCurrentRunningAsync(It.IsAny<CancellationToken>())).ReturnsAsync(current);
        repo.Setup(r => r.GetLatestCompletedAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CatalogSyncRun?)null);
        var games = new Mock<ISharedGameRepository>();
        games.Setup(g => g.CountAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(4812);
        var handler = new GetCatalogSyncStatusQueryHandler(repo.Object, games.Object);

        var result = await handler.Handle(new GetCatalogSyncStatusQuery(), CancellationToken.None);

        result.Status.Should().Be("running");
        result.CurrentRun.Should().NotBeNull();
        result.CurrentRun!.Id.Should().Be(current.Id);
        result.LastRun.Should().BeNull();
        result.Cumulative.GamesTotal.Should().Be(4812);
    }

    [Fact]
    public async Task Status_IdleWithLastRun_ReturnsIdleAndLastRun()
    {
        var last = MakeSuccess("yesterday's run");
        var repo = new Mock<ICatalogSyncRunRepository>();
        repo.Setup(r => r.GetCurrentRunningAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CatalogSyncRun?)null);
        repo.Setup(r => r.GetLatestCompletedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(last);
        var games = new Mock<ISharedGameRepository>();
        games.Setup(g => g.CountAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(100);
        var handler = new GetCatalogSyncStatusQueryHandler(repo.Object, games.Object);

        var result = await handler.Handle(new GetCatalogSyncStatusQuery(), CancellationToken.None);

        result.Status.Should().Be("idle");
        result.CurrentRun.Should().BeNull();
        result.LastRun.Should().NotBeNull();
        result.LastRun!.Id.Should().Be(last.Id);
    }

    [Fact]
    public async Task Status_EmptyDb_ReturnsNeverRun()
    {
        var repo = new Mock<ICatalogSyncRunRepository>();
        repo.Setup(r => r.GetCurrentRunningAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CatalogSyncRun?)null);
        repo.Setup(r => r.GetLatestCompletedAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CatalogSyncRun?)null);
        var games = new Mock<ISharedGameRepository>();
        games.Setup(g => g.CountAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var handler = new GetCatalogSyncStatusQueryHandler(repo.Object, games.Object);

        var result = await handler.Handle(new GetCatalogSyncStatusQuery(), CancellationToken.None);

        result.Status.Should().Be("never_run");
        result.LastRun.Should().BeNull();
        result.CurrentRun.Should().BeNull();
        result.Cumulative.GamesTotal.Should().Be(0);
    }

    // ============================================================
    // GetCatalogSyncRunsQueryHandler
    // ============================================================

    [Fact]
    public async Task Runs_PagedQuery_MapsAndComputesHasMore()
    {
        var items = new[] { MakeSuccess("r1"), MakeSuccess("r2") };
        var repo = new Mock<ICatalogSyncRunRepository>();
        repo.Setup(r => r.GetPagedAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 30));
        var handler = new GetCatalogSyncRunsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetCatalogSyncRunsQuery(1, 2), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(30);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task Runs_LastPage_HasMoreFalse()
    {
        var items = new[] { MakeSuccess("r1") };
        var repo = new Mock<ICatalogSyncRunRepository>();
        repo.Setup(r => r.GetPagedAsync(3, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 21));
        var handler = new GetCatalogSyncRunsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetCatalogSyncRunsQuery(3, 10), CancellationToken.None);

        result.HasMore.Should().BeFalse();
        result.Total.Should().Be(21);
    }

    [Fact]
    public void RunsValidator_RejectsInvalidPaging()
    {
        var v = new GetCatalogSyncRunsQueryValidator();

        v.Validate(new GetCatalogSyncRunsQuery(0, 10)).IsValid.Should().BeFalse();
        v.Validate(new GetCatalogSyncRunsQuery(1, 0)).IsValid.Should().BeFalse();
        v.Validate(new GetCatalogSyncRunsQuery(1, 101)).IsValid.Should().BeFalse();
        v.Validate(new GetCatalogSyncRunsQuery(1, 50)).IsValid.Should().BeTrue();
    }

    // ============================================================
    // GetCatalogSyncRunLogsQueryHandler
    // ============================================================

    [Fact]
    public async Task Logs_RunNotFound_ReturnsNull()
    {
        var repo = new Mock<ICatalogSyncRunRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CatalogSyncRun?)null);
        var handler = new GetCatalogSyncRunLogsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetCatalogSyncRunLogsQuery(Guid.NewGuid(), 100), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Logs_RunFoundButNoLogPath_ReturnsLogsUnavailable()
    {
        var run = MakeSuccess("no logs run");
        var repo = new Mock<ICatalogSyncRunRepository>();
        repo.Setup(r => r.GetByIdAsync(run.Id, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        var handler = new GetCatalogSyncRunLogsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetCatalogSyncRunLogsQuery(run.Id, 100), CancellationToken.None);

        result.Should().NotBeNull();
        result!.RunId.Should().Be(run.Id);
        result.LogsAvailable.Should().BeFalse();
        result.LogsUnavailableReason.Should().Contain("No log path");
        result.Logs.Should().BeEmpty();
    }

    [Fact]
    public async Task Logs_FailedRunHasErrorCodeAndDetail()
    {
        var failed = MakeFailed();
        var repo = new Mock<ICatalogSyncRunRepository>();
        repo.Setup(r => r.GetByIdAsync(failed.Id, It.IsAny<CancellationToken>())).ReturnsAsync(failed);
        var handler = new GetCatalogSyncRunLogsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetCatalogSyncRunLogsQuery(failed.Id, 50), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(CatalogSyncStatus.Failed);
        result.ErrorCode.Should().Be("BGG_API_RATE_LIMIT_429");
        result.ErrorDetail.Should().Be("4 retry esauriti");
    }

    [Fact]
    public async Task Logs_FileExistsReturnsTailedLines()
    {
        var run = MakeSuccess("tailable run");
        var tempLog = Path.Combine(Path.GetTempPath(), $"catalog-test-{Guid.NewGuid():N}.log");
        try
        {
            await File.WriteAllLinesAsync(tempLog, Enumerable.Range(1, 50).Select(i => $"line {i}"));
            run.AttachLogTail(tempLog);

            var repo = new Mock<ICatalogSyncRunRepository>();
            repo.Setup(r => r.GetByIdAsync(run.Id, It.IsAny<CancellationToken>())).ReturnsAsync(run);
            var handler = new GetCatalogSyncRunLogsQueryHandler(repo.Object);

            var result = await handler.Handle(new GetCatalogSyncRunLogsQuery(run.Id, 5), CancellationToken.None);

            result.Should().NotBeNull();
            result!.LogsAvailable.Should().BeTrue();
            result.Logs.Should().HaveCount(5);
            result.Logs[0].Should().Be("line 46");
            result.Logs[4].Should().Be("line 50");
        }
        finally
        {
            if (File.Exists(tempLog)) File.Delete(tempLog);
        }
    }

    [Fact]
    public void LogsValidator_RejectsBadInput()
    {
        var v = new GetCatalogSyncRunLogsQueryValidator();
        v.Validate(new GetCatalogSyncRunLogsQuery(Guid.Empty, 100)).IsValid.Should().BeFalse();
        v.Validate(new GetCatalogSyncRunLogsQuery(Guid.NewGuid(), 0)).IsValid.Should().BeFalse();
        v.Validate(new GetCatalogSyncRunLogsQuery(Guid.NewGuid(), 10_001)).IsValid.Should().BeFalse();
        v.Validate(new GetCatalogSyncRunLogsQuery(Guid.NewGuid(), 100)).IsValid.Should().BeTrue();
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static CatalogSyncRun MakeRunning(string title = "running")
    {
        var run = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, title, null);
        run.MarkRunning();
        return run;
    }

    private static CatalogSyncRun MakeSuccess(string title = "success")
    {
        var run = MakeRunning(title);
        run.Complete();
        return run;
    }

    private static CatalogSyncRun MakeFailed()
    {
        var run = MakeRunning("failed");
        run.Fail("BGG_API_RATE_LIMIT_429", "4 retry esauriti");
        return run;
    }
}

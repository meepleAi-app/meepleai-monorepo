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
    public async Task Handle_RunsCountQueriesSequentially()
    {
        // Regression guard for the KB nav-counts 500: both repositories share the same
        // scoped MeepleAiDbContext, which is NOT thread-safe. The handler must await the
        // two counts sequentially — running them concurrently (Task.WhenAll) throws
        // "A second operation was started on this context instance...". So while the
        // queue count is still pending, the feedback count must NOT have started.
        var queueTcs = new TaskCompletionSource<int>();
        _jobs.CountByStatusesAsync(Arg.Any<IReadOnlyList<JobStatus>>(), Arg.Any<CancellationToken>())
            .Returns(queueTcs.Task);
        _feedback.CountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(9);

        var task = _sut.Handle(new GetKbNavCountsQuery(), CancellationToken.None);

        // Give the scheduler a chance to reach the first (still-pending) await.
        await Task.Yield();

        // Sequential: the feedback count must not run while the queue count is pending.
        await _feedback.DidNotReceive().CountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());

        // Completing the queue count lets the feedback count run.
        queueTcs.SetResult(4);
        var result = await task;

        await _feedback.Received(1).CountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        result.ProcessingQueue.Should().Be(4);
        result.Feedback7d.Should().Be(9);
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

    [Theory]
    [InlineData("jobs")]
    [InlineData("feedback")]
    [InlineData("clock")]
    public void Constructor_NullDependency_ThrowsArgumentNullException(string paramName)
    {
        Action act = paramName switch
        {
            "jobs" => () => new GetKbNavCountsQueryHandler(null!, _feedback, _clock),
            "feedback" => () => new GetKbNavCountsQueryHandler(_jobs, null!, _clock),
            "clock" => () => new GetKbNavCountsQueryHandler(_jobs, _feedback, null!),
            _ => throw new InvalidOperationException()
        };
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName);
    }
}

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

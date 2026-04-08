using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;
using Api.BoundedContexts.SessionTracking.Infrastructure.Scheduling;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Unit;

public class AutoSaveSessionJobHealthTests
{
    [Fact]
    public async Task Execute_OnSuccess_RecordsRunInTracker()
    {
        var tracker = new AutoSaveHealthTracker(new FakeTimeProvider(DateTimeOffset.UtcNow));
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<AutoSaveSessionCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var job = new AutoSaveSessionJob(mediator.Object, NullLogger<AutoSaveSessionJob>.Instance, tracker);
        var ctx = BuildContext(Guid.NewGuid());

        await job.Execute(ctx);

        tracker.GetLastRunAgeSeconds().Should().Be(0);
    }

    [Fact]
    public async Task Execute_OnHandledException_StillRecordsRun()
    {
        var tracker = new AutoSaveHealthTracker(new FakeTimeProvider(DateTimeOffset.UtcNow));
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<AutoSaveSessionCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        var job = new AutoSaveSessionJob(mediator.Object, NullLogger<AutoSaveSessionJob>.Instance, tracker);
        var ctx = BuildContext(Guid.NewGuid());

        await job.Execute(ctx);

        tracker.GetLastRunAgeSeconds().Should().Be(0);
    }

    [Fact]
    public async Task Execute_OnInvalidSessionId_DoesNotRecordRun()
    {
        var tracker = new AutoSaveHealthTracker(new FakeTimeProvider(DateTimeOffset.UtcNow));
        var mediator = new Mock<IMediator>();

        var job = new AutoSaveSessionJob(mediator.Object, NullLogger<AutoSaveSessionJob>.Instance, tracker);
        var ctx = BuildContext(sessionIdRaw: "not-a-guid");

        await job.Execute(ctx);

        tracker.GetLastRunAgeSeconds().Should().BeNull();
        mediator.Verify(
            m => m.Send(It.IsAny<AutoSaveSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static IJobExecutionContext BuildContext(Guid? sessionId = null, string? sessionIdRaw = null)
    {
        var dataMap = new JobDataMap();
        dataMap.Put(AutoSaveSessionJob.SessionIdKey, sessionIdRaw ?? sessionId!.Value.ToString());

        var ctx = new Mock<IJobExecutionContext>();
        ctx.SetupGet(c => c.MergedJobDataMap).Returns(dataMap);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}

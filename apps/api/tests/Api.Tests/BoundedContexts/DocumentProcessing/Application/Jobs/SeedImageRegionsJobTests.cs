using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// #3435 (SP1 slice 2): the Quartz trigger is a thin dispatcher — it must resolve IMediator from a
/// fresh scope and Send RunImageRegionSeedBatchCommand exactly once with the job's cancellation token.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3435")]
public sealed class SeedImageRegionsJobTests
{
    [Fact]
    public async Task Execute_DispatchesRunImageRegionSeedBatchCommand_OnceWithJobToken()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<RunImageRegionSeedBatchCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunImageRegionSeedBatchResult(
                Enabled: false, Processed: 0, TotalRegionsSeeded: 0, Failed: 0));

        var services = new ServiceCollection();
        services.AddScoped(_ => mediator.Object);
        using var serviceProvider = services.BuildServiceProvider();

        var job = new SeedImageRegionsJob(serviceProvider, NullLogger<SeedImageRegionsJob>.Instance);

        using var cts = new CancellationTokenSource();
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(cts.Token);

        await job.Execute(context.Object);

        mediator.Verify(
            m => m.Send(It.IsAny<RunImageRegionSeedBatchCommand>(), cts.Token),
            Times.Once);
    }
}

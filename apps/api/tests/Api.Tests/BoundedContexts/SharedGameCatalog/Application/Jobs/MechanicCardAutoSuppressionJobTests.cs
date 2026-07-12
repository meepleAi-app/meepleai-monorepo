using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardAutoSuppressionJobTests
{
    [Fact]
    public async Task Execute_SendsRunCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<RunMechanicCardAutoSuppressionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoSuppressionResult(2, 1));

        var services = new ServiceCollection();
        services.AddSingleton(mediator.Object);
        await using var provider = services.BuildServiceProvider();

        var job = new MechanicCardAutoSuppressionJob(provider, NullLogger<MechanicCardAutoSuppressionJob>.Instance);

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(context.Object);

        mediator.Verify(
            m => m.Send(It.IsAny<RunMechanicCardAutoSuppressionCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

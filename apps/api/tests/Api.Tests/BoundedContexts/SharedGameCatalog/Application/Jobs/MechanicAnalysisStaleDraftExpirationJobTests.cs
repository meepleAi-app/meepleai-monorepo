using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisStaleDraftExpirationJobTests
{
    private static IJobExecutionContext NewContext()
    {
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    [Fact]
    public async Task Execute_SendsTheBatchCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ExpireStaleMechanicAnalysisDraftsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpireStaleDraftsResult(Found: 2, Expired: 2));

        var services = new ServiceCollection();
        services.AddSingleton(mediator.Object);
        await using var provider = services.BuildServiceProvider();

        var job = new MechanicAnalysisStaleDraftExpirationJob(
            provider, NullLogger<MechanicAnalysisStaleDraftExpirationJob>.Instance);

        await job.Execute(NewContext());

        mediator.Verify(
            m => m.Send(It.IsAny<ExpireStaleMechanicAnalysisDraftsCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WhenTheCommandFails_LogsAndDoesNotThrow()
    {
        // Un job che lancia non deve fermare lo scheduler: il giro dopo riparte da uno scope nuovo.
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ExpireStaleMechanicAnalysisDraftsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var services = new ServiceCollection();
        services.AddSingleton(mediator.Object);
        await using var provider = services.BuildServiceProvider();

        var job = new MechanicAnalysisStaleDraftExpirationJob(
            provider, NullLogger<MechanicAnalysisStaleDraftExpirationJob>.Instance);

        var act = () => job.Execute(NewContext());

        await act.Should().NotThrowAsync();
    }
}

using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.Infrastructure;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class GetAiServicesStatusQueryHandlerTests
{
    private readonly IInfrastructureHealthService _healthService = Substitute.For<IInfrastructureHealthService>();
    private readonly IServiceCooldownRegistry _cooldownRegistry = Substitute.For<IServiceCooldownRegistry>();
    private readonly GetAiServicesStatusQueryHandler _handler;

    public GetAiServicesStatusQueryHandlerTests()
    {
        _handler = new GetAiServicesStatusQueryHandler(
            _healthService,
            _cooldownRegistry,
            NullLogger<GetAiServicesStatusQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsAllEightServices()
    {
        _healthService.GetAllServicesHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ServiceHealthStatus>());

        var result = await _handler.Handle(new GetAiServicesStatusQuery(), CancellationToken.None);

        Assert.Equal(8, result.Services.Count);
    }

    [Fact]
    public async Task Handle_ServiceInCooldown_SetsCanRestartFalse()
    {
        _healthService.GetAllServicesHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ServiceHealthStatus>());
        _cooldownRegistry.IsInCooldown("embedding", out Arg.Any<int>())
            .Returns(x => { x[1] = 120; return true; });

        var result = await _handler.Handle(new GetAiServicesStatusQuery(), CancellationToken.None);

        var embedding = result.Services.First(s => s.Name == "embedding");
        Assert.False(embedding.CanRestart);
        Assert.NotNull(embedding.CooldownRemainingSeconds);
    }
}

using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public sealed class RestartServiceCommandHandlerTests
{
    private readonly IDockerProxyService _dockerProxy = Substitute.For<IDockerProxyService>();
    private readonly IServiceCooldownRegistry _cooldownRegistry = Substitute.For<IServiceCooldownRegistry>();
    private readonly RestartServiceCommandHandler _handler;

    public RestartServiceCommandHandlerTests()
    {
        _handler = new RestartServiceCommandHandler(
            _dockerProxy,
            _cooldownRegistry,
            NullLogger<RestartServiceCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NotInCooldown_RestartsSuccessfully()
    {
        // Arrange
        _cooldownRegistry.IsInCooldown("embedding", out Arg.Any<int>())
            .Returns(x => { x[1] = 0; return false; });
        _dockerProxy.GetContainersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ContainerInfoDto>
            {
                new("abc123", "meepleai-embedding", "image:latest", "running", "Up 2h",
                    DateTime.UtcNow, new Dictionary<string, string>())
            });

        // Act
        var result = await _handler.Handle(
            new RestartServiceCommand("embedding"), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be("embedding");
        _cooldownRegistry.Received(1).RecordRestart("embedding");
    }

    [Fact]
    public async Task Handle_InCooldown_ThrowsConflictException()
    {
        // Arrange
        _cooldownRegistry.IsInCooldown("embedding", out Arg.Any<int>())
            .Returns(x => { x[1] = 120; return true; });

        // Act
        var act = () => _handler.Handle(
            new RestartServiceCommand("embedding"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cooldown*");
    }

    [Fact]
    public async Task Handle_ContainerNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _cooldownRegistry.IsInCooldown("embedding", out Arg.Any<int>())
            .Returns(x => { x[1] = 0; return false; });
        _dockerProxy.GetContainersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ContainerInfoDto>());

        // Act
        var act = () => _handler.Handle(
            new RestartServiceCommand("embedding"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class InvalidateCacheCommandHandlerTests
{
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly InvalidateCacheCommandHandler _handler;

    public InvalidateCacheCommandHandlerTests()
    {
        _handler = new InvalidateCacheCommandHandler(_cacheMock.Object);
    }

    [Fact]
    public async Task Handle_WithSpecificKey_InvalidatesAcrossEnvironments()
    {
        // Arrange
        var command = new InvalidateCacheCommand("feature.enabled");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - should invalidate for all environments
        _cacheMock.Verify(c => c.RemoveAsync(It.Is<string>(k => k.Contains("feature.enabled")), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task Handle_WithNullKey_InvalidatesByTag()
    {
        // Arrange
        var command = new InvalidateCacheCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _cacheMock.Verify(c => c.RemoveByTagAsync("config:category:general", It.IsAny<CancellationToken>()), Times.Once);
    }
}

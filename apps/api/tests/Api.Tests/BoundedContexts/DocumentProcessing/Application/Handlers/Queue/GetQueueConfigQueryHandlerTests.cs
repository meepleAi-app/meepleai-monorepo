using Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

[Trait("Category", TestCategories.Unit)]
public sealed class GetQueueConfigQueryHandlerTests
{
    private readonly Mock<IProcessingQueueConfigRepository> _configRepoMock = new();
    private readonly GetQueueConfigQueryHandler _handler;

    public GetQueueConfigQueryHandlerTests()
    {
        _handler = new GetQueueConfigQueryHandler(_configRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsDefaultConfig()
    {
        // Arrange
        var config = ProcessingQueueConfig.CreateDefault();
        _configRepoMock
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _handler.Handle(new GetQueueConfigQuery(), CancellationToken.None);

        // Assert
        result.IsPaused.Should().BeFalse();
        result.MaxConcurrentWorkers.Should().Be(ProcessingQueueConfig.DefaultMaxConcurrentWorkers);
    }

    [Fact]
    public async Task Handle_ReturnsPausedConfig()
    {
        // Arrange
        var config = ProcessingQueueConfig.CreateDefault();
        var userId = Guid.NewGuid();
        config.Update(isPaused: true, maxConcurrentWorkers: 5, updatedBy: userId);

        _configRepoMock
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _handler.Handle(new GetQueueConfigQuery(), CancellationToken.None);

        // Assert
        result.IsPaused.Should().BeTrue();
        result.MaxConcurrentWorkers.Should().Be(5);
        result.UpdatedBy.Should().Be(userId);
    }
}

using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

[Trait("Category", TestCategories.Unit)]
public sealed class UpdateQueueConfigCommandHandlerTests
{
    private readonly Mock<IProcessingQueueConfigRepository> _configRepoMock = new();
    private readonly UpdateQueueConfigCommandHandler _handler;

    public UpdateQueueConfigCommandHandlerTests()
    {
        _handler = new UpdateQueueConfigCommandHandler(_configRepoMock.Object);
    }

    [Fact]
    public async Task Handle_PauseQueue_UpdatesConfig()
    {
        // Arrange
        var config = ProcessingQueueConfig.CreateDefault();
        var userId = Guid.NewGuid();
        var command = new UpdateQueueConfigCommand(userId, IsPaused: true);

        _configRepoMock
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _configRepoMock.Verify(
            r => r.UpdateAsync(
                It.Is<ProcessingQueueConfig>(c => c.IsPaused && c.UpdatedBy == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateConcurrency_UpdatesConfig()
    {
        // Arrange
        var config = ProcessingQueueConfig.CreateDefault();
        var userId = Guid.NewGuid();
        var command = new UpdateQueueConfigCommand(userId, MaxConcurrentWorkers: 5);

        _configRepoMock
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _configRepoMock.Verify(
            r => r.UpdateAsync(
                It.Is<ProcessingQueueConfig>(c => c.MaxConcurrentWorkers == 5),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidConcurrency_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var config = ProcessingQueueConfig.CreateDefault();
        var command = new UpdateQueueConfigCommand(Guid.NewGuid(), MaxConcurrentWorkers: 15);

        _configRepoMock
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Handle_ZeroConcurrency_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var config = ProcessingQueueConfig.CreateDefault();
        var command = new UpdateQueueConfigCommand(Guid.NewGuid(), MaxConcurrentWorkers: 0);

        _configRepoMock
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}

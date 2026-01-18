using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.SystemConfiguration.Handlers;

public sealed class UpdateAiModelPriorityCommandHandlerTests
{
    private readonly Mock<IAiModelConfigurationRepository> _repositoryMock;
    private readonly Mock<MeepleAiDbContext> _dbMock;
    private readonly UpdateAiModelPriorityCommandHandler _handler;

    public UpdateAiModelPriorityCommandHandlerTests()
    {
        _repositoryMock = new Mock<IAiModelConfigurationRepository>();
        _dbMock = new Mock<MeepleAiDbContext>();
        _handler = new UpdateAiModelPriorityCommandHandler(_repositoryMock.Object, _dbMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesPriority()
    {
        // Arrange
        var modelId = Guid.NewGuid();
        var existingModel = AiModelConfiguration.Create("gpt-4o-mini", "GPT-4o Mini", "OpenRouter", 5);
        var command = new UpdateAiModelPriorityCommand(modelId, NewPriority: 1);

        _repositoryMock.Setup(r => r.GetByIdAsync(modelId, default))
            .ReturnsAsync(existingModel);
        _dbMock.Setup(db => db.SaveChangesAsync(default))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, default);

        // Assert
        existingModel.Priority.Should().Be(1);
        _repositoryMock.Verify(r => r.UpdateAsync(existingModel, default), Times.Once);
        _dbMock.Verify(db => db.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_ModelNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var command = new UpdateAiModelPriorityCommand(Guid.NewGuid(), NewPriority: 1);
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((AiModelConfiguration?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await _handler.Handle(command, default));
    }
}

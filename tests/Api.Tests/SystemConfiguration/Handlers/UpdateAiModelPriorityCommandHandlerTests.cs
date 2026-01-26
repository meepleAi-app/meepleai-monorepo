using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.SystemConfiguration.Handlers;

/// <summary>
/// Integration tests for UpdateAiModelPriorityCommandHandler with in-memory database.
/// ISSUE-3005: Fixed DbContext mocking issues by using InMemoryDatabase.
/// </summary>
public sealed class UpdateAiModelPriorityCommandHandlerTests : IAsyncLifetime
{
    private readonly DbContextOptions<MeepleAiDbContext> _options;
    private readonly Mock<IAiModelConfigurationRepository> _repositoryMock;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IDomainEventCollector> _mockEventCollector;
    private MeepleAiDbContext _dbContext = null!;
    private UpdateAiModelPriorityCommandHandler _handler = null!;

    public UpdateAiModelPriorityCommandHandlerTests()
    {
        _options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UpdateAiModelPriorityTest_{Guid.NewGuid()}")
            .Options;

        _repositoryMock = new Mock<IAiModelConfigurationRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockEventCollector = new Mock<IDomainEventCollector>();
        _mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
    }

    public async Task InitializeAsync()
    {
        _dbContext = new MeepleAiDbContext(_options, _mockMediator.Object, _mockEventCollector.Object);
        _handler = new UpdateAiModelPriorityCommandHandler(_repositoryMock.Object, _dbContext);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesPriority()
    {
        // Arrange
        var modelId = Guid.NewGuid();
        var existingModel = AiModelConfiguration.Create("gpt-4o-mini", "GPT-4o Mini", "OpenRouter", 5, ModelSettings.Default);
        var command = new UpdateAiModelPriorityCommand(modelId, NewPriority: 1);

        _repositoryMock.Setup(r => r.GetByIdAsync(modelId, default))
            .ReturnsAsync(existingModel);

        // Act
        await _handler.Handle(command, default);

        // Assert
        existingModel.Priority.Should().Be(1);
        _repositoryMock.Verify(r => r.UpdateAsync(existingModel, default), Times.Once);
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

using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Handlers;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Unit tests for DeleteCostScenarioCommandHandler (Issue #3725)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class DeleteCostScenarioCommandHandlerTests
{
    private readonly Mock<ICostScenarioRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<DeleteCostScenarioCommandHandler>> _loggerMock;
    private readonly DeleteCostScenarioCommandHandler _handler;

    public DeleteCostScenarioCommandHandlerTests()
    {
        _repositoryMock = new Mock<ICostScenarioRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<DeleteCostScenarioCommandHandler>>();
        _handler = new DeleteCostScenarioCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingScenario_ShouldDeleteSuccessfully()
    {
        // Arrange
        var scenarioId = Guid.NewGuid();
        var scenario = CostScenario.Create(
            name: "Test Scenario",
            strategy: "Balanced",
            modelId: "deepseek/deepseek-chat",
            messagesPerDay: 100,
            activeUsers: 10,
            avgTokensPerRequest: 1000,
            costPerRequest: 0.001m,
            dailyProjection: 1.0m,
            monthlyProjection: 30.0m,
            warnings: null,
            createdByUserId: Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario);
        _repositoryMock
            .Setup(r => r.DeleteAsync(scenario, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new DeleteCostScenarioCommand(scenarioId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(scenario, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentScenario_ShouldThrowNotFoundException()
    {
        // Arrange
        var scenarioId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CostScenario?)null);

        var command = new DeleteCostScenarioCommand(scenarioId);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

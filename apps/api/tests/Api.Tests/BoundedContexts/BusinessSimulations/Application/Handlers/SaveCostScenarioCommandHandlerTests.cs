using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Handlers;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Unit tests for SaveCostScenarioCommandHandler (Issue #3725)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class SaveCostScenarioCommandHandlerTests
{
    private readonly Mock<ICostScenarioRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<SaveCostScenarioCommandHandler>> _loggerMock;
    private readonly SaveCostScenarioCommandHandler _handler;

    public SaveCostScenarioCommandHandlerTests()
    {
        _repositoryMock = new Mock<ICostScenarioRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SaveCostScenarioCommandHandler>>();
        _handler = new SaveCostScenarioCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldSaveScenarioAndReturnId()
    {
        // Arrange
        CostScenario? capturedScenario = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<CostScenario>(), It.IsAny<CancellationToken>()))
            .Callback<CostScenario, CancellationToken>((s, _) => capturedScenario = s)
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new SaveCostScenarioCommand(
            Name: "Production Baseline",
            Strategy: "Balanced",
            ModelId: "deepseek/deepseek-chat",
            MessagesPerDay: 1000,
            ActiveUsers: 100,
            AvgTokensPerRequest: 1000,
            CostPerRequest: 0.00054m,
            DailyProjection: 54.0m,
            MonthlyProjection: 1620.0m,
            Warnings: new List<string> { "Monthly projection exceeds $1,000." },
            CreatedByUserId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        capturedScenario.Should().NotBeNull();
        capturedScenario!.Name.Should().Be("Production Baseline");
        capturedScenario.Strategy.Should().Be("Balanced");
        capturedScenario.ModelId.Should().Be("deepseek/deepseek-chat");
        capturedScenario.MessagesPerDay.Should().Be(1000);
        capturedScenario.ActiveUsers.Should().Be(100);
        capturedScenario.CostPerRequest.Should().Be(0.00054m);
        capturedScenario.MonthlyProjection.Should().Be(1620.0m);
        capturedScenario.Warnings.Should().Contain("Monthly projection exceeds");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyWarnings_ShouldSaveNullWarnings()
    {
        // Arrange
        CostScenario? capturedScenario = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<CostScenario>(), It.IsAny<CancellationToken>()))
            .Callback<CostScenario, CancellationToken>((s, _) => capturedScenario = s)
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new SaveCostScenarioCommand(
            Name: "Free Tier",
            Strategy: "Fast",
            ModelId: "meta-llama/llama-3.3-70b-instruct:free",
            MessagesPerDay: 100,
            ActiveUsers: 10,
            AvgTokensPerRequest: 500,
            CostPerRequest: 0m,
            DailyProjection: 0m,
            MonthlyProjection: 0m,
            Warnings: null,
            CreatedByUserId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        capturedScenario!.Warnings.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

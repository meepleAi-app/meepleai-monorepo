using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.Queries;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Handlers;

/// <summary>
/// Tests for GenerateToolkitFromKbHandler.
/// NOTE: Vector store (Qdrant) has been removed — handler now always returns default suggestion.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class GenerateToolkitFromKbHandlerTests
{
    private readonly Mock<ILogger<GenerateToolkitFromKbHandler>> _loggerMock;
    private readonly GenerateToolkitFromKbHandler _handler;

    public GenerateToolkitFromKbHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GenerateToolkitFromKbHandler>>();

        _handler = new GenerateToolkitFromKbHandler(
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_ReturnsDefaultSuggestion()
    {
        // Arrange
        var command = new GenerateToolkitFromKbCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — handler returns default suggestion since vector store is removed
        Assert.NotNull(result);
        Assert.Equal("Game Toolkit", result.ToolkitName);
        Assert.Single(result.DiceTools);
        Assert.Equal(DiceType.D6, result.DiceTools[0].DiceType);
        Assert.Equal(2, result.DiceTools[0].Quantity);
        Assert.Empty(result.CounterTools);
        Assert.Empty(result.TimerTools);
        Assert.NotNull(result.ScoringTemplate);
        Assert.Equal(ScoreType.Points, result.ScoringTemplate!.ScoreType);
        Assert.NotNull(result.TurnTemplate);
        Assert.Equal(TurnOrderType.RoundRobin, result.TurnTemplate!.TurnOrderType);
        Assert.Contains("not available", result.Reasoning);
    }
}

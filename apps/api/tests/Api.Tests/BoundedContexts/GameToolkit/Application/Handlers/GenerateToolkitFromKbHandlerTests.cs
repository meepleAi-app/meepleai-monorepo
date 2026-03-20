using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.Queries;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

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
        result.Should().NotBeNull();
        result.ToolkitName.Should().Be("Game Toolkit");
        result.DiceTools.Should().ContainSingle();
        result.DiceTools[0].DiceType.Should().Be(DiceType.D6);
        result.DiceTools[0].Quantity.Should().Be(2);
        result.CounterTools.Should().BeEmpty();
        result.TimerTools.Should().BeEmpty();
        result.ScoringTemplate.Should().NotBeNull();
        result.ScoringTemplate!.ScoreType.Should().Be(ScoreType.Points);
        result.TurnTemplate.Should().NotBeNull();
        result.TurnTemplate!.TurnOrderType.Should().Be(TurnOrderType.RoundRobin);
        result.Reasoning.Should().Contain("not available");
    }
}

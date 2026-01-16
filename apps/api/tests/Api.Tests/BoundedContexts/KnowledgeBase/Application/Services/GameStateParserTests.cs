using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for GameStateParser.
/// Issue #2473: Production AI Implementation for Player Mode Move Suggestions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GameStateParserTests
{
    private readonly GameStateParser _parser;
    private readonly Mock<ILogger<GameStateParser>> _mockLogger;

    public GameStateParserTests()
    {
        _mockLogger = new Mock<ILogger<GameStateParser>>();
        _parser = new GameStateParser(_mockLogger.Object);
    }

    [Fact]
    public void Parse_WithValidGameState_ShouldReturnParsedState()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["players"] = JsonSerializer.SerializeToElement(new[]
            {
                new { name = "Player1", resources = new { wood = 2, brick = 1 }, score = 3 }
            }),
            ["resources"] = new Dictionary<string, object> { ["wood"] = 5, ["brick"] = 3 },
            ["currentPhase"] = "main",
            ["currentTurn"] = 5
        };

        // Act
        var result = _parser.Parse(rawState);

        // Assert
        result.Should().NotBeNull();
        result!.Players.Should().HaveCount(1);
        result.Players[0].Name.Should().Be("Player1");
        result.Resources.Should().ContainKey("wood");
        result.CurrentPhase.Should().Be("main");
        result.CurrentTurn.Should().Be(5);
        result.CompletenessScore.Should().BeGreaterThanOrEqualTo(0.8);
    }

    [Fact]
    public void Parse_WithNoPlayers_ShouldReturnNull()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["resources"] = new Dictionary<string, object> { ["wood"] = 5 }
        };

        // Act
        var result = _parser.Parse(rawState);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_WithMinimalGameState_ShouldCalculateLowCompleteness()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["players"] = JsonSerializer.SerializeToElement(new[]
            {
                new { name = "Player1" }
            })
        };

        // Act
        var result = _parser.Parse(rawState);

        // Assert
        result.Should().NotBeNull();
        result!.CompletenessScore.Should().Be(0.4); // Only players present
    }

    [Fact]
    public void Parse_WithCompleteGameState_ShouldCalculateHighCompleteness()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["players"] = JsonSerializer.SerializeToElement(new[]
            {
                new { name = "Player1", score = 5 }
            }),
            ["resources"] = new Dictionary<string, object> { ["wood"] = 3 },
            ["board"] = new Dictionary<string, object> { ["description"] = "Standard board" },
            ["currentPhase"] = "main",
            ["currentTurn"] = 8
        };

        // Act
        var result = _parser.Parse(rawState);

        // Assert
        result.Should().NotBeNull();
        result!.CompletenessScore.Should().Be(1.0); // All components present
    }

    [Fact]
    public void Parse_WithFallbackPlayerKeys_ShouldParseSuccessfully()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["player1"] = new Dictionary<string, object> { ["name"] = "Alice", ["score"] = 10 },
            ["player2"] = new Dictionary<string, object> { ["name"] = "Bob", ["score"] = 8 }
        };

        // Act
        var result = _parser.Parse(rawState);

        // Assert
        result.Should().NotBeNull();
        result!.Players.Should().HaveCount(2);
        result.Players[0].Name.Should().Be("Alice");
        result.Players[1].Name.Should().Be("Bob");
    }

    [Fact]
    public void Parse_WithBoardState_ShouldParseBoardCorrectly()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["players"] = JsonSerializer.SerializeToElement(new[] { new { name = "Player1" } }),
            ["board"] = new Dictionary<string, object>
            {
                ["description"] = "Hexagonal board",
                ["tiles"] = new Dictionary<string, object> { ["hex1"] = "forest", ["hex2"] = "mountain" }
            }
        };

        // Act
        var result = _parser.Parse(rawState);

        // Assert
        result.Should().NotBeNull();
        result!.Board.Should().NotBeNull();
        result.Board!.Description.Should().Be("Hexagonal board");
        result.Board.Tiles.Should().ContainKey("hex1");
    }

    [Fact]
    public void Parse_WithInvalidData_ShouldReturnNullAndLog()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["invalid_key"] = "invalid_value"
        };

        // Act
        var result = _parser.Parse(rawState);

        // Assert
        result.Should().BeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No players found")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ToSummary_ShouldGenerateReadableString()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["players"] = JsonSerializer.SerializeToElement(new[] { new { name = "Player1" } }),
            ["resources"] = new Dictionary<string, object> { ["wood"] = 3, ["brick"] = 2 },
            ["currentPhase"] = "setup",
            ["currentTurn"] = 1
        };

        var parsed = _parser.Parse(rawState);

        // Act
        var summary = parsed!.ToSummary();

        // Assert
        summary.Should().Contain("Players: 1");
        summary.Should().Contain("wood=3");
        summary.Should().Contain("brick=2");
        summary.Should().Contain("Phase: setup");
        summary.Should().Contain("Turn: 1");
    }

    [Fact]
    public void Parse_WithCapitalizedPlayerKey_ShouldParseSuccessfully()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Players"] = JsonSerializer.SerializeToElement(new[]
            {
                new { name = "Alice", score = 10 }
            })
        };

        // Act
        var result = _parser.Parse(rawState);

        // Assert
        result.Should().NotBeNull();
        result!.Players.Should().HaveCount(1);
        result.Players[0].Name.Should().Be("Alice");
    }

    [Fact]
    public void Parse_WithCapitalizedResourcesKey_ShouldParseSuccessfully()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["players"] = JsonSerializer.SerializeToElement(new[] { new { name = "Player1" } }),
            ["Resources"] = new Dictionary<string, object> { ["gold"] = 5 }
        };

        // Act
        var result = _parser.Parse(rawState);

        // Assert
        result.Should().NotBeNull();
        result!.Resources.Should().ContainKey("gold");
        result.Resources["gold"].Should().Be(5);
    }

    [Fact]
    public void Parse_WithPlayerHavingNullResources_ShouldHandleGracefully()
    {
        // Arrange
        var rawState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["player1"] = new Dictionary<string, object>
            {
                ["name"] = "Alice",
                ["score"] = 8
                // No resources key
            }
        };

        // Act
        var result = _parser.Parse(rawState);

        // Assert
        result.Should().NotBeNull();
        result!.Players.Should().HaveCount(1);
        result.Players[0].Resources.Should().BeNull();
    }

    [Fact]
    public void Parse_WithNullArgument_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => _parser.Parse(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

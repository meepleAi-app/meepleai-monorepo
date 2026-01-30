using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GameStateTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsGameState()
    {
        // Arrange
        var activePlayer = Guid.NewGuid();
        var playerScores = new Dictionary<Guid, int> { { activePlayer, 50 } };

        // Act
        var state = GameState.Create(
            currentTurn: 3,
            activePlayer: activePlayer,
            playerScores: playerScores,
            gamePhase: "midgame",
            lastAction: "placed worker on farm");

        // Assert
        state.Should().NotBeNull();
        state.CurrentTurn.Should().Be(3);
        state.ActivePlayer.Should().Be(activePlayer);
        state.PlayerScores.Should().HaveCount(1);
        state.GamePhase.Should().Be("midgame");
        state.LastAction.Should().Be("placed worker on farm");
    }

    [Fact]
    public void Create_WithNegativeTurn_ThrowsArgumentException()
    {
        // Act
        var act = () => GameState.Create(
            currentTurn: -1,
            activePlayer: Guid.NewGuid(),
            playerScores: new Dictionary<Guid, int>(),
            gamePhase: "setup",
            lastAction: "started");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*CurrentTurn*");
    }

    [Fact]
    public void Create_WithEmptyActivePlayer_ThrowsArgumentException()
    {
        // Act
        var act = () => GameState.Create(
            currentTurn: 1,
            activePlayer: Guid.Empty,
            playerScores: new Dictionary<Guid, int>(),
            gamePhase: "setup",
            lastAction: "started");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ActivePlayer*");
    }

    [Fact]
    public void Create_WithEmptyGamePhase_ThrowsArgumentException()
    {
        // Act
        var act = () => GameState.Create(
            currentTurn: 1,
            activePlayer: Guid.NewGuid(),
            playerScores: new Dictionary<Guid, int>(),
            gamePhase: "",
            lastAction: "started");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*GamePhase*");
    }

    [Fact]
    public void Create_WithTooLongGamePhase_ThrowsArgumentException()
    {
        // Arrange
        var longPhase = new string('x', 51);

        // Act
        var act = () => GameState.Create(
            currentTurn: 1,
            activePlayer: Guid.NewGuid(),
            playerScores: new Dictionary<Guid, int>(),
            gamePhase: longPhase,
            lastAction: "started");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*GamePhase*exceed*");
    }

    [Fact]
    public void Create_WithTooLongLastAction_ThrowsArgumentException()
    {
        // Arrange
        var longAction = new string('x', 201);

        // Act
        var act = () => GameState.Create(
            currentTurn: 1,
            activePlayer: Guid.NewGuid(),
            playerScores: new Dictionary<Guid, int>(),
            gamePhase: "setup",
            lastAction: longAction);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*LastAction*exceed*");
    }

    [Fact]
    public void Initial_CreatesValidInitialState()
    {
        // Arrange
        var activePlayer = Guid.NewGuid();

        // Act
        var state = GameState.Initial(activePlayer);

        // Assert
        state.CurrentTurn.Should().Be(1);
        state.ActivePlayer.Should().Be(activePlayer);
        state.PlayerScores.Should().BeEmpty();
        state.GamePhase.Should().Be("setup");
        state.LastAction.Should().Be("session started");
    }

    [Fact]
    public void ToJson_SerializesCorrectly()
    {
        // Arrange
        var activePlayer = Guid.NewGuid();
        var state = GameState.Create(
            currentTurn: 2,
            activePlayer: activePlayer,
            playerScores: new Dictionary<Guid, int> { { activePlayer, 30 } },
            gamePhase: "early",
            lastAction: "rolled dice");

        // Act
        var json = state.ToJson();

        // Assert
        json.Should().Contain("\"CurrentTurn\":2");
        json.Should().Contain("\"GamePhase\":\"early\"");
        json.Should().Contain("\"LastAction\":\"rolled dice\"");
    }

    [Fact]
    public void FromJson_WithValidJson_DeserializesCorrectly()
    {
        // Arrange
        var activePlayer = Guid.NewGuid();
        var json = $@"{{
            ""CurrentTurn"": 5,
            ""ActivePlayer"": ""{activePlayer}"",
            ""PlayerScores"": {{""{activePlayer}"": 100}},
            ""GamePhase"": ""endgame"",
            ""LastAction"": ""final scoring""
        }}";

        // Act
        var state = GameState.FromJson(json);

        // Assert
        state.CurrentTurn.Should().Be(5);
        state.ActivePlayer.Should().Be(activePlayer);
        state.PlayerScores.Should().HaveCount(1);
        state.GamePhase.Should().Be("endgame");
        state.LastAction.Should().Be("final scoring");
    }

    [Fact]
    public void FromJson_WithInvalidJson_ThrowsArgumentException()
    {
        // Act
        var act = () => GameState.FromJson("{ invalid json }");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid GameState JSON*");
    }

    [Fact]
    public void FromJson_WithEmptyString_ThrowsArgumentException()
    {
        // Act
        var act = () => GameState.FromJson("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*JSON cannot be empty*");
    }

    [Fact]
    public void RoundTrip_Serialization_PreservesData()
    {
        // Arrange
        var activePlayer = Guid.NewGuid();
        var original = GameState.Create(
            currentTurn: 7,
            activePlayer: activePlayer,
            playerScores: new Dictionary<Guid, int>
            {
                { activePlayer, 150 },
                { Guid.NewGuid(), 120 }
            },
            gamePhase: "final round",
            lastAction: "triggered end condition");

        // Act
        var json = original.ToJson();
        var deserialized = GameState.FromJson(json);

        // Assert
        deserialized.CurrentTurn.Should().Be(original.CurrentTurn);
        deserialized.ActivePlayer.Should().Be(original.ActivePlayer);
        deserialized.PlayerScores.Should().HaveCount(original.PlayerScores.Count);
        foreach (var kvp in original.PlayerScores)
        {
            deserialized.PlayerScores.Should().ContainKey(kvp.Key);
            deserialized.PlayerScores[kvp.Key].Should().Be(kvp.Value);
        }
        deserialized.GamePhase.Should().Be(original.GamePhase);
        deserialized.LastAction.Should().Be(original.LastAction);
    }
}

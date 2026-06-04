using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Scoring;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Scoring;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class ObjectivesScoringStrategyTests
{
    private readonly ObjectivesScoringStrategy _sut = new();

    [Fact]
    public void Type_IsObjectives()
    {
        _sut.Type.Should().Be(ScoreType.Objectives);
    }

    [Fact]
    public void Validate_ValidJson_ReturnsValid()
    {
        var json = """
            {
              "completedByPlayer": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "objectives": ["Find clue A", "Solve riddle B"] },
                { "playerId": "00000000-0000-0000-0000-000000000002", "objectives": ["Find clue A"] },
                { "playerId": "00000000-0000-0000-0000-000000000003", "objectives": ["Find clue A", "Solve riddle B", "Confront suspect"] }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyObjectivesList_ReturnsValid()
    {
        // A player who has not completed any objective yet is semantically OK.
        var json = """
            {
              "completedByPlayer": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "objectives": [] },
                { "playerId": "00000000-0000-0000-0000-000000000002", "objectives": ["Goal 1"] }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NullObjectivesList_ReturnsInvalid()
    {
        var json = """
            {
              "completedByPlayer": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "objectives": null }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Objectives list cannot be null"));
    }

    [Fact]
    public void Validate_EmptyObjectiveName_ReturnsInvalid()
    {
        var json = """
            {
              "completedByPlayer": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "objectives": ["valid", "   "] }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Objective names cannot be empty"));
    }

    [Fact]
    public void Validate_DuplicatePlayer_ReturnsInvalid()
    {
        var json = """
            {
              "completedByPlayer": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "objectives": ["A"] },
                { "playerId": "00000000-0000-0000-0000-000000000001", "objectives": ["B"] }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Duplicate playerId"));
    }

    [Fact]
    public void Validate_EmptyPlayerId_ReturnsInvalid()
    {
        var json = """
            {
              "completedByPlayer": [
                { "playerId": "00000000-0000-0000-0000-000000000000", "objectives": ["A"] }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("PlayerId cannot be empty"));
    }

    [Fact]
    public void Validate_EmptyJson_ReturnsInvalid()
    {
        var result = _sut.Validate("");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhitespaceJson_ReturnsInvalid()
    {
        var result = _sut.Validate("   ");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyCompletedByPlayerArray_ReturnsInvalid()
    {
        var json = """{"completedByPlayer":[]}""";

        var result = _sut.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No players"));
    }

    [Fact]
    public void Validate_MalformedJson_ReturnsInvalid()
    {
        var result = _sut.Validate("not json");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid JSON"));
    }

    [Fact]
    public void ComputeWinnerPlayerId_ReturnsPlayerWithMostObjectives()
    {
        var winner = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var p1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var p2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var json = $$"""
            {
              "completedByPlayer": [
                { "playerId": "{{p1}}", "objectives": ["A"] },
                { "playerId": "{{p2}}", "objectives": ["A", "B"] },
                { "playerId": "{{winner}}", "objectives": ["A", "B", "C"] }
              ]
            }
            """;

        var result = _sut.ComputeWinnerPlayerId(json);

        result.Should().Be(winner);
    }

    [Fact]
    public void ComputeWinnerPlayerId_OnTie_ReturnsNull()
    {
        var json = """
            {
              "completedByPlayer": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "objectives": ["A", "B"] },
                { "playerId": "00000000-0000-0000-0000-000000000002", "objectives": ["X", "Y"] }
              ]
            }
            """;

        var result = _sut.ComputeWinnerPlayerId(json);

        result.Should().BeNull();
    }

    [Fact]
    public void ComputeWinnerPlayerId_SinglePlayer_ReturnsThatPlayer()
    {
        var only = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var json = $$"""
            {
              "completedByPlayer": [
                { "playerId": "{{only}}", "objectives": [] }
              ]
            }
            """;

        var result = _sut.ComputeWinnerPlayerId(json);

        result.Should().Be(only);
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var original = new ObjectivesScoreData(new[]
        {
            new PlayerObjectives(p1, new[] { "Find map", "Defeat boss" }),
            new PlayerObjectives(p2, new[] { "Find map" }),
        });

        var json = _sut.Serialize(original);
        var roundtrip = (ObjectivesScoreData)_sut.Deserialize(json);

        roundtrip.CompletedByPlayer.Should().HaveCount(2);
        roundtrip.CompletedByPlayer[0].PlayerId.Should().Be(p1);
        roundtrip.CompletedByPlayer[0].Objectives.Should().BeEquivalentTo(new[] { "Find map", "Defeat boss" });
        roundtrip.CompletedByPlayer[1].PlayerId.Should().Be(p2);
        roundtrip.CompletedByPlayer[1].Objectives.Should().BeEquivalentTo(new[] { "Find map" });
    }

    [Fact]
    public void Serialize_WrongType_Throws()
    {
        var act = () => _sut.Serialize(new { foo = "bar" });
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Expected ObjectivesScoreData*");
    }
}

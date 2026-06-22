using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Scoring;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Scoring;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class RankingScoringStrategyTests
{
    private readonly RankingScoringStrategy _sut = new();

    [Fact]
    public void Type_IsRanking()
    {
        _sut.Type.Should().Be(ScoreType.Ranking);
    }

    [Fact]
    public void Validate_ValidJson_ReturnsValid()
    {
        var json = """
            {
              "positions": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "position": 2 },
                { "playerId": "00000000-0000-0000-0000-000000000002", "position": 1 },
                { "playerId": "00000000-0000-0000-0000-000000000003", "position": 3 }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DuplicatePosition_ReturnsInvalid()
    {
        var json = """
            {
              "positions": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "position": 1 },
                { "playerId": "00000000-0000-0000-0000-000000000002", "position": 1 }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("sequential"));
    }

    [Fact]
    public void Validate_GapInPositions_ReturnsInvalid()
    {
        // 3 players but positions 1, 3, 5 (gaps + no sequential up to N=3)
        var json = """
            {
              "positions": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "position": 1 },
                { "playerId": "00000000-0000-0000-0000-000000000002", "position": 3 },
                { "playerId": "00000000-0000-0000-0000-000000000003", "position": 5 }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("sequential"));
    }

    [Fact]
    public void Validate_ZeroPosition_ReturnsInvalid()
    {
        var json = """
            {
              "positions": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "position": 0 },
                { "playerId": "00000000-0000-0000-0000-000000000002", "position": 1 }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Position must be >= 1"));
    }

    [Fact]
    public void Validate_NegativePosition_ReturnsInvalid()
    {
        var json = """
            {
              "positions": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "position": -3 },
                { "playerId": "00000000-0000-0000-0000-000000000002", "position": 1 }
              ]
            }
            """;

        var result = _sut.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Position must be >= 1"));
    }

    [Fact]
    public void Validate_DuplicatePlayer_ReturnsInvalid()
    {
        var json = """
            {
              "positions": [
                { "playerId": "00000000-0000-0000-0000-000000000001", "position": 1 },
                { "playerId": "00000000-0000-0000-0000-000000000001", "position": 2 }
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
              "positions": [
                { "playerId": "00000000-0000-0000-0000-000000000000", "position": 1 }
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
    public void Validate_EmptyPositionsArray_ReturnsInvalid()
    {
        var json = """{"positions":[]}""";

        var result = _sut.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No positions"));
    }

    [Fact]
    public void Validate_MalformedJson_ReturnsInvalid()
    {
        var result = _sut.Validate("not json");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid JSON"));
    }

    [Fact]
    public void ComputeWinnerPlayerId_ReturnsPosition1Player()
    {
        var winner = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var third = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var json = $$"""
            {
              "positions": [
                { "playerId": "{{second}}", "position": 2 },
                { "playerId": "{{winner}}", "position": 1 },
                { "playerId": "{{third}}", "position": 3 }
              ]
            }
            """;

        var result = _sut.ComputeWinnerPlayerId(json);

        result.Should().Be(winner);
    }

    [Fact]
    public void ComputeWinnerPlayerId_SinglePlayer_ReturnsThatPlayer()
    {
        var only = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var json = $$"""
            {
              "positions": [
                { "playerId": "{{only}}", "position": 1 }
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
        var original = new RankingScoreData(new[]
        {
            new PlayerRanking(p1, 1),
            new PlayerRanking(p2, 2),
        });

        var json = _sut.Serialize(original);
        var roundtrip = (RankingScoreData)_sut.Deserialize(json);

        roundtrip.Positions.Should().HaveCount(2);
        roundtrip.Positions[0].PlayerId.Should().Be(p1);
        roundtrip.Positions[0].Position.Should().Be(1);
        roundtrip.Positions[1].PlayerId.Should().Be(p2);
        roundtrip.Positions[1].Position.Should().Be(2);
    }

    [Fact]
    public void Serialize_WrongType_Throws()
    {
        var act = () => _sut.Serialize(new { foo = "bar" });
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Expected RankingScoreData*");
    }
}

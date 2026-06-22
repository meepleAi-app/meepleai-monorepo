using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Scoring;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Scoring;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class PointsScoringStrategyTests
{
    private readonly PointsScoringStrategy _sut = new();

    [Fact]
    public void Type_IsPoints()
    {
        _sut.Type.Should().Be(ScoreType.Points);
    }

    [Fact]
    public void Validate_ValidJson_ReturnsValid()
    {
        var json = """{"scores":[{"playerId":"00000000-0000-0000-0000-000000000001","points":42}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NegativeScore_ReturnsInvalid()
    {
        var json = """{"scores":[{"playerId":"00000000-0000-0000-0000-000000000001","points":-5}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("non-negative"));
    }

    [Fact]
    public void Validate_DuplicatePlayer_ReturnsInvalid()
    {
        var json = """{"scores":[{"playerId":"00000000-0000-0000-0000-000000000001","points":10},{"playerId":"00000000-0000-0000-0000-000000000001","points":20}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Validate_EmptyPlayerId_ReturnsInvalid()
    {
        var json = """{"scores":[{"playerId":"00000000-0000-0000-0000-000000000000","points":10}]}""";
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
    public void Validate_EmptyScoresArray_ReturnsInvalid()
    {
        var json = """{"scores":[]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No scores"));
    }

    [Fact]
    public void Validate_MalformedJson_ReturnsInvalid()
    {
        var result = _sut.Validate("not json");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid JSON"));
    }

    [Fact]
    public void ComputeWinnerPlayerId_ReturnsHighestScorePlayer()
    {
        var winner = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var loser = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var json = $$"""{"scores":[{"playerId":"{{loser}}","points":10},{"playerId":"{{winner}}","points":50}]}""";

        var result = _sut.ComputeWinnerPlayerId(json);

        result.Should().Be(winner);
    }

    [Fact]
    public void ComputeWinnerPlayerId_OnTie_ReturnsNull()
    {
        var json = """{"scores":[{"playerId":"00000000-0000-0000-0000-000000000001","points":10},{"playerId":"00000000-0000-0000-0000-000000000002","points":10}]}""";

        var result = _sut.ComputeWinnerPlayerId(json);

        result.Should().BeNull();
    }

    [Fact]
    public void ComputeWinnerPlayerId_SinglePlayer_ReturnsThatPlayer()
    {
        var only = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var json = $$"""{"scores":[{"playerId":"{{only}}","points":0}]}""";

        var result = _sut.ComputeWinnerPlayerId(json);

        result.Should().Be(only);
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var original = new PointsScoreData(new[]
        {
            new PlayerScore(p1, 42),
            new PlayerScore(p2, 30),
        });

        var json = _sut.Serialize(original);
        var roundtrip = (PointsScoreData)_sut.Deserialize(json);

        roundtrip.Scores.Should().HaveCount(2);
        roundtrip.Scores[0].PlayerId.Should().Be(p1);
        roundtrip.Scores[0].Points.Should().Be(42);
        roundtrip.Scores[1].PlayerId.Should().Be(p2);
        roundtrip.Scores[1].Points.Should().Be(30);
    }

    [Fact]
    public void Serialize_WrongType_Throws()
    {
        var act = () => _sut.Serialize(new { foo = "bar" });
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Expected PointsScoreData*");
    }
}

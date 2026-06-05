using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Scoring;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Scoring;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class BinaryWinScoringStrategyTests
{
    private readonly BinaryWinScoringStrategy _sut = new();

    [Fact]
    public void Type_IsBinaryWin()
    {
        _sut.Type.Should().Be(ScoreType.BinaryWin);
    }

    [Fact]
    public void Validate_SingleWinner_ReturnsValid()
    {
        var json = """{"results":[{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":true},{"playerId":"00000000-0000-0000-0000-000000000002","isWinner":false}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MultipleWinners_ReturnsValid()
    {
        // Cooperative all-win is a legitimate outcome
        var json = """{"results":[{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":true},{"playerId":"00000000-0000-0000-0000-000000000002","isWinner":true}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AllLose_ReturnsValid()
    {
        // Cooperative all-lose is a legitimate outcome
        var json = """{"results":[{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":false},{"playerId":"00000000-0000-0000-0000-000000000002","isWinner":false}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyResults_ReturnsInvalid()
    {
        var json = """{"results":[]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No results"));
    }

    [Fact]
    public void Validate_EmptyJson_ReturnsInvalid()
    {
        var result = _sut.Validate("");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_MalformedJson_ReturnsInvalid()
    {
        var result = _sut.Validate("not json");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid JSON"));
    }

    [Fact]
    public void Validate_EmptyPlayerId_ReturnsInvalid()
    {
        var json = """{"results":[{"playerId":"00000000-0000-0000-0000-000000000000","isWinner":true}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("PlayerId cannot be empty"));
    }

    [Fact]
    public void Validate_DuplicatePlayer_ReturnsInvalid()
    {
        var json = """{"results":[{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":true},{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":false}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Duplicate"));
    }

    [Fact]
    public void ComputeWinnerPlayerId_SingleWinner_ReturnsThatPlayer()
    {
        var winner = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var loser = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var json = $$"""{"results":[{"playerId":"{{winner}}","isWinner":true},{"playerId":"{{loser}}","isWinner":false}]}""";

        var result = _sut.ComputeWinnerPlayerId(json);

        result.Should().Be(winner);
    }

    [Fact]
    public void ComputeWinnerPlayerId_AllLose_ReturnsNull()
    {
        var json = """{"results":[{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":false},{"playerId":"00000000-0000-0000-0000-000000000002","isWinner":false}]}""";

        var result = _sut.ComputeWinnerPlayerId(json);

        result.Should().BeNull();
    }

    [Fact]
    public void ComputeWinnerPlayerId_AllWin_ReturnsNull()
    {
        var json = """{"results":[{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":true},{"playerId":"00000000-0000-0000-0000-000000000002","isWinner":true}]}""";

        var result = _sut.ComputeWinnerPlayerId(json);

        result.Should().BeNull();
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var original = new BinaryWinScoreData(new[]
        {
            new BinaryPlayerResult(p1, true),
            new BinaryPlayerResult(p2, false),
        });

        var json = _sut.Serialize(original);
        var roundtrip = (BinaryWinScoreData)_sut.Deserialize(json);

        roundtrip.Results.Should().HaveCount(2);
        roundtrip.Results[0].PlayerId.Should().Be(p1);
        roundtrip.Results[0].IsWinner.Should().BeTrue();
        roundtrip.Results[1].PlayerId.Should().Be(p2);
        roundtrip.Results[1].IsWinner.Should().BeFalse();
    }

    [Fact]
    public void Serialize_WrongType_Throws()
    {
        var act = () => _sut.Serialize(new { foo = "bar" });
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Expected BinaryWinScoreData*");
    }
}

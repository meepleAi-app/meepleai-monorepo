using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Scoring;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Scoring;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class ScoringStrategyFactoryTests
{
    private readonly ScoringStrategyFactory _sut = new();

    [Theory]
    [InlineData(ScoreType.Points, typeof(PointsScoringStrategy))]
    [InlineData(ScoreType.BinaryWin, typeof(BinaryWinScoringStrategy))]
    [InlineData(ScoreType.Objectives, typeof(ObjectivesScoringStrategy))]
    [InlineData(ScoreType.Ranking, typeof(RankingScoringStrategy))]
    public void GetStrategy_ReturnsExpectedStrategyForEachScoreType(
        ScoreType type, Type expectedStrategyType)
    {
        var strategy = _sut.GetStrategy(type);
        strategy.Should().BeOfType(expectedStrategyType);
        strategy.Type.Should().Be(type);
    }

    [Fact]
    public void GetStrategy_OnUnknownType_Throws()
    {
        var act = () => _sut.GetStrategy((ScoreType)999);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Unknown ScoreType*");
    }

    [Fact]
    public void ScoringValidationResult_Valid_HasEmptyErrors()
    {
        var result = ScoringValidationResult.Valid();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ScoringValidationResult_Failed_SingleError_ContainsError()
    {
        var result = ScoringValidationResult.Failed("test error");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("test error");
    }

    [Fact]
    public void ScoringValidationResult_Failed_MultipleErrors_ContainsAll()
    {
        var result = ScoringValidationResult.Failed(new[] { "err1", "err2" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2).And.ContainInOrder("err1", "err2");
    }
}

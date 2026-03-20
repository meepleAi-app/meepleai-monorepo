using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class RoundScoreTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesSuccessfully()
    {
        var score = new RoundScore(Guid.NewGuid(), 1, "points", 10, DateTime.UtcNow, "pts");
        score.Round.Should().Be(1);
        score.Dimension.Should().Be("points");
        score.Value.Should().Be(10);
        score.Unit.Should().Be("pts");
    }

    [Fact]
    public void Constructor_EmptyPlayerId_ThrowsValidationException()
    {
        var act = () =>
            new RoundScore(Guid.Empty, 1, "points", 10, DateTime.UtcNow);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_ZeroRound_ThrowsValidationException()
    {
        var act = () =>
            new RoundScore(Guid.NewGuid(), 0, "points", 10, DateTime.UtcNow);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_EmptyDimension_ThrowsValidationException()
    {
        var act = () =>
            new RoundScore(Guid.NewGuid(), 1, "", 10, DateTime.UtcNow);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_DimensionTooLong_ThrowsValidationException()
    {
        var act = () =>
            new RoundScore(Guid.NewGuid(), 1, new string('a', 51), 10, DateTime.UtcNow);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_UnitTooLong_ThrowsValidationException()
    {
        var act = () =>
            new RoundScore(Guid.NewGuid(), 1, "points", 10, DateTime.UtcNow, new string('u', 21));
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_TrimsDimensionAndUnit()
    {
        var score = new RoundScore(Guid.NewGuid(), 1, "  points  ", 10, DateTime.UtcNow, "  pts  ");
        score.Dimension.Should().Be("points");
        score.Unit.Should().Be("pts");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var playerId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var s1 = new RoundScore(playerId, 1, "points", 10, now);
        var s2 = new RoundScore(playerId, 1, "points", 10, now);
        s2.Should().Be(s1);
    }

    [Fact]
    public void ToString_WithUnit_FormatsCorrectly()
    {
        var score = new RoundScore(Guid.NewGuid(), 1, "points", 10, DateTime.UtcNow, "pts");
        score.ToString().Should().Contain("10 pts");
    }

    [Fact]
    public void ToString_WithoutUnit_FormatsCorrectly()
    {
        var score = new RoundScore(Guid.NewGuid(), 1, "points", 10, DateTime.UtcNow);
        var str = score.ToString();
        str.Should().Contain("10");
        str.Should().NotContain("pts");
    }
}

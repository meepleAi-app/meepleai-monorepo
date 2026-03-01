using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class RoundScoreTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesSuccessfully()
    {
        var score = new RoundScore(Guid.NewGuid(), 1, "points", 10, DateTime.UtcNow, "pts");
        Assert.Equal(1, score.Round);
        Assert.Equal("points", score.Dimension);
        Assert.Equal(10, score.Value);
        Assert.Equal("pts", score.Unit);
    }

    [Fact]
    public void Constructor_EmptyPlayerId_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new RoundScore(Guid.Empty, 1, "points", 10, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_ZeroRound_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new RoundScore(Guid.NewGuid(), 0, "points", 10, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_EmptyDimension_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new RoundScore(Guid.NewGuid(), 1, "", 10, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_DimensionTooLong_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new RoundScore(Guid.NewGuid(), 1, new string('a', 51), 10, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_UnitTooLong_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new RoundScore(Guid.NewGuid(), 1, "points", 10, DateTime.UtcNow, new string('u', 21)));
    }

    [Fact]
    public void Constructor_TrimsDimensionAndUnit()
    {
        var score = new RoundScore(Guid.NewGuid(), 1, "  points  ", 10, DateTime.UtcNow, "  pts  ");
        Assert.Equal("points", score.Dimension);
        Assert.Equal("pts", score.Unit);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var playerId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var s1 = new RoundScore(playerId, 1, "points", 10, now);
        var s2 = new RoundScore(playerId, 1, "points", 10, now);
        Assert.Equal(s1, s2);
    }

    [Fact]
    public void ToString_WithUnit_FormatsCorrectly()
    {
        var score = new RoundScore(Guid.NewGuid(), 1, "points", 10, DateTime.UtcNow, "pts");
        Assert.Contains("10 pts", score.ToString());
    }

    [Fact]
    public void ToString_WithoutUnit_FormatsCorrectly()
    {
        var score = new RoundScore(Guid.NewGuid(), 1, "points", 10, DateTime.UtcNow);
        var str = score.ToString();
        Assert.Contains("10", str);
        Assert.DoesNotContain("pts", str);
    }
}

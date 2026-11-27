using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Validators;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class FinalizeSessionCommandValidatorTests
{
    private readonly FinalizeSessionCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidConsecutiveRanks_ShouldPass()
    {
        // Arrange
        var ranks = new Dictionary<Guid, int>
        {
            { Guid.NewGuid(), 1 },
            { Guid.NewGuid(), 2 },
            { Guid.NewGuid(), 3 }
        };

        var command = new FinalizeSessionCommand(
            SessionId: Guid.NewGuid(),
            FinalRanks: ranks,
            RequestedBy: Guid.NewGuid()
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithGapsInRanks_ShouldFail()
    {
        // Arrange
        var ranks = new Dictionary<Guid, int>
        {
            { Guid.NewGuid(), 1 },
            { Guid.NewGuid(), 3 }, // gap - missing 2
            { Guid.NewGuid(), 4 }
        };

        var command = new FinalizeSessionCommand(
            SessionId: Guid.NewGuid(),
            FinalRanks: ranks,
            RequestedBy: Guid.NewGuid()
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.FinalRanks));
    }

    [Fact]
    public void Validate_WithRanksNotStartingAtOne_ShouldFail()
    {
        // Arrange
        var ranks = new Dictionary<Guid, int>
        {
            { Guid.NewGuid(), 0 }, // should start at 1
            { Guid.NewGuid(), 1 },
            { Guid.NewGuid(), 2 }
        };

        var command = new FinalizeSessionCommand(
            SessionId: Guid.NewGuid(),
            FinalRanks: ranks,
            RequestedBy: Guid.NewGuid()
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.FinalRanks));
    }

    [Fact]
    public void Validate_WithEmptyRanks_ShouldFail()
    {
        // Arrange
        var command = new FinalizeSessionCommand(
            SessionId: Guid.NewGuid(),
            FinalRanks: new Dictionary<Guid, int>(),
            RequestedBy: Guid.NewGuid()
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.FinalRanks));
    }

    /// <summary>
    /// #4780 — the ranks of a live game are a competition ranking: players who tie share the rank, and the next rank
    /// skips the places they took. «Consecutive from 1 to N» refused every tie, so the live ranks could not be kept.
    /// </summary>
    [Theory]
    [Trait("Issue", "4780")]
    [InlineData(new[] { 1, 1, 3 })]
    [InlineData(new[] { 1, 2, 2 })]
    [InlineData(new[] { 1, 1, 1 })]
    [InlineData(new[] { 1, 2, 2, 4 })]
    public void Validate_ACompetitionRankingWithTies_Passes(int[] ranks)
    {
        _validator.Validate(CommandWith(ranks)).IsValid.Should().BeTrue();
    }

    /// <summary>A tie does not open the door to gaps: after two players tied at 1, the next is 3, never 2 or 4.</summary>
    [Theory]
    [Trait("Issue", "4780")]
    [InlineData(new[] { 1, 1, 2 })]
    [InlineData(new[] { 1, 1, 4 })]
    [InlineData(new[] { 2, 2 })]
    [InlineData(new[] { 1, 2, 2, 3 })]
    public void Validate_ARankingThatIsNotACompetitionRanking_Fails(int[] ranks)
    {
        var result = _validator.Validate(CommandWith(ranks));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(FinalizeSessionCommand.FinalRanks));
    }

    private static FinalizeSessionCommand CommandWith(int[] ranks) => new(
        SessionId: Guid.NewGuid(),
        FinalRanks: ranks.ToDictionary(_ => Guid.NewGuid(), rank => rank),
        RequestedBy: Guid.NewGuid());
}

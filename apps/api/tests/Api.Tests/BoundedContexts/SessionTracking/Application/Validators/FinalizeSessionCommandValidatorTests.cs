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
            FinalRanks: ranks
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
            FinalRanks: ranks
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
            FinalRanks: ranks
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
            FinalRanks: new Dictionary<Guid, int>()
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.FinalRanks));
    }
}

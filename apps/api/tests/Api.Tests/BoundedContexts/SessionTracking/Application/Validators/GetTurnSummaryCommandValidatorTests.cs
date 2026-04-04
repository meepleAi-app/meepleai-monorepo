using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Validators;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class GetTurnSummaryCommandValidatorTests
{
    private readonly GetTurnSummaryCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_WithLastNEvents_ShouldPass()
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.NewGuid(), LastNEvents: 20);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidCommand_WithPhaseRange_ShouldPass()
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.NewGuid(), FromPhase: 1, ToPhase: 5);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidCommand_WithFromPhaseOnly_ShouldPass()
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.NewGuid(), FromPhase: 3);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySessionId_ShouldFail()
    {
        var command = new GetTurnSummaryCommand(Guid.Empty, Guid.NewGuid(), LastNEvents: 10);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validate_EmptyRequesterId_ShouldFail()
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.Empty, LastNEvents: 10);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RequesterId);
    }

    [Fact]
    public void Validate_NoFilteringStrategy_ShouldFail()
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "At least one of LastNEvents, FromPhase, or ToPhase must be provided.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(200)]
    public void Validate_LastNEvents_OutOfRange_ShouldFail(int value)
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.NewGuid(), LastNEvents: value);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.LastNEvents);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_LastNEvents_InRange_ShouldPass(int value)
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.NewGuid(), LastNEvents: value);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.LastNEvents);
    }

    [Fact]
    public void Validate_NegativeFromPhase_ShouldFail()
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.NewGuid(), FromPhase: -1);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FromPhase);
    }

    [Fact]
    public void Validate_NegativeToPhase_ShouldFail()
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.NewGuid(), ToPhase: -1);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ToPhase);
    }

    [Fact]
    public void Validate_ToPhase_LessThan_FromPhase_ShouldFail()
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.NewGuid(), FromPhase: 5, ToPhase: 2);
        var result = _validator.TestValidate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "ToPhase must be greater than or equal to FromPhase.");
    }
}

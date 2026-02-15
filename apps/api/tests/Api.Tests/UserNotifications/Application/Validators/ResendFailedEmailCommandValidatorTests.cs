using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.UserNotifications.Application.Validators;

[Trait("Category", TestCategories.Unit)]
public sealed class ResendFailedEmailCommandValidatorTests
{
    private readonly ResendFailedEmailCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var command = new ResendFailedEmailCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyEmailId_Fails()
    {
        var command = new ResendFailedEmailCommand(Guid.Empty, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.EmailId);
    }

    [Fact]
    public void Validate_EmptyUserId_Fails()
    {
        var command = new ResendFailedEmailCommand(Guid.NewGuid(), Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validate_BothEmpty_FailsWithTwoErrors()
    {
        var command = new ResendFailedEmailCommand(Guid.Empty, Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.EmailId);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}

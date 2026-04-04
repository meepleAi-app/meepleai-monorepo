using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for TestAlertCommand.
/// Ensures alert type and channel are provided.
/// </summary>
internal sealed class TestAlertCommandValidator : AbstractValidator<TestAlertCommand>
{
    public TestAlertCommandValidator()
    {
        RuleFor(x => x.AlertType)
            .NotEmpty()
            .WithMessage("AlertType is required")
            .MaximumLength(100)
            .WithMessage("AlertType must not exceed 100 characters");

        RuleFor(x => x.Channel)
            .NotEmpty()
            .WithMessage("Channel is required")
            .MaximumLength(50)
            .WithMessage("Channel must not exceed 50 characters");
    }
}

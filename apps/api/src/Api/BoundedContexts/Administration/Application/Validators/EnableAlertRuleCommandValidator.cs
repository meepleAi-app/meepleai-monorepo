using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for EnableAlertRuleCommand.
/// Ensures alert rule ID and updater are valid.
/// </summary>
internal sealed class EnableAlertRuleCommandValidator : AbstractValidator<EnableAlertRuleCommand>
{
    public EnableAlertRuleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Alert rule ID is required");

        RuleFor(x => x.UpdatedBy)
            .NotEmpty()
            .WithMessage("UpdatedBy is required");
    }
}

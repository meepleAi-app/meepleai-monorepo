using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for DeleteAlertRuleCommand.
/// Ensures alert rule ID is valid.
/// </summary>
internal sealed class DeleteAlertRuleCommandValidator : AbstractValidator<DeleteAlertRuleCommand>
{
    public DeleteAlertRuleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Alert rule ID is required");
    }
}

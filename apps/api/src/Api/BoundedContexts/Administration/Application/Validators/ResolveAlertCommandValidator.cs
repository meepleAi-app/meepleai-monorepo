using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for ResolveAlertCommand.
/// Ensures alert type is provided.
/// </summary>
internal sealed class ResolveAlertCommandValidator : AbstractValidator<ResolveAlertCommand>
{
    public ResolveAlertCommandValidator()
    {
        RuleFor(x => x.AlertType)
            .NotEmpty()
            .WithMessage("AlertType is required")
            .MaximumLength(100)
            .WithMessage("AlertType must not exceed 100 characters");
    }
}

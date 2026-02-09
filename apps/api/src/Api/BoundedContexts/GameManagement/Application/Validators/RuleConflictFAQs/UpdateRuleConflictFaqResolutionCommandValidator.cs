using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.RuleConflictFAQs;

/// <summary>
/// Validator for UpdateRuleConflictFaqResolutionCommand.
/// Issue #3966: CQRS validation for FAQ resolution updates.
/// </summary>
internal sealed class UpdateRuleConflictFaqResolutionCommandValidator : AbstractValidator<UpdateRuleConflictFaqResolutionCommand>
{
    public UpdateRuleConflictFaqResolutionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("FAQ ID is required");

        RuleFor(x => x.Resolution)
            .NotEmpty()
            .WithMessage("Resolution is required")
            .MaximumLength(2000)
            .WithMessage("Resolution cannot exceed 2000 characters");
    }
}

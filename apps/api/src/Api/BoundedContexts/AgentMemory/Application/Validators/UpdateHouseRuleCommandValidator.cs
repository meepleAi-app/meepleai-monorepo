using Api.BoundedContexts.AgentMemory.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.AgentMemory.Application.Validators;

/// <summary>
/// Validates <see cref="UpdateHouseRuleCommand"/> inputs (#1464). Mirrors the
/// AddHouseRuleCommandValidator caps so the 2000-character description limit
/// is enforced at the pipeline layer instead of the domain only.
/// </summary>
internal sealed class UpdateHouseRuleCommandValidator : AbstractValidator<UpdateHouseRuleCommand>
{
    public UpdateHouseRuleCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty().WithMessage("Game ID is required");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Owner ID is required");
        RuleFor(x => x.RuleId).NotEmpty().WithMessage("Rule ID is required");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters");
    }
}

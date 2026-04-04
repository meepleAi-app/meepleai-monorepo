using Api.BoundedContexts.AgentMemory.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.AgentMemory.Application.Validators;

/// <summary>
/// Validates <see cref="AddHouseRuleCommand"/> inputs.
/// </summary>
internal sealed class AddHouseRuleCommandValidator : AbstractValidator<AddHouseRuleCommand>
{
    public AddHouseRuleCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required");

        RuleFor(x => x.OwnerId)
            .NotEmpty()
            .WithMessage("Owner ID is required");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required")
            .MaximumLength(2000)
            .WithMessage("Description cannot exceed 2000 characters");
    }
}

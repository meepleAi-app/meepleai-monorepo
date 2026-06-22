using Api.BoundedContexts.AgentMemory.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.AgentMemory.Application.Validators;

/// <summary>
/// Validates <see cref="RemoveHouseRuleCommand"/> inputs (#1464). Route constraints
/// already reject empty Guids, but pipeline validation gives early rejection and
/// keeps the command surface uniform with the other AgentMemory commands.
/// </summary>
internal sealed class RemoveHouseRuleCommandValidator : AbstractValidator<RemoveHouseRuleCommand>
{
    public RemoveHouseRuleCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty().WithMessage("Game ID is required");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Owner ID is required");
        RuleFor(x => x.RuleId).NotEmpty().WithMessage("Rule ID is required");
    }
}

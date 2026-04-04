using Api.BoundedContexts.AgentMemory.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.AgentMemory.Application.Validators;

/// <summary>
/// Validates <see cref="ClaimGuestPlayerCommand"/> inputs.
/// </summary>
internal sealed class ClaimGuestPlayerCommandValidator : AbstractValidator<ClaimGuestPlayerCommand>
{
    public ClaimGuestPlayerCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.PlayerMemoryId)
            .NotEmpty()
            .WithMessage("Player memory ID is required");
    }
}

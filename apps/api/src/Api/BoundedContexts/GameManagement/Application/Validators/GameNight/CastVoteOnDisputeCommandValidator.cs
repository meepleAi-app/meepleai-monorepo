using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNight;

/// <summary>
/// Validates <see cref="CastVoteOnDisputeCommand"/> inputs.
/// </summary>
internal sealed class CastVoteOnDisputeCommandValidator : AbstractValidator<CastVoteOnDisputeCommand>
{
    public CastVoteOnDisputeCommandValidator()
    {
        RuleFor(x => x.DisputeId)
            .NotEmpty()
            .WithMessage("Dispute ID is required");

        RuleFor(x => x.PlayerId)
            .NotEmpty()
            .WithMessage("Player ID is required");
    }
}

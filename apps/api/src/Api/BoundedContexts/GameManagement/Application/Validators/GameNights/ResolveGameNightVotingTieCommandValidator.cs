using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>Validator for ResolveGameNightVotingTieCommand — Issue #2700.</summary>
internal sealed class ResolveGameNightVotingTieCommandValidator
    : AbstractValidator<ResolveGameNightVotingTieCommand>
{
    public ResolveGameNightVotingTieCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty().WithMessage("Game night ID is required");
        RuleFor(x => x.HostUserId).NotEmpty().WithMessage("Host user ID is required");
        RuleFor(x => x.WinningCandidateGameId).NotEmpty().WithMessage("Winning candidate game ID is required");
    }
}

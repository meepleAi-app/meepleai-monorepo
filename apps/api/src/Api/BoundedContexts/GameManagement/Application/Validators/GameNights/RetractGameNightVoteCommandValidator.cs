using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>Validator for RetractGameNightVoteCommand — Issue #2700.</summary>
internal sealed class RetractGameNightVoteCommandValidator : AbstractValidator<RetractGameNightVoteCommand>
{
    public RetractGameNightVoteCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty().WithMessage("Game night ID is required");
        RuleFor(x => x.VoterUserId).NotEmpty().WithMessage("Voter user ID is required");
        RuleFor(x => x.CandidateGameId).NotEmpty().WithMessage("Candidate game ID is required");
    }
}

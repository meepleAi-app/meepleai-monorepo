using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNight;

/// <summary>
/// Validates <see cref="RespondToDisputeCommand"/> inputs.
/// </summary>
internal sealed class RespondToDisputeCommandValidator : AbstractValidator<RespondToDisputeCommand>
{
    public RespondToDisputeCommandValidator()
    {
        RuleFor(x => x.DisputeId)
            .NotEmpty()
            .WithMessage("Dispute ID is required");

        RuleFor(x => x.RespondentPlayerId)
            .NotEmpty()
            .WithMessage("Respondent player ID is required");

        RuleFor(x => x.RespondentClaim)
            .NotEmpty()
            .WithMessage("Respondent claim is required")
            .MaximumLength(2000)
            .WithMessage("Respondent claim cannot exceed 2000 characters");
    }
}

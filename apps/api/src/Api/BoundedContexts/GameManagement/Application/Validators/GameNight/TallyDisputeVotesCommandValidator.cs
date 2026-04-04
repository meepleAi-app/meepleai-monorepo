using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNight;

/// <summary>
/// Validates <see cref="TallyDisputeVotesCommand"/> inputs.
/// </summary>
internal sealed class TallyDisputeVotesCommandValidator : AbstractValidator<TallyDisputeVotesCommand>
{
    public TallyDisputeVotesCommandValidator()
    {
        RuleFor(x => x.DisputeId)
            .NotEmpty()
            .WithMessage("Dispute ID is required");
    }
}

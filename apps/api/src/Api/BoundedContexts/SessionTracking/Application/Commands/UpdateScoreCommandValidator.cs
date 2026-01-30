using MediatR;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class UpdateScoreCommandValidator : AbstractValidator<UpdateScoreCommand>
{
    public UpdateScoreCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("ParticipantId is required");

        RuleFor(x => x.ScoreValue)
            .InclusiveBetween(-99999, 99999)
            .WithMessage("ScoreValue must be between -99999 and 99999");

        RuleFor(x => x.RoundNumber)
            .GreaterThanOrEqualTo(1)
            .When(x => x.RoundNumber.HasValue)
            .WithMessage("RoundNumber must be >= 1 when specified");

        RuleFor(x => x.Category)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.Category))
            .WithMessage("Category must not exceed 50 characters");
    }
}

using MediatR;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class AddParticipantCommandValidator : AbstractValidator<AddParticipantCommand>
{
    public AddParticipantCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("DisplayName is required")
            .MaximumLength(50)
            .WithMessage("DisplayName must not exceed 50 characters");
    }
}

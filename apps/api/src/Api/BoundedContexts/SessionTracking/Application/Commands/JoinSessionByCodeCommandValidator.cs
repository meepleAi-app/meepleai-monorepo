using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class JoinSessionByCodeCommandValidator : AbstractValidator<JoinSessionByCodeCommand>
{
    public JoinSessionByCodeCommandValidator()
    {
        RuleFor(x => x.SessionCode)
            .NotEmpty()
            .WithMessage("Session code is required")
            .Length(6)
            .WithMessage("Session code must be exactly 6 characters")
            .Matches("^[A-Za-z0-9]+$")
            .WithMessage("Session code must be alphanumeric");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("Display name is required")
            .MaximumLength(50)
            .WithMessage("Display name must be at most 50 characters");
    }
}

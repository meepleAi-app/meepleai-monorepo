using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Sessions;

/// <summary>
/// Validates CreateSessionCommand.
/// Ensures user ID is provided and optional fields have length limits.
/// </summary>
internal sealed class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.IpAddress)
            .MaximumLength(45)
            .WithMessage("IP address must not exceed 45 characters")
            .When(x => x.IpAddress != null);

        RuleFor(x => x.UserAgent)
            .MaximumLength(500)
            .WithMessage("User agent must not exceed 500 characters")
            .When(x => x.UserAgent != null);
    }
}

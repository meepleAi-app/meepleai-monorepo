using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.CreateShareLink;

/// <summary>
/// Validates CreateShareLinkCommand.
/// Ensures thread ID, user ID, and expiration are valid.
/// </summary>
internal sealed class CreateShareLinkCommandValidator : AbstractValidator<CreateShareLinkCommand>
{
    public CreateShareLinkCommandValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty()
            .WithMessage("Thread ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("Role must be a valid ShareLinkRole value");

        RuleFor(x => x.ExpiresAt)
            .Must(date => date > DateTime.UtcNow)
            .WithMessage("Expiration date must be in the future");

        RuleFor(x => x.Label)
            .MaximumLength(200)
            .WithMessage("Label must not exceed 200 characters")
            .When(x => x.Label != null);
    }
}

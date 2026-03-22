using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for LogoutCommand.
/// Ensures session token is provided.
/// </summary>
internal sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.SessionToken)
            .NotEmpty()
            .WithMessage("Session token is required")
            .MaximumLength(500)
            .WithMessage("Session token must not exceed 500 characters");
    }
}

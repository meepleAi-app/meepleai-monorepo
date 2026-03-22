using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Sessions;

/// <summary>
/// Validates RevokeAllUserSessionsCommand.
/// Ensures user ID is provided.
/// </summary>
internal sealed class RevokeAllUserSessionsCommandValidator : AbstractValidator<RevokeAllUserSessionsCommand>
{
    public RevokeAllUserSessionsCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}

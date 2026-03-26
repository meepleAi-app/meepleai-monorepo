using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.RevokeShareLink;

/// <summary>
/// Validates RevokeShareLinkCommand.
/// Ensures share link ID and user ID are provided.
/// </summary>
internal sealed class RevokeShareLinkCommandValidator : AbstractValidator<RevokeShareLinkCommand>
{
    public RevokeShareLinkCommandValidator()
    {
        RuleFor(x => x.ShareLinkId)
            .NotEmpty()
            .WithMessage("Share link ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}

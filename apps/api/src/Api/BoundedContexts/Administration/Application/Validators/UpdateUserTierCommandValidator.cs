using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for UpdateUserTierCommand.
/// Ensures user ID, tier name, and requester ID are valid.
/// </summary>
internal sealed class UpdateUserTierCommandValidator : AbstractValidator<UpdateUserTierCommand>
{
    private static readonly string[] AllowedTiers = { "Free", "Basic", "Pro", "Enterprise" };

    public UpdateUserTierCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.NewTier)
            .NotEmpty()
            .WithMessage("NewTier is required")
            .Must(t => AllowedTiers.Contains(t, StringComparer.Ordinal))
            .WithMessage("NewTier must be one of: Free, Basic, Pro, Enterprise");

        RuleFor(x => x.RequesterUserId)
            .NotEmpty()
            .WithMessage("RequesterUserId is required");
    }
}

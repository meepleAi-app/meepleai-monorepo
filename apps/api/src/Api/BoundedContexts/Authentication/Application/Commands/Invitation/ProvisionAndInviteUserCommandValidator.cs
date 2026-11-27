using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Validator for ProvisionAndInviteUserCommand.
/// Issue #124: Admin invitation flow.
/// </summary>
internal sealed class ProvisionAndInviteUserCommandValidator : AbstractValidator<ProvisionAndInviteUserCommand>
{
    private static readonly string[] ValidRoles = { "User", "Editor", "Admin" };
    // Volutamente PIU' STRETTO del dominio: UserTier riconosce anche "normal" ed "enterprise",
    // ma non si invita nessuno direttamente in Enterprise — e InvitationSecurityTests lo asserisce.
    // E' una politica di invito, non il vocabolario dei tier (#3842).
    private static readonly string[] ValidTiers = { "Free", "Premium", "Pro" };

    public ProvisionAndInviteUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(256)
            .WithMessage("Email must not exceed 256 characters");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("Display name is required")
            .MinimumLength(2)
            .WithMessage("Display name must be at least 2 characters")
            .MaximumLength(100)
            .WithMessage("Display name must not exceed 100 characters");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required")
            .Must(role => ValidRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Role must be one of: {string.Join(", ", ValidRoles)}");

        RuleFor(x => x.Tier)
            .NotEmpty()
            .WithMessage("Tier is required")
            .Must(tier => ValidTiers.Contains(tier, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Tier must be one of: {string.Join(", ", ValidTiers)}");

        RuleFor(x => x.CustomMessage)
            .MaximumLength(500)
            .WithMessage("Custom message must not exceed 500 characters")
            .When(x => x.CustomMessage is not null);

        RuleFor(x => x.ExpiresInDays)
            .InclusiveBetween(1, 30)
            .WithMessage("ExpiresInDays must be between 1 and 30");

        RuleFor(x => x.InvitedByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("InvitedByUserId is required");

    }
}

using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Waitlist;

/// <summary>
/// FluentValidation rules for <see cref="JoinWaitlistCommand"/>.
/// Spec §3.5 / §4.4 (2026-04-27-v2-migration-wave-a-2-join.md).
/// </summary>
internal class JoinWaitlistCommandValidator : AbstractValidator<JoinWaitlistCommand>
{
    /// <summary>
    /// Allowlist of game preference IDs accepted by the public waitlist form.
    /// Mirrors the frontend TOP_GAMES catalog (spec §3.3).
    /// Keep in sync with <c>apps/web/src/lib/join/games.ts</c> until a single source of truth is introduced.
    /// </summary>
    private static readonly HashSet<string> AllowedGameIds = new(StringComparer.Ordinal)
    {
        "g-azul",
        "g-catan",
        "g-wingspan",
        "g-brass",
        "g-gloomhaven",
        "g-arknova",
        "g-spirit",
        "g-7wonders",
        "g-carcassonne",
        "g-ticket",
        "g-other",
    };

    public JoinWaitlistCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(254).WithMessage("Email must not exceed 254 characters.");

        RuleFor(x => x.Name)
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.GamePreferenceId)
            .NotEmpty().WithMessage("Game preference is required.")
            .MaximumLength(40).WithMessage("Game preference id must not exceed 40 characters.")
            .Must(id => id is not null && AllowedGameIds.Contains(id))
                .WithMessage("Game preference is not in the allowed catalog.");

        When(x => string.Equals(x.GamePreferenceId, "g-other", StringComparison.Ordinal), () =>
        {
            RuleFor(x => x.GamePreferenceOther)
                .NotEmpty().WithMessage("Specify the game when 'Altro' is selected.")
                .MinimumLength(2).WithMessage("Game name must be at least 2 characters.")
                .MaximumLength(80).WithMessage("Game name must not exceed 80 characters.");
        });
    }
}

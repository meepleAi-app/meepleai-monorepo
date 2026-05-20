using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// Validator for CreateGameNightCommand.
/// Issue #43: Game Night CRUD validators.
/// Issue #950 (W1-PR1): combined invitee cap (userIds + emails ≤ 49)
/// + per-email format/length per RFC 5321 + spec §7b.4.
/// </summary>
internal sealed class CreateGameNightCommandValidator : AbstractValidator<CreateGameNightCommand>
{
    /// <summary>
    /// Combined cap across <see cref="CreateGameNightCommand.InvitedUserIds"/>
    /// and <see cref="CreateGameNightCommand.InvitedEmails"/>. Mirrors the legacy
    /// 49-invitee rule (#43) which previously applied to <c>InvitedUserIds</c> alone.
    /// </summary>
    public const int MaxCombinedInvitees = 49;

    /// <summary>
    /// Spec §7b.4 max email length (more conservative than RFC 5321's 254).
    /// </summary>
    public const int MaxEmailLength = 200;

    // Pragmatic RFC 5321 gate: rejects obvious format errors (no @, no domain dot,
    // whitespace, control chars) without attempting full RFC parsing.
    // The aggregate factory lower-cases + trims; this rule only blocks malformed inputs.
    private static readonly Regex EmailFormatRegex = new(
        pattern: @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeout: TimeSpan.FromMilliseconds(100));

    public CreateGameNightCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required")
            .MinimumLength(3)
            .WithMessage("Title must be at least 3 characters")
            .MaximumLength(200)
            .WithMessage("Title cannot exceed 200 characters");

        RuleFor(x => x.ScheduledAt)
            .Must(scheduledAt => scheduledAt > DateTimeOffset.UtcNow.AddHours(1))
            .WithMessage("Scheduled time must be at least 1 hour in the future");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description != null)
            .WithMessage("Description cannot exceed 2000 characters");

        RuleFor(x => x.Location)
            .MaximumLength(500)
            .When(x => x.Location != null)
            .WithMessage("Location cannot exceed 500 characters");

        RuleFor(x => x.MaxPlayers)
            .InclusiveBetween(2, 50)
            .When(x => x.MaxPlayers.HasValue)
            .WithMessage("Max players must be between 2 and 50");

        RuleFor(x => x.GameIds)
            .Must(ids => ids == null || ids.Count <= 20)
            .WithMessage("Cannot include more than 20 games");

        // Legacy rule (#43): userIds alone ≤ 49. Preserved as a quick gate before the
        // combined check (clearer error when only userIds are provided).
        RuleFor(x => x.InvitedUserIds)
            .Must(ids => ids == null || ids.Count <= MaxCombinedInvitees)
            .WithMessage($"Cannot invite more than {MaxCombinedInvitees} users");

        // Issue #950 (W1-PR1): per-email format/length.
        RuleForEach(x => x.InvitedEmails)
            .NotEmpty()
            .WithMessage("Email cannot be empty")
            .MaximumLength(MaxEmailLength)
            .WithMessage($"Email cannot exceed {MaxEmailLength} characters")
            .Must(email => !string.IsNullOrWhiteSpace(email) && EmailFormatRegex.IsMatch(email))
            .WithMessage("Invalid email format")
            .When(x => x.InvitedEmails != null);

        // Issue #950 (W1-PR1): combined invitee cap (userIds + emails ≤ 49) — spec §12b BE-7.
        // Validated on the command itself so the error surfaces independently of which
        // collection pushed the total over the limit.
        RuleFor(x => x)
            .Must(cmd => (cmd.InvitedUserIds?.Count ?? 0) + (cmd.InvitedEmails?.Count ?? 0) <= MaxCombinedInvitees)
            .WithMessage($"Combined invited users and emails cannot exceed {MaxCombinedInvitees}")
            .WithName(nameof(CreateGameNightCommand.InvitedEmails));

        // Issue #950 (W1-PR1) — code review PR #1289 feedback:
        // Reject duplicate emails (case-insensitive + trim) before they reach the chain
        // loop in CreateGameNightCommandHandler. The sub-handler
        // CreateGameNightInvitationByEmailCommandHandler normalizes
        // (Trim + ToLowerInvariant) and throws ConflictException on the 2nd duplicate
        // (after the 1st invitation has already been committed). Catching duplicates here
        // turns a partial-success 409 into a 400 validation error before any persistence.
        RuleFor(x => x.InvitedEmails)
            .Must(emails => emails == null || HasNoCaseInsensitiveDuplicates(emails))
            .WithMessage("InvitedEmails cannot contain duplicate emails (case-insensitive)")
            .When(x => x.InvitedEmails is { Count: > 1 });
    }

    private static bool HasNoCaseInsensitiveDuplicates(IReadOnlyList<string> emails)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in emails)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                // Format/empty checks fire from RuleForEach; skip them here so the
                // dedup error doesn't double-flag malformed inputs.
                continue;
            }

            if (!seen.Add(email.Trim()))
            {
                return false;
            }
        }
        return true;
    }
}

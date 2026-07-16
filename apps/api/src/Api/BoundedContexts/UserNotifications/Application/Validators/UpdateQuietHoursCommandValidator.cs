using System.Globalization;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;
using TimeZoneConverter;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

/// <summary>
/// Validates <see cref="UpdateQuietHoursCommand"/> (ADR-076, issue #2995).
/// <para>
/// Guards the domain invariants BEFORE the aggregate mutator runs so a bad payload returns
/// 400 (FluentValidation) rather than bubbling a domain <see cref="ArgumentException"/> up to a
/// 500 (project rule #2568). Enforces: valid IANA/Windows timezone, parseable HH:mm times, and
/// the both-set-or-both-null pairing that <see cref="Api.BoundedContexts.UserNotifications.Domain.Aggregates.NotificationPreferences.UpdateQuietHours"/> requires.
/// </para>
/// </summary>
internal sealed class UpdateQuietHoursCommandValidator : AbstractValidator<UpdateQuietHoursCommand>
{
    public UpdateQuietHoursCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.TimeZone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("TimeZone is required")
            .Must(BeAValidTimeZone)
            .WithMessage("TimeZone must be a valid IANA or Windows time zone identifier");

        RuleFor(x => x.QuietHoursStart)
            .Must(BeAValidTimeOrEmpty)
            .WithMessage("QuietHoursStart must be a valid time in HH:mm format");

        RuleFor(x => x.QuietHoursEnd)
            .Must(BeAValidTimeOrEmpty)
            .WithMessage("QuietHoursEnd must be a valid time in HH:mm format");

        // Both-set-or-both-null: mirrors the domain invariant so a partial window is a 400.
        RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.QuietHoursStart) == string.IsNullOrWhiteSpace(x.QuietHoursEnd))
            .WithName("QuietHours")
            .WithMessage("QuietHoursStart and QuietHoursEnd must both be set or both be empty");
    }

    private static bool BeAValidTimeZone(string? timeZone) =>
        !string.IsNullOrWhiteSpace(timeZone) && TZConvert.TryGetTimeZoneInfo(timeZone, out _);

    private static bool BeAValidTimeOrEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}

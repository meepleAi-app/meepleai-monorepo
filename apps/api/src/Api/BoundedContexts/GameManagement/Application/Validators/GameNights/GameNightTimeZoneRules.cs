using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// The one shape check for the IANA zone a client sends with a game night (#4754), shared by the create and
/// update validators so the two verbs answer the same input the same way.
/// </summary>
/// <remarks>
/// Only the SHAPE is checked (<c>Europe/Rome</c>, <c>America/Argentina/Buenos_Aires</c>, <c>Etc/GMT+5</c>,
/// <c>UTC</c>): that is enough to keep junk out of the column. Whether the id names a zone this server knows
/// is not asked here, because the zone only feeds a display and a night must not fail to be created over it;
/// the email formatter resolves it and says UTC, out loud, when it cannot.
/// </remarks>
internal static class GameNightTimeZoneRules
{
    private static readonly Regex ShapeRegex = new(
        pattern: @"^[A-Za-z][A-Za-z0-9_+\-]*(/[A-Za-z0-9_+\-]+){0,3}$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeout: TimeSpan.FromMilliseconds(100));

    /// <summary>Blank is "not sent" and is accepted: the aggregate turns it into null.</summary>
    public static bool IsAcceptable(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return true;

        var trimmed = timeZoneId.Trim();
        return trimmed.Length <= GameNightEvent.TimeZoneIdMaxLength && ShapeRegex.IsMatch(trimmed);
    }

    public static IRuleBuilderOptions<T, string?> MustBeAnIanaZoneId<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(IsAcceptable)
            .WithMessage($"Time zone must be an IANA zone id (e.g. Europe/Rome) of at most {GameNightEvent.TimeZoneIdMaxLength} characters");
}

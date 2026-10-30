namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Service for sending game night-related emails with HTML templates.
/// Issue #44/#47: Game night notification types and email templates.
/// Issue #4754: every method takes the <see cref="GameNightEmailLocale"/> (the organizer's language and the
/// night's time zone) so the text and the time are written the way the organizer meant them. The two emails
/// that go to a guest — the invitation and the RSVP confirmation — take no unsubscribe link: the address is
/// not an account, there is no preference page behind it, and the link the footer used to carry had no page.
/// </summary>
internal interface IGameNightEmailService
{
    Task SendGameNightInvitationEmailAsync(
        string toEmail,
        string organizerName,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        IReadOnlyList<string> gameNames,
        string rsvpAcceptUrl,
        string rsvpDeclineUrl,
        GameNightEmailLocale locale,
        CancellationToken ct = default);

    Task SendGameNightReminder24hEmailAsync(
        string toEmail,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        int confirmedCount,
        bool isPending,
        string unsubscribeUrl,
        GameNightEmailLocale locale,
        CancellationToken ct = default);

    Task SendGameNightCancelledEmailAsync(
        string toEmail,
        string organizerName,
        string title,
        DateTimeOffset scheduledAt,
        string unsubscribeUrl,
        GameNightEmailLocale locale,
        CancellationToken ct = default);

    Task SendGameNightRsvpConfirmationEmailAsync(
        string toEmail,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        GameNightEmailLocale locale,
        CancellationToken ct = default);
}

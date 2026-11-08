namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Service for sending game night-related emails with HTML templates.
/// Issue #44/#47: Game night notification types and email templates.
/// Issue #4754: every method takes the <see cref="GameNightEmailLocale"/> (the organizer's language and the
/// night's time zone) so the text and the time are written the way the organizer meant them. The emails
/// that go to a guest — the invitation, the RSVP confirmation and (#4853) the cancellation — carry the
/// one-tap opt-out link (#4846): the address is not an account, so there is no preference page behind it, and the
/// link the footer used to carry (#4754) pointed at a page that did not exist. The opt-out is keyed by the address
/// and authorised by the invitation token. The 24 hour reminder (#4846) is one of the emails to a guest: the
/// registered members are reminded in the app by the notification dispatcher, and this one goes to the guests who
/// were invited by address.
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
        string unsubscribeUrl,
        GameNightEmailLocale locale,
        CancellationToken ct = default);

    Task SendGameNightReminder24hEmailAsync(
        string toEmail,
        string organizerName,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        int confirmedCount,
        bool isPending,
        string rsvpUrl,
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
        string unsubscribeUrl,
        GameNightEmailLocale locale,
        CancellationToken ct = default);
}

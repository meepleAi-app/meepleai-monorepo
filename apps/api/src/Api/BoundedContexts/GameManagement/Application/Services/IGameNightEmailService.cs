namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Service for sending game night-related emails with HTML templates.
/// Issue #44/#47: Game night notification types and email templates.
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
        CancellationToken ct = default);

    Task SendGameNightReminder24hEmailAsync(
        string toEmail,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        int confirmedCount,
        bool isPending,
        string unsubscribeUrl,
        CancellationToken ct = default);

    Task SendGameNightCancelledEmailAsync(
        string toEmail,
        string organizerName,
        string title,
        DateTimeOffset scheduledAt,
        string unsubscribeUrl,
        CancellationToken ct = default);

    Task SendGameNightRsvpConfirmationEmailAsync(
        string toEmail,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        string unsubscribeUrl,
        CancellationToken ct = default);
}

using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to update a user's quiet-hours window (ADR-076, issue #2995).
/// <para>
/// Quiet hours gate time-sensitive channels (email + Slack DM) server-side during a
/// user-defined window; in-app notifications are never suppressed. Times are transported
/// as "HH:mm" strings (matching the HTML <c>&lt;input type="time"&gt;</c> value format) and
/// parsed to <see cref="TimeOnly"/> in the handler. Pass null/empty start AND end to clear
/// (disable) quiet hours.
/// </para>
/// </summary>
internal record UpdateQuietHoursCommand(
    Guid UserId,
    string TimeZone,
    string? QuietHoursStart,
    string? QuietHoursEnd
) : ICommand;

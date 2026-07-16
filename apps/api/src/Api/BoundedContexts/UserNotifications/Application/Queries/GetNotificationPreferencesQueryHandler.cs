using System.Globalization;
using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

internal class GetNotificationPreferencesQueryHandler : IQueryHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    private readonly INotificationPreferencesRepository _repository;

    public GetNotificationPreferencesQueryHandler(INotificationPreferencesRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<NotificationPreferencesDto> Handle(
        GetNotificationPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        var prefs = await _repository.GetByUserIdAsync(query.UserId, cancellationToken).ConfigureAwait(false)
            ?? new Api.BoundedContexts.UserNotifications.Domain.Aggregates.NotificationPreferences(query.UserId);

        return new NotificationPreferencesDto(
            prefs.UserId,
            prefs.EmailOnDocumentReady, prefs.EmailOnDocumentFailed, prefs.EmailOnRetryAvailable,
            prefs.PushOnDocumentReady, prefs.PushOnDocumentFailed, prefs.PushOnRetryAvailable,
            prefs.InAppOnDocumentReady, prefs.InAppOnDocumentFailed, prefs.InAppOnRetryAvailable,
            prefs.HasPushSubscription,
            prefs.EmailOnCardSuppressed,
            // Quiet hours (ADR-076) — TimeOnly? formatted as "HH:mm" for a stable FE contract.
            prefs.TimeZone,
            FormatTime(prefs.QuietHoursStart),
            FormatTime(prefs.QuietHoursEnd),
            // Slack channel preferences.
            prefs.SlackEnabled,
            prefs.SlackOnDocumentReady, prefs.SlackOnDocumentFailed, prefs.SlackOnRetryAvailable,
            prefs.SlackOnGameNightInvitation, prefs.SlackOnGameNightReminder,
            prefs.SlackOnShareRequestCreated, prefs.SlackOnShareRequestApproved, prefs.SlackOnBadgeEarned
        );
    }

    private static string? FormatTime(TimeOnly? value) =>
        value?.ToString("HH:mm", CultureInfo.InvariantCulture);
}

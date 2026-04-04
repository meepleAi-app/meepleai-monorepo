using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetEmailQueueStatsQuery.
/// Returns aggregated email queue statistics for admin monitoring.
/// Issue #4430: Email queue dashboard monitoring.
/// </summary>
internal class GetEmailQueueStatsQueryHandler : IQueryHandler<GetEmailQueueStatsQuery, EmailQueueStatsDto>
{
    private readonly IEmailQueueRepository _emailQueueRepository;

    public GetEmailQueueStatsQueryHandler(IEmailQueueRepository emailQueueRepository)
    {
        _emailQueueRepository = emailQueueRepository;
    }

    public async Task<EmailQueueStatsDto> Handle(GetEmailQueueStatsQuery query, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var countsByStatus = await _emailQueueRepository
            .GetCountsByStatusAsync(cancellationToken).ConfigureAwait(false);

        var sentLastHour = await _emailQueueRepository
            .GetSentCountSinceAsync(now.AddHours(-1), cancellationToken).ConfigureAwait(false);

        var sentLast24Hours = await _emailQueueRepository
            .GetSentCountSinceAsync(now.AddHours(-24), cancellationToken).ConfigureAwait(false);

        return new EmailQueueStatsDto(
            PendingCount: countsByStatus.GetValueOrDefault("pending"),
            ProcessingCount: countsByStatus.GetValueOrDefault("processing"),
            SentCount: countsByStatus.GetValueOrDefault("sent"),
            FailedCount: countsByStatus.GetValueOrDefault("failed"),
            DeadLetterCount: countsByStatus.GetValueOrDefault("dead_letter"),
            SentLastHour: sentLastHour,
            SentLast24Hours: sentLast24Hours
        );
    }
}

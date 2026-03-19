using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetEmailHistoryQuery.
/// Returns paginated email history for a user.
/// Issue #4417: Email notification queue.
/// </summary>
internal class GetEmailHistoryQueryHandler : IQueryHandler<GetEmailHistoryQuery, EmailHistoryResult>
{
    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly ILogger<GetEmailHistoryQueryHandler> _logger;
    private const int MaxPageSize = 50;

    public GetEmailHistoryQueryHandler(
        IEmailQueueRepository emailQueueRepository,
        ILogger<GetEmailHistoryQueryHandler> logger)
    {
        _emailQueueRepository = emailQueueRepository;
        _logger = logger;
    }

    public async Task<EmailHistoryResult> Handle(GetEmailHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var effectiveTake = Math.Min(query.Take, MaxPageSize);
        var effectiveSkip = Math.Max(query.Skip, 0);

        var items = await _emailQueueRepository.GetByUserIdAsync(
            query.UserId, effectiveSkip, effectiveTake, cancellationToken).ConfigureAwait(false);

        var totalCount = await _emailQueueRepository.GetCountByUserIdAsync(
            query.UserId, cancellationToken).ConfigureAwait(false);

        var dtos = items.Select(item => new EmailQueueItemDto(
            Id: item.Id,
            UserId: item.UserId,
            To: item.To,
            Subject: item.Subject,
            Status: item.Status.Value,
            RetryCount: item.RetryCount,
            MaxRetries: item.MaxRetries,
            ErrorMessage: item.ErrorMessage,
            CreatedAt: item.CreatedAt,
            ProcessedAt: item.ProcessedAt,
            FailedAt: item.FailedAt
        )).ToList();

        _logger.LogDebug("Retrieved {Count} email history items for user {UserId}", dtos.Count, query.UserId);

        return new EmailHistoryResult(dtos, totalCount, effectiveSkip, effectiveTake);
    }
}

using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetDeadLetterEmailsQuery.
/// Returns paginated dead-lettered emails for admin review.
/// Issue #4430: Email queue dashboard monitoring.
/// </summary>
internal class GetDeadLetterEmailsQueryHandler : IQueryHandler<GetDeadLetterEmailsQuery, DeadLetterEmailsResult>
{
    private readonly IEmailQueueRepository _emailQueueRepository;
    private const int MaxPageSize = 50;

    public GetDeadLetterEmailsQueryHandler(IEmailQueueRepository emailQueueRepository)
    {
        _emailQueueRepository = emailQueueRepository;
    }

    public async Task<DeadLetterEmailsResult> Handle(GetDeadLetterEmailsQuery query, CancellationToken cancellationToken)
    {
        var effectiveTake = Math.Min(query.Take, MaxPageSize);
        var effectiveSkip = Math.Max(query.Skip, 0);

        var items = await _emailQueueRepository.GetDeadLetterAsync(
            effectiveSkip, effectiveTake, cancellationToken).ConfigureAwait(false);

        var totalCount = await _emailQueueRepository.GetDeadLetterCountAsync(
            cancellationToken).ConfigureAwait(false);

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

        return new DeadLetterEmailsResult(dtos, totalCount, effectiveSkip, effectiveTake);
    }
}

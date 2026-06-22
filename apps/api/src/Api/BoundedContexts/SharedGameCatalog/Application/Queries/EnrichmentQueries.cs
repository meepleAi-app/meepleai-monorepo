using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

// ── DTOs ────────────────────────────────────────────────────────────

public sealed record EnrichmentQueueItem(
    Guid SharedGameId,
    string Title,
    EnrichmentPriority Priority,
    DateTimeOffset QueuedAt,
    string Reason,
    Guid? QueuedBy);

public sealed record EnrichmentQueueResult(
    IReadOnlyList<EnrichmentQueueItem> Items,
    int Total);

public sealed record FailedItem(
    Guid SharedGameId,
    string Title,
    string ErrorCode,
    string ErrorDetail,
    DateTimeOffset LastAttemptAt,
    int RetryCount);

public sealed record FailedItemsResult(
    IReadOnlyList<FailedItem> Items,
    int Total);

// ── Queries ─────────────────────────────────────────────────────────

public sealed record GetEnrichmentQueueQuery(EnrichmentPriority? Priority, int Limit)
    : IQuery<EnrichmentQueueResult>;

public sealed record GetFailedItemsQuery(int Days, int Limit)
    : IQuery<FailedItemsResult>;

// ── Validators ──────────────────────────────────────────────────────

public sealed class GetEnrichmentQueueQueryValidator : AbstractValidator<GetEnrichmentQueueQuery>
{
    public GetEnrichmentQueueQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}

public sealed class GetFailedItemsQueryValidator : AbstractValidator<GetFailedItemsQuery>
{
    public GetFailedItemsQueryValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 365);
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}

// ── Handlers ────────────────────────────────────────────────────────

internal sealed class GetEnrichmentQueueQueryHandler
    : IQueryHandler<GetEnrichmentQueueQuery, EnrichmentQueueResult>
{
    private readonly IEnrichmentQueueRepository _repository;

    public GetEnrichmentQueueQueryHandler(IEnrichmentQueueRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<EnrichmentQueueResult> Handle(
        GetEnrichmentQueueQuery query, CancellationToken cancellationToken)
    {
        var (rows, total) = await _repository
            .GetPendingAsync(query.Priority, query.Limit, cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(r => new EnrichmentQueueItem(
                SharedGameId: r.Entry.SharedGameId,
                Title: r.SharedGameTitle,
                Priority: r.Entry.Priority,
                QueuedAt: r.Entry.QueuedAt,
                Reason: r.Entry.Reason,
                QueuedBy: r.Entry.QueuedByUserId))
            .ToList();

        return new EnrichmentQueueResult(items, total);
    }
}

internal sealed class GetFailedItemsQueryHandler
    : IQueryHandler<GetFailedItemsQuery, FailedItemsResult>
{
    private readonly IEnrichmentAttemptRepository _repository;

    public GetFailedItemsQueryHandler(IEnrichmentAttemptRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<FailedItemsResult> Handle(
        GetFailedItemsQuery query, CancellationToken cancellationToken)
    {
        var (rows, total) = await _repository
            .GetFailedAggregatesAsync(query.Days, query.Limit, cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(r => new FailedItem(
                SharedGameId: r.SharedGameId,
                Title: r.SharedGameTitle,
                ErrorCode: r.ErrorCode,
                ErrorDetail: r.ErrorDetail,
                LastAttemptAt: r.LastAttemptAt,
                RetryCount: r.RetryCount))
            .ToList();

        return new FailedItemsResult(items, total);
    }
}

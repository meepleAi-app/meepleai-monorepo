using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts;

internal sealed class GetKbNavCountsQueryHandler
    : IRequestHandler<GetKbNavCountsQuery, KbNavCountsDto>
{
    private static readonly JobStatus[] ActiveStatuses =
        [JobStatus.Queued, JobStatus.Processing, JobStatus.Failed];

    private static readonly TimeSpan FeedbackWindow = TimeSpan.FromDays(7);

    private readonly IProcessingJobRepository _jobs;
    private readonly IKbUserFeedbackRepository _feedback;
    private readonly TimeProvider _clock;

    public GetKbNavCountsQueryHandler(
        IProcessingJobRepository jobs,
        IKbUserFeedbackRepository feedback,
        TimeProvider clock)
    {
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<KbNavCountsDto> Handle(GetKbNavCountsQuery request, CancellationToken cancellationToken)
    {
        var asOf = _clock.GetUtcNow();
        var since = asOf.UtcDateTime - FeedbackWindow;

        // NOTE: these two counts MUST be awaited sequentially. Both repositories share the same
        // scoped MeepleAiDbContext, and EF Core's DbContext is not thread-safe — running them
        // concurrently via Task.WhenAll throws "A second operation was started on this context
        // instance before a previous operation completed" (HTTP 500). The counts are cheap, so the
        // extra round-trip is negligible.
        var processingQueue = await _jobs
            .CountByStatusesAsync(ActiveStatuses, cancellationToken)
            .ConfigureAwait(false);
        var feedback7d = await _feedback
            .CountSinceAsync(since, cancellationToken)
            .ConfigureAwait(false);

        return new KbNavCountsDto(
            ProcessingQueue: processingQueue,
            Feedback7d: feedback7d,
            AsOf: asOf);
    }
}

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

        var queueTask = _jobs.CountByStatusesAsync(ActiveStatuses, cancellationToken);
        var feedbackTask = _feedback.CountSinceAsync(since, cancellationToken);

        await Task.WhenAll(queueTask, feedbackTask).ConfigureAwait(false);

        return new KbNavCountsDto(
            ProcessingQueue: await queueTask.ConfigureAwait(false),
            Feedback7d: await feedbackTask.ConfigureAwait(false),
            AsOf: asOf);
    }
}

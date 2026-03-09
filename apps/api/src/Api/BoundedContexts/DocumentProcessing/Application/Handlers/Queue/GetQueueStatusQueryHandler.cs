using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Returns queue status with backpressure info and estimated wait time.
/// Issue #5457: Queue status for frontend ETA display.
/// </summary>
internal sealed class GetQueueStatusQueryHandler : IQueryHandler<GetQueueStatusQuery, QueueStatusDto>
{
    private readonly IQueueBackpressureService _backpressureService;
    private readonly IProcessingQueueConfigRepository _configRepository;

    public GetQueueStatusQueryHandler(
        IQueueBackpressureService backpressureService,
        IProcessingQueueConfigRepository configRepository)
    {
        _backpressureService = backpressureService ?? throw new ArgumentNullException(nameof(backpressureService));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task<QueueStatusDto> Handle(GetQueueStatusQuery query, CancellationToken cancellationToken)
    {
        var backpressure = await _backpressureService.CheckAsync(cancellationToken).ConfigureAwait(false);
        var config = await _configRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);

        return new QueueStatusDto(
            QueueDepth: backpressure.QueueDepth,
            BackpressureThreshold: backpressure.BackpressureThreshold,
            IsUnderPressure: backpressure.IsUnderPressure,
            IsPaused: config.IsPaused,
            MaxConcurrentWorkers: config.MaxConcurrentWorkers,
            EstimatedWaitMinutes: Math.Round(backpressure.EstimatedWaitTime.TotalMinutes, 1));
    }
}

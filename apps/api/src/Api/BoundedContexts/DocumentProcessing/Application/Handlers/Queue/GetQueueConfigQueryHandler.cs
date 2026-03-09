using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Handles getting the current processing queue configuration.
/// Issue #5455: Queue configuration management.
/// </summary>
internal class GetQueueConfigQueryHandler : IQueryHandler<GetQueueConfigQuery, QueueConfigDto>
{
    private readonly IProcessingQueueConfigRepository _configRepository;

    public GetQueueConfigQueryHandler(IProcessingQueueConfigRepository configRepository)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task<QueueConfigDto> Handle(GetQueueConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _configRepository.GetOrCreateAsync(cancellationToken)
            .ConfigureAwait(false);

        return new QueueConfigDto(
            config.IsPaused,
            config.MaxConcurrentWorkers,
            config.UpdatedAt,
            config.UpdatedBy);
    }
}

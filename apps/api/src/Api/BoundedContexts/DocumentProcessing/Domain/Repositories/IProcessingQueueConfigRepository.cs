using Api.BoundedContexts.DocumentProcessing.Domain.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

/// <summary>
/// Repository interface for the singleton ProcessingQueueConfig.
/// Issue #5455: Queue configuration management.
/// </summary>
internal interface IProcessingQueueConfigRepository
{
    Task<ProcessingQueueConfig> GetOrCreateAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(ProcessingQueueConfig config, CancellationToken cancellationToken = default);
}

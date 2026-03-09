using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Handles updating the processing queue configuration.
/// Issue #5455: Queue configuration management.
/// </summary>
internal class UpdateQueueConfigCommandHandler : ICommandHandler<UpdateQueueConfigCommand>
{
    private readonly IProcessingQueueConfigRepository _configRepository;

    public UpdateQueueConfigCommandHandler(IProcessingQueueConfigRepository configRepository)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task Handle(UpdateQueueConfigCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var config = await _configRepository.GetOrCreateAsync(cancellationToken)
            .ConfigureAwait(false);

        config.Update(command.IsPaused, command.MaxConcurrentWorkers, command.UpdatedBy);

        await _configRepository.UpdateAsync(config, cancellationToken).ConfigureAwait(false);
    }
}

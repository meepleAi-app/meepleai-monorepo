using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Handles <see cref="AgentSummaryGeneratedEvent"/> by persisting the AI-generated
/// conversation summary onto the pause snapshot stored via
/// <see cref="IPauseSnapshotRepository"/>.
///
/// This handler runs after <see cref="GenerateAgentSummaryHandler"/> successfully
/// produces a summary from the LLM. It updates the snapshot so the recap is
/// available when the session is next resumed.
///
/// Game Night Improvvisata — E4: Save/Resume — async agent recap persistence.
/// </summary>
internal sealed class UpdateSnapshotSummaryHandler : INotificationHandler<AgentSummaryGeneratedEvent>
{
    private readonly IPauseSnapshotRepository _snapshotRepository;
    private readonly ILogger<UpdateSnapshotSummaryHandler> _logger;

    public UpdateSnapshotSummaryHandler(
        IPauseSnapshotRepository snapshotRepository,
        ILogger<UpdateSnapshotSummaryHandler> logger)
    {
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AgentSummaryGeneratedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // 1. Load the PauseSnapshot by ID
        var snapshot = await _snapshotRepository
            .GetByIdAsync(notification.PauseSnapshotId, cancellationToken)
            .ConfigureAwait(false);

        if (snapshot is null)
        {
            _logger.LogWarning(
                "UpdateSnapshotSummary: PauseSnapshot {SnapshotId} not found. Summary will be lost.",
                notification.PauseSnapshotId);
            return;
        }

        // 2. Set the summary on the domain entity
        snapshot.UpdateSummary(notification.Summary);

        // 3. Persist the updated snapshot
        await _snapshotRepository.UpdateAsync(snapshot, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "UpdateSnapshotSummary: Summary ({Length} chars) persisted to SnapshotId={SnapshotId}.",
            notification.Summary.Length, notification.PauseSnapshotId);
    }
}

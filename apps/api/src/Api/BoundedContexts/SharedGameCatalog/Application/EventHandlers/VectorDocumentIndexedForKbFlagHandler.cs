using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Listens to VectorDocumentIndexedEvent and maintains the denormalized
/// `has_knowledge_base` column on `shared_games`. Bridges the KnowledgeBase
/// BC (write source) with SharedGameCatalog BC (read projection).
///
/// Flow:
///   1. Read the VectorDocumentEntity by DocumentId to resolve SharedGameId
///      (the event carries GameId which is the Game aggregate id; VectorDocuments
///      carry their own separate SharedGameId per Issue #4921).
///   2. If SharedGameId is not null, execute a targeted bulk update on
///      shared_games to set has_knowledge_base = true. Uses ExecuteUpdateAsync
///      to bypass change tracking — idempotent, low overhead.
///
/// DDD note: the cross-BC read in step 1 is acceptable as an async projection
/// update (not at query time). The write path stays within SharedGameCatalog tables.
///
/// Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.2
/// Plan: docs/superpowers/plans/2026-04-09-s2-kb-filter.md Task 7
/// </summary>
internal sealed class VectorDocumentIndexedForKbFlagHandler
    : INotificationHandler<VectorDocumentIndexedEvent>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<VectorDocumentIndexedForKbFlagHandler> _logger;

    public VectorDocumentIndexedForKbFlagHandler(
        MeepleAiDbContext context,
        ILogger<VectorDocumentIndexedForKbFlagHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(VectorDocumentIndexedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Look up the VectorDocument to resolve SharedGameId.
        // This is the only cross-BC table read in the handler — the update
        // below stays within SharedGameCatalog's own tables.
        var sharedGameId = await _context.VectorDocuments
            .AsNoTracking()
            .Where(v => v.Id == notification.DocumentId)
            .Select(v => v.SharedGameId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sharedGameId is null || sharedGameId == Guid.Empty)
        {
            _logger.LogDebug(
                "VectorDocument {DocumentId} has no SharedGameId, skipping KB flag update",
                notification.DocumentId);
            return;
        }

        // Bulk update bypassing change tracking (perf + avoids loading aggregate).
        // Idempotent: only updates if HasKnowledgeBase is currently false.
        var rowsAffected = await _context.SharedGames
            .Where(g => g.Id == sharedGameId.Value && !g.HasKnowledgeBase)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(g => g.HasKnowledgeBase, true),
                cancellationToken)
            .ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            _logger.LogInformation(
                "Set HasKnowledgeBase=true for SharedGame {SharedGameId} triggered by VectorDocument {DocumentId}",
                sharedGameId.Value,
                notification.DocumentId);
        }
    }
}

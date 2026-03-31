using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.IntegrationEvents;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Syncs agent SelectedDocumentIds when a new VectorDocument becomes available.
/// Fires after PDF indexing is complete (VectorDocumentReadyIntegrationEvent).
///
/// This handler replaces the premature auto-add that was in PrivatePdfAssociatedEventHandler,
/// which fired before indexing and incorrectly stored PdfDocument.Id instead of VectorDocument.Id.
///
/// SelectedDocumentIdsJson MUST contain VectorDocument.Id values — the canonical type
/// consumed by SendAgentMessageCommandHandler via IHybridSearchService.
/// </summary>
internal sealed class AgentDocumentSyncOnReadyHandler : INotificationHandler<VectorDocumentReadyIntegrationEvent>
{
    private readonly IAgentRepository _agentRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<AgentDocumentSyncOnReadyHandler> _logger;

    public AgentDocumentSyncOnReadyHandler(
        IAgentRepository agentRepository,
        MeepleAiDbContext dbContext,
        ILogger<AgentDocumentSyncOnReadyHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        VectorDocumentReadyIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        await HandleAsync(notification, cancellationToken).ConfigureAwait(false);
    }

    internal async Task HandleAsync(
        VectorDocumentReadyIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        // Only auto-add for private PDFs (uploaded by users for their own games).
        // Base/shared documents are selected explicitly via UpdateAgentDocumentsCommand.
        var isPrivatePdf = await _dbContext.PdfDocuments
            .AsNoTracking()
            .AnyAsync(p => p.Id == integrationEvent.PdfDocumentId && p.PrivateGameId != null, cancellationToken)
            .ConfigureAwait(false);

        if (!isPrivatePdf)
        {
            _logger.LogDebug(
                "VectorDocument {DocId} (PDF {PdfId}) is not a private document — skipping auto-add",
                integrationEvent.DocumentId, integrationEvent.PdfDocumentId);
            return;
        }

        var agents = await _agentRepository
            .GetByGameIdAsync(integrationEvent.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (agents.Count == 0)
        {
            _logger.LogInformation(
                "No agent found for game {GameId} — skipping auto-add of VectorDocument {DocId}",
                integrationEvent.GameId, integrationEvent.DocumentId);
            return;
        }

        foreach (var agent in agents)
        {
            var config = await _dbContext.AgentConfigurations
                .FirstOrDefaultAsync(
                    c => c.AgentId == agent.Id && c.IsCurrent,
                    cancellationToken)
                .ConfigureAwait(false);

            if (config == null)
            {
                _logger.LogWarning(
                    "Agent {AgentId} has no current config — skipping auto-add of VectorDocument {DocId}",
                    agent.Id, integrationEvent.DocumentId);
                continue;
            }

            var selectedIds = new List<Guid>();
            if (!string.IsNullOrEmpty(config.SelectedDocumentIdsJson))
            {
                try
                {
                    selectedIds = JsonSerializer.Deserialize<List<Guid>>(config.SelectedDocumentIdsJson)
                        ?? new List<Guid>();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to parse SelectedDocumentIdsJson for agent {AgentId}",
                        agent.Id);
                    selectedIds = new List<Guid>();
                }
            }

            if (selectedIds.Contains(integrationEvent.DocumentId))
            {
                _logger.LogDebug(
                    "VectorDocument {DocId} already in agent {AgentId} — skipping",
                    integrationEvent.DocumentId, agent.Id);
                continue;
            }

            selectedIds.Add(integrationEvent.DocumentId);
            config.SelectedDocumentIdsJson = JsonSerializer.Serialize(selectedIds);

            _logger.LogInformation(
                "Auto-added VectorDocument {DocId} (PDF {PdfId}) to agent {AgentId} for game {GameId}. Total docs: {Count}",
                integrationEvent.DocumentId, integrationEvent.PdfDocumentId,
                agent.Id, integrationEvent.GameId, selectedIds.Count);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

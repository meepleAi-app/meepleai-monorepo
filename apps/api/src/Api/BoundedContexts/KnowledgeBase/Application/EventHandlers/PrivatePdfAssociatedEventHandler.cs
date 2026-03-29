using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

internal sealed class PrivatePdfAssociatedEventHandler : INotificationHandler<PrivatePdfAssociatedEvent>
{
    private readonly IAgentRepository _agentRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<PrivatePdfAssociatedEventHandler> _logger;

    public PrivatePdfAssociatedEventHandler(
        IAgentRepository agentRepository,
        MeepleAiDbContext dbContext,
        ILogger<PrivatePdfAssociatedEventHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PrivatePdfAssociatedEvent notification, CancellationToken cancellationToken)
    {
        await HandleAsync(notification, cancellationToken).ConfigureAwait(false);
    }

    internal async Task HandleAsync(PrivatePdfAssociatedEvent domainEvent, CancellationToken cancellationToken)
    {
        var agents = await _agentRepository
            .GetByGameIdAsync(domainEvent.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (agents.Count == 0)
        {
            _logger.LogInformation(
                "No agent found for game {GameId} — skipping auto-add of PDF {PdfId}",
                domainEvent.GameId, domainEvent.PdfDocumentId);
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
                _logger.LogWarning("Agent {AgentId} has no current config — skipping", agent.Id);
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
                    _logger.LogError(ex, "Failed to parse SelectedDocumentIdsJson for agent {AgentId}", agent.Id);
                    selectedIds = new List<Guid>();
                }
            }

            if (selectedIds.Contains(domainEvent.PdfDocumentId))
            {
                _logger.LogInformation("PDF {PdfId} already in agent {AgentId} — skipping", domainEvent.PdfDocumentId, agent.Id);
                continue;
            }

            selectedIds.Add(domainEvent.PdfDocumentId);
            config.SelectedDocumentIdsJson = JsonSerializer.Serialize(selectedIds);

            _logger.LogInformation(
                "Auto-added PDF {PdfId} to agent {AgentId} for game {GameId}. Total docs: {Count}",
                domainEvent.PdfDocumentId, agent.Id, domainEvent.GameId, selectedIds.Count);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

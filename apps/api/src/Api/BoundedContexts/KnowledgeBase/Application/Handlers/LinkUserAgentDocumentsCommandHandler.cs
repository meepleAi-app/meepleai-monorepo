using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for LinkUserAgentDocumentsCommand.
/// Issue #4941: Auto-link indexed PDF documents when creating game agent.
/// </summary>
/// <remarks>
/// Fail-safe design: missing entities (no documents, no agent definition) result in silent no-op.
/// No admin session required — this operates in user context.
/// </remarks>
internal sealed class LinkUserAgentDocumentsCommandHandler : IRequestHandler<LinkUserAgentDocumentsCommand, Unit>
{
    private readonly IVectorDocumentRepository _vectorDocumentRepository;
    private readonly IAgentDefinitionRepository _agentDefinitionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LinkUserAgentDocumentsCommandHandler> _logger;

    public LinkUserAgentDocumentsCommandHandler(
        IVectorDocumentRepository vectorDocumentRepository,
        IAgentDefinitionRepository agentDefinitionRepository,
        IUnitOfWork unitOfWork,
        ILogger<LinkUserAgentDocumentsCommandHandler> logger)
    {
        _vectorDocumentRepository = vectorDocumentRepository ?? throw new ArgumentNullException(nameof(vectorDocumentRepository));
        _agentDefinitionRepository = agentDefinitionRepository ?? throw new ArgumentNullException(nameof(agentDefinitionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(LinkUserAgentDocumentsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Get all indexed documents for this game
        var documents = await _vectorDocumentRepository
            .GetByGameIdAsync(request.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (documents.Count == 0)
        {
            _logger.LogDebug(
                "No indexed documents found for game {GameId}. Skipping auto-link.",
                request.GameId);
            return Unit.Value;
        }

        // Load the agent definition
        var agentDefinition = await _agentDefinitionRepository
            .GetByIdAsync(request.AgentDefinitionId, cancellationToken)
            .ConfigureAwait(false);

        if (agentDefinition is null)
        {
            _logger.LogWarning(
                "AgentDefinition {AgentDefinitionId} not found. Skipping auto-link for game {GameId}.",
                request.AgentDefinitionId,
                request.GameId);
            return Unit.Value;
        }

        // Merge new document IDs with any already linked (additive, never overwrite admin-linked cards)
        var documentIds = documents.Select(d => d.Id).ToHashSet();
        documentIds.UnionWith(agentDefinition.KbCardIds);
        agentDefinition.UpdateKbCardIds(documentIds);

        await _agentDefinitionRepository.UpdateAsync(agentDefinition, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Auto-linked {Count} document(s) to AgentDefinition {AgentDefinitionId} for game {GameId}.",
            documentIds.Count,
            request.AgentDefinitionId,
            request.GameId);

        return Unit.Value;
    }
}

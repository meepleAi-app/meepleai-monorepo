using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.IntegrationEvents;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Handles VectorDocumentReadyIntegrationEvent to auto-create an AgentDefinition
/// linked to the user's PrivateGame when PDF processing completes.
///
/// Orchestrates: PrivateGame (UserLibrary) ↔ AgentDefinition (KnowledgeBase).
/// Placed in GameManagement because it coordinates cross-context linking
/// for the Game Night Improvvisata feature.
///
/// Business Rules:
/// - Only acts on PrivateGames owned by the user who uploaded the PDF.
/// - If the game is a SharedGame (not found as PrivateGame) → silently skip.
/// - If the PrivateGame already has an agent → update KbCardIds, do not create a new agent.
/// - Tier quota check guards agent creation; if exceeded → log warning and skip.
/// - All errors are caught and logged — this is a background handler and must not throw.
/// </summary>
internal sealed class AutoCreateAgentOnPdfReadyHandler
    : INotificationHandler<VectorDocumentReadyIntegrationEvent>
{
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly IAgentDefinitionRepository _agentDefinitionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly ILogger<AutoCreateAgentOnPdfReadyHandler> _logger;

    public AutoCreateAgentOnPdfReadyHandler(
        IPrivateGameRepository privateGameRepository,
        IAgentDefinitionRepository agentDefinitionRepository,
        IUnitOfWork unitOfWork,
        ITierEnforcementService tierEnforcementService,
        ILogger<AutoCreateAgentOnPdfReadyHandler> logger)
    {
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _agentDefinitionRepository = agentDefinitionRepository ?? throw new ArgumentNullException(nameof(agentDefinitionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        VectorDocumentReadyIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            await HandleInternalAsync(notification, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation — do not swallow it.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AutoCreateAgentOnPdfReadyHandler failed for DocumentId={DocumentId}, GameId={GameId}, UserId={UserId}. " +
                "Agent auto-creation skipped.",
                notification.DocumentId,
                notification.GameId,
                notification.UploadedByUserId);
        }
    }

    private async Task HandleInternalAsync(
        VectorDocumentReadyIntegrationEvent evt,
        CancellationToken ct)
    {
        var userId = evt.UploadedByUserId;
        var gameId = evt.GameId;
        var documentId = evt.DocumentId;

        _logger.LogInformation(
            "AutoCreateAgentOnPdfReadyHandler: Processing DocumentId={DocumentId}, GameId={GameId}, UserId={UserId}",
            documentId, gameId, userId);

        // Step 1: Check if GameId belongs to a PrivateGame owned by this user.
        // If not found it's a SharedGame upload → skip silently.
        var privateGame = await _privateGameRepository
            .GetByIdAsync(gameId, ct)
            .ConfigureAwait(false);

        if (privateGame is null || privateGame.OwnerId != userId)
        {
            _logger.LogDebug(
                "AutoCreateAgentOnPdfReadyHandler: GameId={GameId} is not a PrivateGame owned by UserId={UserId}. Skipping.",
                gameId, userId);
            return;
        }

        // Step 2: If the PrivateGame already has an agent, just update KbCardIds.
        if (privateGame.AgentDefinitionId.HasValue)
        {
            await UpdateExistingAgentKbCardsAsync(
                privateGame.AgentDefinitionId.Value, documentId, ct)
                .ConfigureAwait(false);
            return;
        }

        // Step 3: Tier quota check.
        var canCreate = await _tierEnforcementService
            .CanPerformAsync(userId, TierAction.CreateAgent, ct)
            .ConfigureAwait(false);

        if (!canCreate)
        {
            _logger.LogWarning(
                "AutoCreateAgentOnPdfReadyHandler: Tier quota exceeded for UserId={UserId}. " +
                "Agent auto-creation skipped for GameId={GameId}.",
                userId, gameId);
            return;
        }

        // Step 4: Create a new AgentDefinition.
        var agentName = $"Agent - {privateGame.Title}";
        // AgentDefinition.Create validates name <= 100 chars — truncate if needed.
        if (agentName.Length > 100)
            agentName = agentName[..100];

        var agentDescription = $"AI assistant for {privateGame.Title}";
        if (agentDescription.Length > 1000)
            agentDescription = agentDescription[..1000];

        var agent = AgentDefinition.Create(
            name: agentName,
            description: agentDescription,
            type: AgentType.RagAgent,
            config: AgentDefinitionConfig.Default(),
            strategy: AgentStrategy.HybridSearch());

        // Step 5: Link the new document as the first KbCard.
        agent.UpdateKbCardIds([documentId]);

        // Step 6: Persist agent.
        await _agentDefinitionRepository
            .AddAsync(agent, ct)
            .ConfigureAwait(false);

        // Step 7: Link agent to PrivateGame.
        privateGame.LinkAgent(agent.Id);

        await _privateGameRepository
            .UpdateAsync(privateGame, ct)
            .ConfigureAwait(false);

        // Step 8: Save all changes atomically.
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        // Step 9: Record tier usage.
        await _tierEnforcementService
            .RecordUsageAsync(userId, TierAction.CreateAgent, ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "AutoCreateAgentOnPdfReadyHandler: Created AgentDefinition {AgentId} and linked to PrivateGame {GameId} for UserId={UserId}.",
            agent.Id, gameId, userId);
    }

    private async Task UpdateExistingAgentKbCardsAsync(
        Guid agentDefinitionId,
        Guid documentId,
        CancellationToken ct)
    {
        var agent = await _agentDefinitionRepository
            .GetByIdAsync(agentDefinitionId, ct)
            .ConfigureAwait(false);

        if (agent is null)
        {
            _logger.LogWarning(
                "AutoCreateAgentOnPdfReadyHandler: AgentDefinition {AgentId} not found. " +
                "Cannot update KbCardIds with DocumentId={DocumentId}.",
                agentDefinitionId, documentId);
            return;
        }

        // Merge: add documentId if not already present.
        var existingIds = agent.KbCardIds.ToList();
        if (existingIds.Contains(documentId))
        {
            _logger.LogDebug(
                "AutoCreateAgentOnPdfReadyHandler: DocumentId={DocumentId} already in KbCardIds for AgentId={AgentId}. Skipping.",
                documentId, agentDefinitionId);
            return;
        }

        existingIds.Add(documentId);
        agent.UpdateKbCardIds(existingIds);

        await _agentDefinitionRepository.UpdateAsync(agent, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "AutoCreateAgentOnPdfReadyHandler: Updated KbCardIds for existing AgentDefinition {AgentId} with DocumentId={DocumentId}.",
            agentDefinitionId, documentId);
    }
}

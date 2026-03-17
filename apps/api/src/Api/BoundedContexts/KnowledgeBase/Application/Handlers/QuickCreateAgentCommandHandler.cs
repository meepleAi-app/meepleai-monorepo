using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for quick-creating an agent + chat thread after ownership declaration.
/// Follows the same pattern as CreateAgentWithSetupCommandHandler but simplified:
/// no slot validation, no collection add, always Tutor type.
/// </summary>
internal sealed class QuickCreateAgentCommandHandler
    : IRequestHandler<QuickCreateAgentCommand, QuickCreateAgentResult>
{
    private const int MaxAutoSuffixAttempts = 5;

    private readonly IAgentRepository _agentRepository;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IRagAccessService _ragAccessService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<QuickCreateAgentCommandHandler> _logger;

    public QuickCreateAgentCommandHandler(
        IAgentRepository agentRepository,
        IChatThreadRepository chatThreadRepository,
        IRagAccessService ragAccessService,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext db,
        IMediator mediator,
        ILogger<QuickCreateAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QuickCreateAgentResult> Handle(
        QuickCreateAgentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Step 1: Check RAG access
        var role = Enum.TryParse<UserRole>(request.UserRole, ignoreCase: true, out var parsedRole)
            ? parsedRole
            : UserRole.User;

        var canAccess = await _ragAccessService.CanAccessRagAsync(
            request.UserId, request.GameId, role, cancellationToken).ConfigureAwait(false);

        if (!canAccess)
        {
            throw new ForbiddenException("You do not have RAG access to this game. Declare ownership first.");
        }

        // Step 2: Get indexed KB cards
        var indexedDocIds = await _db.VectorDocuments
            .Where(vd => (vd.SharedGameId == request.GameId || vd.GameId == request.GameId)
                         && vd.IndexingStatus == "completed")
            .Select(vd => vd.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Step 3: Get game name for the agent
        var gameName = await GetGameNameAsync(request.GameId, request.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        // Step 4: Create agent within a transaction
        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var agentType = AgentType.RagAgent;
            var strategy = AgentStrategy.SingleModel();
            var agentName = await ResolveUniqueName(request.UserId, $"Tutor {gameName}", cancellationToken)
                .ConfigureAwait(false);

            var agent = new Agent(
                id: Guid.NewGuid(),
                name: agentName,
                type: agentType,
                strategy: strategy,
                isActive: true,
                gameId: request.GameId,
                createdByUserId: request.UserId);

            await _agentRepository.AddAsync(agent, cancellationToken).ConfigureAwait(false);

            // Create default AgentConfiguration with auto-selected indexed documents
            var documentIdsJson = indexedDocIds.Count > 0
                ? JsonSerializer.Serialize(indexedDocIds)
                : "[]";

            var defaultConfig = new AgentConfigurationEntity
            {
                Id = Guid.NewGuid(),
                AgentId = agent.Id,
                LlmProvider = AgentDefaults.DefaultLlmProvider,
                LlmModel = AgentDefaults.DefaultModel,
                AgentMode = 0, // Chat
                SelectedDocumentIdsJson = documentIdsJson,
                Temperature = AgentDefaults.DefaultTemperature,
                MaxTokens = AgentDefaults.DefaultMaxTokens,
                IsCurrent = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.UserId
            };

            _db.Set<AgentConfigurationEntity>().Add(defaultConfig);

            // Step 5: Create initial chat thread
            var chatThread = new ChatThread(
                id: Guid.NewGuid(),
                userId: request.UserId,
                gameId: request.GameId,
                title: null,
                agentId: agent.Id,
                agentType: agentType.Value);

            await _chatThreadRepository.AddAsync(chatThread, cancellationToken).ConfigureAwait(false);

            // Step 6: Commit
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Quick-created agent {AgentId} ({AgentType}) + Thread {ThreadId} for user {UserId}, game {GameId} with {KbCardCount} KB cards",
                agent.Id, agentType.Value, chatThread.Id, request.UserId, request.GameId, indexedDocIds.Count);

            // Post-creation: link to SharedGame if provided
            if (request.SharedGameId.HasValue)
            {
                await _mediator.Send(
                    new LinkAgentToSharedGameCommand(request.SharedGameId.Value, agent.Id),
                    cancellationToken).ConfigureAwait(false);
            }

            return new QuickCreateAgentResult(
                AgentId: agent.Id,
                ChatThreadId: chatThread.Id,
                AgentName: agentName,
                KbCardCount: indexedDocIds.Count
            );
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<string> GetGameNameAsync(Guid gameId, Guid? sharedGameId, CancellationToken cancellationToken)
    {
        // Try SharedGame first
        var lookupId = sharedGameId ?? gameId;
        var title = await _db.SharedGames
            .AsNoTracking()
            .Where(sg => sg.Id == lookupId && !sg.IsDeleted)
            .Select(sg => sg.Title)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(title))
            return title;

        // Fallback: use the gameId short form
        return gameId.ToString()[..8];
    }

    private async Task<string> ResolveUniqueName(Guid userId, string baseName, CancellationToken cancellationToken)
    {
        if (!await _agentRepository.ExistsByNameForUserAsync(userId, baseName, cancellationToken)
                .ConfigureAwait(false))
            return baseName;

        for (var i = 1; i <= MaxAutoSuffixAttempts; i++)
        {
            var name = $"{baseName}-{i}";
            if (!await _agentRepository.ExistsByNameForUserAsync(userId, name, cancellationToken)
                    .ConfigureAwait(false))
                return name;
        }

        throw new ConflictException($"Could not generate unique name for agent based on '{baseName}'");
    }
}

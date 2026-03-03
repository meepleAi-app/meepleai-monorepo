using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Orchestrated handler: validates slots → optionally adds game to library →
/// creates agent → creates chat thread. All within a single transaction.
/// Issue #4772: Agent Creation Orchestration Flow.
/// </summary>
internal sealed class CreateAgentWithSetupCommandHandler
    : IRequestHandler<CreateAgentWithSetupCommand, AgentCreationResultDto>
{
    private const int MaxAutoSuffixAttempts = 5;

    private readonly IAgentRepository _agentRepository;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<CreateAgentWithSetupCommandHandler> _logger;

    public CreateAgentWithSetupCommandHandler(
        IAgentRepository agentRepository,
        IChatThreadRepository chatThreadRepository,
        IUserLibraryRepository libraryRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext db,
        ILogger<CreateAgentWithSetupCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentCreationResultDto> Handle(
        CreateAgentWithSetupCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Step 1: Validate agent slot availability
        var userAgents = await _agentRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);
        var activeAgents = userAgents.Where(a => a.IsActive).ToList();
        var maxAgents = AgentTierLimits.GetMaxAgents(request.UserTier, request.UserRole);

        if (activeAgents.Count >= maxAgents)
        {
            throw new ConflictException(
                $"Agent limit reached ({maxAgents}). Upgrade your tier for more agents.");
        }

        var slotUsed = activeAgents.Count + 1;
        var gameAddedToCollection = false;

        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Step 2: Optionally add game to user's library
            if (request.AddToCollection)
            {
                var alreadyInLibrary = await _libraryRepository
                    .IsGameInLibraryAsync(request.UserId, request.GameId, cancellationToken)
                    .ConfigureAwait(false);

                if (!alreadyInLibrary)
                {
                    var libraryEntry = new UserLibraryEntry(
                        Guid.NewGuid(), request.UserId, request.GameId);
                    await _libraryRepository.AddAsync(libraryEntry, cancellationToken)
                        .ConfigureAwait(false);
                    gameAddedToCollection = true;

                    _logger.LogInformation(
                        "Auto-added game {GameId} to library for user {UserId}",
                        request.GameId, request.UserId);
                }
            }

            // Step 3: Create agent
            var agentType = AgentType.Parse(request.AgentType);
            var strategy = ResolveStrategy(request);
            var agentName = await ResolveUniqueName(request, agentType, cancellationToken)
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

            // Step 3b: Create default AgentConfiguration with auto-selected indexed documents
            var indexedDocIds = await _db.VectorDocuments
                .Where(vd => vd.GameId == request.GameId && vd.IndexingStatus == "completed")
                .Select(vd => vd.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

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

            _logger.LogInformation(
                "Created default config for agent {AgentId} with {DocCount} indexed documents",
                agent.Id, indexedDocIds.Count);

            // Step 4: Create initial chat thread
            var chatThread = new ChatThread(
                id: Guid.NewGuid(),
                userId: request.UserId,
                gameId: request.GameId,
                title: null,
                agentId: agent.Id,
                agentType: agentType.Value);

            await _chatThreadRepository.AddAsync(chatThread, cancellationToken).ConfigureAwait(false);

            // Step 5: Commit transaction
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Orchestrated agent creation: Agent {AgentId} ({AgentType}) + Thread {ThreadId} for user {UserId}, game {GameId}",
                agent.Id, agentType.Value, chatThread.Id, request.UserId, request.GameId);

            return new AgentCreationResultDto(
                AgentId: agent.Id,
                AgentName: agentName,
                ThreadId: chatThread.Id,
                SlotUsed: slotUsed,
                GameAddedToCollection: gameAddedToCollection);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<string> ResolveUniqueName(
        CreateAgentWithSetupCommand request,
        AgentType agentType,
        CancellationToken cancellationToken)
    {
        var name = !string.IsNullOrWhiteSpace(request.AgentName)
            ? request.AgentName.Trim()
            : $"{agentType.Value}-{request.GameId.ToString()[..8]}";

        if (!await _agentRepository.ExistsByNameForUserAsync(request.UserId, name, cancellationToken)
                .ConfigureAwait(false))
            return name;

        var baseName = name;
        for (var i = 1; i <= MaxAutoSuffixAttempts; i++)
        {
            name = $"{baseName}-{i}";
            if (!await _agentRepository.ExistsByNameForUserAsync(request.UserId, name, cancellationToken)
                    .ConfigureAwait(false))
                return name;
        }

        throw new ConflictException($"Could not generate unique name for agent based on '{baseName}'");
    }

    private static AgentStrategy ResolveStrategy(CreateAgentWithSetupCommand request)
    {
        var isAdmin = AgentTierLimits.IsAdminOrEditor(request.UserRole);
        var tierLevel = GetTierLevel(request.UserTier);

        if ((isAdmin || tierLevel >= 2) && !string.IsNullOrWhiteSpace(request.StrategyName))
        {
            var parameters = request.StrategyParameters ?? new Dictionary<string, object>(StringComparer.Ordinal);
            return AgentStrategy.Custom(request.StrategyName, parameters);
        }

        if (tierLevel >= 1 && !string.IsNullOrWhiteSpace(request.StrategyName))
        {
            return AgentStrategy.Custom(request.StrategyName, new Dictionary<string, object>(StringComparer.Ordinal));
        }

        return AgentStrategy.SingleModel();
    }

    private static int GetTierLevel(string tier)
    {
        return tier?.ToLowerInvariant() switch
        {
            "free" => 0,
            "normal" => 1,
            "premium" or "pro" => 2,
            "enterprise" => 3,
            _ => 0
        };
    }
}

using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for CreateGameAgentCommand.
/// Issue #5: Backend Agent Creation with Custom Config API.
/// </summary>
/// <remarks>
/// Follows existing UserLibrary domain pattern where agent configuration is stored
/// on UserLibraryEntry rather than creating separate Agent entities in KnowledgeBase.
/// This aligns with the architecture where game-specific agent config is user-scoped.
/// </remarks>
internal class CreateGameAgentCommandHandler : IRequestHandler<CreateGameAgentCommand, CreateGameAgentResult>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly IAgentDefinitionRepository _definitionRepository;
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IVectorDocumentRepository _vectorDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateGameAgentCommandHandler> _logger;

    public CreateGameAgentCommandHandler(
        ISharedGameRepository gameRepository,
        IAgentDefinitionRepository definitionRepository,
        IUserLibraryRepository libraryRepository,
        IVectorDocumentRepository vectorDocumentRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateGameAgentCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _definitionRepository = definitionRepository;
        _libraryRepository = libraryRepository;
        _vectorDocumentRepository = vectorDocumentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreateGameAgentResult> Handle(
        CreateGameAgentCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Verify game exists in shared catalog
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken).ConfigureAwait(false)
                   ?? throw new NotFoundException($"Game with ID {request.GameId} not found");

        // 1.5. KB gate: Verify indexed knowledge base exists for this game
        var indexingInfo = await _vectorDocumentRepository
            .GetIndexingInfoByGameIdAsync(request.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (indexingInfo == null || indexingInfo.Status != VectorDocumentIndexingStatus.Completed)
        {
            var statusMessage = indexingInfo == null
                ? "no knowledge base found"
                : $"knowledge base status is '{indexingInfo.Status}'";
            throw new ConflictException(
                $"Cannot create agent for game '{game.Title}': {statusMessage}. Upload and process a PDF first.");
        }

        // 2. Verify definition exists and is active
        var definition = await _definitionRepository.GetByIdAsync(request.AgentDefinitionId, cancellationToken).ConfigureAwait(false)
                         ?? throw new NotFoundException($"Agent definition with ID {request.AgentDefinitionId} not found");

        if (!definition.IsActive)
        {
            throw new ConflictException($"Agent definition '{definition.Name}' is not active");
        }

        // 3. Parse and validate RAG strategy
        if (!Domain.Enums.RagStrategyExtensions.TryParse(request.StrategyName, out var strategy))
        {
            throw new ConflictException($"Invalid RAG strategy: '{request.StrategyName}'");
        }

        // 3.5. Check if agent already exists for this game before quota validation.
        // This ensures a user at their limit who re-submits for an existing agent gets the
        // correct "already exists" error rather than a misleading quota error. (Issue #4944)
        var entry = await _libraryRepository.GetByUserAndGameAsync(request.UserId, request.GameId, cancellationToken).ConfigureAwait(false);

        if (entry != null && entry.HasCustomAgent())
        {
            throw new ConflictException($"Agent configuration already exists for game '{game.Title}'");
        }

        // 4. Enforce agent creation quota per user tier (Issue #4944)
        var agentCount = await _libraryRepository.GetAgentConfigCountAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        var maxAgents = AgentTierLimits.GetMaxAgents(request.UserTier, request.UserRole);
        if (agentCount >= maxAgents)
        {
            throw new ConflictException(
                $"Agent limit reached ({maxAgents}). Upgrade your tier for more agents.");
        }

        // 5. Get or create library entry for this game
        if (entry == null)
        {
            // Game not in user's library - add it first
            entry = new UserLibraryEntry(
                id: Guid.NewGuid(),
                userId: request.UserId,
                gameId: request.GameId
            );
            await _libraryRepository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        // 6. Create agent configuration (using definition config + strategy)
        var agentConfig = AgentConfiguration.Create(
            llmModel: definition.Config.Model,
            temperature: (double)definition.Config.Temperature,
            maxTokens: definition.Config.MaxTokens,
            personality: $"{definition.Name}: {definition.Description}",
            detailLevel: strategy.ToString(),
            personalNotes: request.StrategyParameters
        );

        // 7. Configure agent through domain method
        entry.ConfigureAgent(agentConfig);

        // 8. Persist changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent configuration created for game {GameId} by user {UserId}, definition: {DefinitionName}, strategy: {Strategy}",
            game.Id,
            request.UserId,
            definition.Name,
            strategy.GetDisplayName()
        );

        // 9. Return result
        return new CreateGameAgentResult
        {
            LibraryEntryId = entry.Id,
            GameId = game.Id,
            Status = "ready",  // Config is immediately ready for use
            Definition = new AgentDefinitionInfo
            {
                Id = definition.Id,
                Name = definition.Name
            },
            Strategy = new AgentStrategyInfo
            {
                Name = strategy.GetDisplayName(),
                Parameters = request.StrategyParameters
            }
        };
    }
}

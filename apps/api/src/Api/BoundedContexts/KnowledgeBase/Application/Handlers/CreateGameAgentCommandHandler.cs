using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

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
    private readonly IAgentTypologyRepository _typologyRepository;
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateGameAgentCommandHandler> _logger;

    public CreateGameAgentCommandHandler(
        ISharedGameRepository gameRepository,
        IAgentTypologyRepository typologyRepository,
        IUserLibraryRepository libraryRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateGameAgentCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _typologyRepository = typologyRepository;
        _libraryRepository = libraryRepository;
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

        // 2. Verify typology exists and is approved
        var typology = await _typologyRepository.GetByIdAsync(request.TypologyId, cancellationToken).ConfigureAwait(false)
                       ?? throw new NotFoundException($"Agent typology with ID {request.TypologyId} not found");

        if (typology.Status != TypologyStatus.Approved)
        {
            throw new ConflictException($"Typology '{typology.Name}' is not approved (status: {typology.Status})");
        }

        // 3. Parse and validate RAG strategy
        if (!Domain.Enums.RagStrategyExtensions.TryParse(request.StrategyName, out var strategy))
        {
            throw new ConflictException($"Invalid RAG strategy: '{request.StrategyName}'");
        }

        // 4. Get or create library entry for this game
        var entry = await _libraryRepository.GetByUserAndGameAsync(request.UserId, request.GameId, cancellationToken).ConfigureAwait(false);

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
        else if (entry.HasCustomAgent())
        {
            // Agent config already exists - conflict
            throw new ConflictException($"Agent configuration already exists for game '{game.Title}'");
        }

        // 5. Create agent configuration (using typology defaults + strategy)
        var agentConfig = AgentConfiguration.Create(
            llmModel: typology.DefaultStrategy.GetParameter<string>("Model", "gpt-4"),
            temperature: typology.DefaultStrategy.GetParameter<double>("Temperature", 0.7),
            maxTokens: typology.DefaultStrategy.GetParameter<int>("MaxTokens", 2000),
            personality: $"{typology.Name}: {typology.Description}",
            detailLevel: strategy.ToString(),
            personalNotes: request.StrategyParameters
        );

        // 6. Configure agent through domain method
        entry.ConfigureAgent(agentConfig);

        // 7. Persist changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent configuration created for game {GameId} by user {UserId}, typology: {TypologyName}, strategy: {Strategy}",
            game.Id,
            request.UserId,
            typology.Name,
            strategy.GetDisplayName()
        );

        // 8. Return result
        return new CreateGameAgentResult
        {
            LibraryEntryId = entry.Id,
            GameId = game.Id,
            Status = "ready",  // Config is immediately ready for use
            Typology = new AgentTypologyInfo
            {
                Id = typology.Id,
                Name = typology.Name
            },
            Strategy = new AgentStrategyInfo
            {
                Name = strategy.GetDisplayName(),
                Parameters = request.StrategyParameters
            }
        };
    }
}

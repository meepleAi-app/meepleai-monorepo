using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for retrieving custom AI agent configuration for a game in user's library.
/// Returns null if no custom configuration exists (user should use defaults).
/// </summary>
internal class GetGameAgentConfigQueryHandler : IQueryHandler<GetGameAgentConfigQuery, AgentConfigDto?>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ILogger<GetGameAgentConfigQueryHandler> _logger;

    public GetGameAgentConfigQueryHandler(
        IUserLibraryRepository libraryRepository,
        ILogger<GetGameAgentConfigQueryHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentConfigDto?> Handle(GetGameAgentConfigQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entry = await _libraryRepository.GetByUserAndGameAsync(query.UserId, query.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            _logger.LogDebug("Game {GameId} not found in library for user {UserId}", query.GameId, query.UserId);
            return null;
        }

        if (entry.CustomAgentConfig is null)
        {
            _logger.LogDebug("No custom agent config for game {GameId} in library for user {UserId}", query.GameId, query.UserId);
            return null;
        }

        _logger.LogDebug("Retrieved custom agent config for game {GameId} in library for user {UserId}", query.GameId, query.UserId);

        return new AgentConfigDto(
            LlmModel: entry.CustomAgentConfig.LlmModel,
            Temperature: entry.CustomAgentConfig.Temperature,
            MaxTokens: entry.CustomAgentConfig.MaxTokens,
            Personality: entry.CustomAgentConfig.Personality,
            DetailLevel: entry.CustomAgentConfig.DetailLevel,
            PersonalNotes: entry.CustomAgentConfig.PersonalNotes
        );
    }
}

using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGameKbSettings;

/// <summary>
/// Handles GetGameKbSettingsQuery.
/// Reads per-game KB settings overrides from SystemConfiguration.
/// KB-10: Admin per-game KB settings backend.
/// </summary>
internal sealed class GetGameKbSettingsQueryHandler
    : IQueryHandler<GetGameKbSettingsQuery, GameKbSettingsDto>
{
    private readonly IConfigurationRepository _configRepository;
    private readonly ILogger<GetGameKbSettingsQueryHandler> _logger;

    public GetGameKbSettingsQueryHandler(
        IConfigurationRepository configRepository,
        ILogger<GetGameKbSettingsQueryHandler> logger)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameKbSettingsDto> Handle(
        GetGameKbSettingsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var configKey = $"KB:Game:{query.GameId}:Settings";

        _logger.LogDebug("Fetching KB settings for game {GameId} from key {Key}", query.GameId, configKey);

        var existing = await _configRepository
            .GetByKeyAsync(configKey, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return new GameKbSettingsDto(query.GameId, null, null, null, null);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<GameKbSettingsPayload>(existing.Value);
            if (payload is null)
            {
                return new GameKbSettingsDto(query.GameId, null, null, null, null);
            }

            return new GameKbSettingsDto(
                query.GameId,
                payload.MaxChunks,
                payload.ChunkSize,
                payload.CacheEnabled,
                payload.Language);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to deserialize KB settings payload for game {GameId}", query.GameId);
            return new GameKbSettingsDto(query.GameId, null, null, null, null);
        }
    }

    private sealed record GameKbSettingsPayload(
        int? MaxChunks,
        int? ChunkSize,
        bool? CacheEnabled,
        string? Language);
}

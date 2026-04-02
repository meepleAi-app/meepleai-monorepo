using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGameKbSettings;

/// <summary>
/// Query to retrieve KB settings overrides for a specific game (admin).
/// KB-10: Admin per-game KB settings backend.
/// </summary>
internal sealed record GetGameKbSettingsQuery(Guid GameId)
    : IQuery<GameKbSettingsDto>;

/// <summary>
/// DTO containing KB settings overrides for a game.
/// All fields are nullable — null means "use system default".
/// </summary>
internal sealed record GameKbSettingsDto(
    Guid GameId,
    int? MaxChunksOverride,
    int? ChunkSizeOverride,
    bool? CacheEnabledOverride,
    string? LanguageOverride);

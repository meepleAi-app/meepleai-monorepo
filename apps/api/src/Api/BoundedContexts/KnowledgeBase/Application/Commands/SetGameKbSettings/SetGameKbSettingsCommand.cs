using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SetGameKbSettings;

/// <summary>
/// Command to upsert per-game KB settings overrides in SystemConfiguration.
/// KB-10: Admin per-game KB settings backend.
/// </summary>
internal sealed record SetGameKbSettingsCommand(
    Guid GameId,
    int? MaxChunks,
    int? ChunkSize,
    bool? CacheEnabled,
    string? Language) : IRequest;

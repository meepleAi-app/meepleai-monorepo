using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.RecordGameEvent;

/// <summary>
/// Command to record a game analytics event for trending calculation.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
internal sealed record RecordGameEventCommand(
    Guid GameId,
    GameEventType EventType,
    Guid? UserId = null
) : ICommand;

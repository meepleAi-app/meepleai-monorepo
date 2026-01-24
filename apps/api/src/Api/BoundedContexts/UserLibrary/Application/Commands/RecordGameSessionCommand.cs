using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to record a new game session with optional competitive outcome.
/// Automatically updates GameStats and emits GameSessionRecordedEvent.
/// </summary>
internal record RecordGameSessionCommand(
    Guid UserId,
    Guid GameId,
    DateTime PlayedAt,
    int DurationMinutes,
    bool? DidWin = null,
    string? Players = null,
    string? Notes = null
) : ICommand<Guid>; // Returns session ID

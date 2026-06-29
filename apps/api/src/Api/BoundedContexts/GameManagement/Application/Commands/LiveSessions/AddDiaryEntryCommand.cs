using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to append an immutable diary entry to a live game session.
/// Returns the new diary entry's id.
/// Issue #2570 SP3 T3.
/// </summary>
internal record AddDiaryEntryCommand(
    Guid SessionId,
    Guid AuthorId,
    string Text
) : ICommand<Guid>;

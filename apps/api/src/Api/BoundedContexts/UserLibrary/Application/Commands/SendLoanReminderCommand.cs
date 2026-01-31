using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to send loan reminder notification to game owner.
/// Only works for games in InPrestito state.
/// </summary>
internal record SendLoanReminderCommand(
    Guid UserId,
    Guid GameId,
    string? CustomMessage = null
) : ICommand;

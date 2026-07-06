using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Organiser archives (or restores) a finalised game night — Issue #2702.
/// Archiving requires a Completed night; restoring is always allowed.
/// </summary>
internal record SetGameNightArchivedCommand(Guid GameNightId, Guid UserId, bool Archived) : ICommand;

using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to remove a private PDF from a library entry.
/// Issue #3651: Removes the association and triggers vector cleanup via PrivatePdfRemovedEvent.
/// </summary>
/// <param name="UserId">The user who owns the library entry</param>
/// <param name="EntryId">The library entry ID to remove PDF from</param>
internal record RemovePrivatePdfCommand(
    Guid UserId,
    Guid EntryId
) : ICommand<UserLibraryEntryDto>;

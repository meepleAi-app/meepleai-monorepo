using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to create a new library share link.
/// </summary>
internal record CreateLibraryShareLinkCommand(
    Guid UserId,
    string PrivacyLevel,
    bool IncludeNotes = false,
    DateTime? ExpiresAt = null
) : ICommand<LibraryShareLinkDto>;

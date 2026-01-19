using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to update library share link settings.
/// </summary>
internal record UpdateLibraryShareLinkCommand(
    Guid UserId,
    string ShareToken,
    string? PrivacyLevel = null,
    bool? IncludeNotes = null,
    DateTime? ExpiresAt = null
) : ICommand<LibraryShareLinkDto>;

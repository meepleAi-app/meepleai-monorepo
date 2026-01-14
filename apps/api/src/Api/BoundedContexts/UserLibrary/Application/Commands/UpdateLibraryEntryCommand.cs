using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to update a library entry (notes, favorite status).
/// </summary>
internal record UpdateLibraryEntryCommand(
    Guid UserId,
    Guid GameId,
    string? Notes = null,
    bool? IsFavorite = null
) : ICommand<UserLibraryEntryDto>;

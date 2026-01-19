using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get user's active library share link.
/// </summary>
internal record GetLibraryShareLinkQuery(
    Guid UserId
) : IQuery<LibraryShareLinkDto?>;

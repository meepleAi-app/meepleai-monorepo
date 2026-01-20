using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get a shared library by its share token (public access).
/// </summary>
internal record GetSharedLibraryQuery(
    string ShareToken
) : IQuery<SharedLibraryDto?>;

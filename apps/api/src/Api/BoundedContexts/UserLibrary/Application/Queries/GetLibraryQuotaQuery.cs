using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get user's library quota information.
/// </summary>
internal record GetLibraryQuotaQuery(Guid UserId) : IQuery<LibraryQuotaDto>;

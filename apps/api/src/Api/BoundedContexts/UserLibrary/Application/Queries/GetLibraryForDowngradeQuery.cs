using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

internal record GetLibraryForDowngradeQuery(Guid UserId, int NewQuota) : IQuery<LibraryForDowngradeDto>;

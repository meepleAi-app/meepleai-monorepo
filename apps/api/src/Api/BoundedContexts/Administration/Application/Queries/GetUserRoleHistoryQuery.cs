using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get user role change history (Issue #2890).
/// </summary>
internal record GetUserRoleHistoryQuery(
    Guid UserId
) : IQuery<List<RoleChangeHistoryDto>>;

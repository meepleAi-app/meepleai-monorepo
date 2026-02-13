using MediatR;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Auth = Api.BoundedContexts.Authentication.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Application.Queries.GetUserPermissions;

/// <summary>
/// Query to get user's permission context
/// Epic #4068 - Issue #4177
/// </summary>
public sealed record GetUserPermissionsQuery(Guid UserId) : IRequest<GetUserPermissionsResponse>;

public sealed record GetUserPermissionsResponse(
    string Tier,
    string Role,
    UserAccountStatus Status,
    CollectionLimits Limits,
    List<string> AccessibleFeatures);

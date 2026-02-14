using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.CheckPermission;

public sealed record CheckPermissionQuery(
    Guid UserId,
    string FeatureName,
    string? ResourceState = null) : IRequest<CheckPermissionResponse>;

public sealed record CheckPermissionResponse(
    bool HasAccess,
    string Reason,
    PermissionDetails Details);

public sealed record PermissionDetails(
    string UserTier,
    string UserRole,
    string UserStatus,
    RequiredPermissions Required,
    string Logic);

public sealed record RequiredPermissions(
    string? Tier,
    string? Role,
    List<string>? States);

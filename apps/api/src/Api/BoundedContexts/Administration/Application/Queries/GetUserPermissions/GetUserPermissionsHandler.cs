using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.Administration.Application.Queries.GetUserPermissions;

public sealed class GetUserPermissionsHandler
    : IRequestHandler<GetUserPermissionsQuery, GetUserPermissionsResponse>
{
    private readonly MeepleAiDbContext _context;
    private readonly PermissionRegistry _permissionRegistry;

    public GetUserPermissionsHandler(
        MeepleAiDbContext context,
        PermissionRegistry permissionRegistry)
    {
        _context = context;
        _permissionRegistry = permissionRegistry;
    }

    public async Task<GetUserPermissionsResponse> Handle(
        GetUserPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Where(u => u.Id == request.UserId)
            .Select(u => new { u.Tier, u.Role, u.Status })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("User", request.UserId.ToString());

        // Parse string values to domain value objects
        var userTier = UserTier.Parse(user.Tier);
        var userRole = Role.Parse(user.Role);
        var userStatus = Enum.Parse<UserAccountStatus>(user.Status);

        var permissionContext = new PermissionContext(
            userTier,
            userRole,
            userStatus);

        var accessibleFeatures = _permissionRegistry.GetAccessibleFeatures(permissionContext);

        return new GetUserPermissionsResponse(
            userTier.Value,
            userRole.Value,
            userStatus,
            userTier.GetLimits(),
            accessibleFeatures.ToList());
    }
}

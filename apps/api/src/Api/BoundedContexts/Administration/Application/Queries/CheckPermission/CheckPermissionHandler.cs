using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.BoundedContexts.Administration.Application.Queries.CheckPermission;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.Administration.Application.Queries.CheckPermission;

public sealed class CheckPermissionHandler
    : IRequestHandler<CheckPermissionQuery, CheckPermissionResponse>
{
    private readonly MeepleAiDbContext _context;
    private readonly PermissionRegistry _permissionRegistry;

    public CheckPermissionHandler(
        MeepleAiDbContext context,
        PermissionRegistry permissionRegistry)
    {
        _context = context;
        _permissionRegistry = permissionRegistry;
    }

    public async Task<CheckPermissionResponse> Handle(
        CheckPermissionQuery request,
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
            userStatus,
            request.ResourceState);

        var result = _permissionRegistry.CheckAccess(request.FeatureName, permissionContext);
        var permission = _permissionRegistry.GetPermission(request.FeatureName);

        return new CheckPermissionResponse(
            result.HasAccess,
            result.Reason,
            new PermissionDetails(
                userTier.Value,
                userRole.Value,
                userStatus.ToString(),
                new RequiredPermissions(
                    permission?.RequiredTier?.Value,
                    permission?.RequiredRole?.Value,
                    permission?.AllowedStates?.ToList()),
                permission?.Logic.ToString() ?? "Unknown"));
    }
}

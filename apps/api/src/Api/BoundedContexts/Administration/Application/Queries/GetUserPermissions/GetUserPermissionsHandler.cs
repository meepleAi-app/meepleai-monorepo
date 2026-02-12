using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
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
            ?? throw new NotFoundException("User", request.UserId);

        var permissionContext = new PermissionContext(
            user.Tier,
            user.Role,
            user.Status);

        var accessibleFeatures = _permissionRegistry.GetAccessibleFeatures(permissionContext);

        return new GetUserPermissionsResponse(
            user.Tier.Value,
            user.Role.Value,
            user.Status,
            user.Tier.GetLimits(),
            accessibleFeatures);
    }
}

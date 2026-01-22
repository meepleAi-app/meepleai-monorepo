using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.GetAllRateLimitOverrides;

/// <summary>
/// Handler for GetAllRateLimitOverridesQuery.
/// Retrieves all rate limit overrides with user information and paginates results.
/// </summary>
public sealed class GetAllRateLimitOverridesQueryHandler
    : IRequestHandler<GetAllRateLimitOverridesQuery, PagedRateLimitOverridesResult>
{
    private readonly IUserRateLimitOverrideRepository _overrideRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetAllRateLimitOverridesQueryHandler> _logger;

    public GetAllRateLimitOverridesQueryHandler(
        IUserRateLimitOverrideRepository overrideRepository,
        IUserRepository userRepository,
        ILogger<GetAllRateLimitOverridesQueryHandler> logger)
    {
        _overrideRepository = overrideRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<PagedRateLimitOverridesResult> Handle(
        GetAllRateLimitOverridesQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving rate limit overrides (IncludeExpired: {IncludeExpired}, Page: {Page}, PageSize: {PageSize})",
            request.IncludeExpired, request.PageNumber, request.PageSize);

        // Get all overrides (active only or all)
        var allOverrides = await _overrideRepository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false);

        // If includeExpired is true, we need to get ALL overrides (not just active)
        // For now, we only have GetAllActiveAsync, so we filter expired ones client-side
        var overrides = request.IncludeExpired
            ? allOverrides
            : allOverrides.Where(o => o.IsActive()).ToList();

        _logger.LogDebug("Found {Count} rate limit overrides", overrides.Count);

        // Apply pagination
        var totalCount = overrides.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var pagedOverrides = overrides
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Get user info for each override
        // Store full User entity to avoid re-fetching in mapping loop
        var userIds = pagedOverrides.Select(o => o.UserId).Distinct().ToList();
        var users = new Dictionary<Guid, Api.BoundedContexts.Authentication.Domain.Entities.User?>();

        foreach (var userId in userIds)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            users[userId] = user;

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for rate limit override", userId);
            }
        }

        // Get admin info for createdBy
        var adminIds = pagedOverrides.Select(o => o.CreatedByAdminId).Distinct().ToList();
        var admins = new Dictionary<Guid, string>();

        foreach (var adminId in adminIds)
        {
            var admin = await _userRepository.GetByIdAsync(adminId, cancellationToken).ConfigureAwait(false);
            admins[adminId] = admin?.DisplayName ?? "[Unknown Admin]";
        }

        // Map to DTOs - reuse cached User entities to avoid re-fetching
        var dtos = new List<RateLimitOverrideListDto>();

        foreach (var o in pagedOverrides)
        {
            var user = users[o.UserId];

            // Handle deleted users
            var userName = user?.DisplayName ?? "[Deleted User]";
            var userEmail = user?.Email.Value ?? "[deleted@unknown.com]";
            var authTier = user?.Tier ?? Api.BoundedContexts.Authentication.Domain.ValueObjects.UserTier.Free;

            // Map Authentication tier to SystemConfiguration tier
            var sysTier = MapUserTier(user, authTier);

            dtos.Add(new RateLimitOverrideListDto
            {
                Id = o.Id,
                UserId = o.UserId,
                UserName = userName,
                UserEmail = userEmail,
                UserTier = sysTier,
                MaxPendingRequests = o.MaxPendingRequests,
                MaxRequestsPerMonth = o.MaxRequestsPerMonth,
                CooldownAfterRejection = o.CooldownAfterRejection,
                ExpiresAt = o.ExpiresAt,
                IsExpired = o.IsExpired(),
                Reason = o.Reason,
                CreatedAt = o.CreatedAt,
                CreatedByAdminName = admins[o.CreatedByAdminId]
            });
        }

        _logger.LogInformation("Returning {Count} rate limit overrides (Page {Page}/{TotalPages})",
            dtos.Count, request.PageNumber, totalPages);

        return new PagedRateLimitOverridesResult
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    /// <summary>
    /// Maps Authentication tier to SystemConfiguration tier.
    /// Based on RateLimitEvaluator.MapUserTier logic.
    /// </summary>
    private static UserTier MapUserTier(
        Api.BoundedContexts.Authentication.Domain.Entities.User? user,
        Api.BoundedContexts.Authentication.Domain.ValueObjects.UserTier authTier)
    {
        // Check if user is admin via Role (Admin is Role-based, not Tier-based)
        if (user != null && user.Role.Value.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            return UserTier.Admin;
        }

        // Map Authentication.UserTier to SystemConfiguration.UserTier
        // Authentication: "free", "normal", "premium"
        // SystemConfiguration: Free(0), Premium(1), Pro(2), Admin(3)
        return authTier.Value switch
        {
            "free" => UserTier.Free,
            "normal" => UserTier.Premium,
            "premium" => UserTier.Pro,
            _ => UserTier.Free
        };
    }
}

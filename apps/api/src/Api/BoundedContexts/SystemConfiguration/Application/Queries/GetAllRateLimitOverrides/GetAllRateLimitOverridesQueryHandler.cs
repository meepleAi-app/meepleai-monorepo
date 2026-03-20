using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.GetAllRateLimitOverrides;

/// <summary>
/// Handler for GetAllRateLimitOverridesQuery.
/// Retrieves all rate limit overrides with user information and paginates results.
/// </summary>
internal sealed class GetAllRateLimitOverridesQueryHandler
    : IRequestHandler<GetAllRateLimitOverridesQuery, PagedRateLimitOverridesResult>
{
    private readonly IUserRateLimitOverrideRepository _overrideRepository;
    private readonly IUserProfileReadService _userProfileReadService;
    private readonly ILogger<GetAllRateLimitOverridesQueryHandler> _logger;

    public GetAllRateLimitOverridesQueryHandler(
        IUserRateLimitOverrideRepository overrideRepository,
        IUserProfileReadService userProfileReadService,
        ILogger<GetAllRateLimitOverridesQueryHandler> logger)
    {
        _overrideRepository = overrideRepository;
        _userProfileReadService = userProfileReadService;
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

        // Batch-fetch user info for all unique user IDs
        var userIds = pagedOverrides.Select(o => o.UserId).Distinct().ToList();
        var userProfiles = await _userProfileReadService.GetByIdsAsync(userIds, cancellationToken).ConfigureAwait(false);
        var userDictionary = userProfiles.ToDictionary(u => u.Id);

        foreach (var userId in userIds.Where(id => !userDictionary.ContainsKey(id)))
        {
            _logger.LogWarning("User {UserId} not found for rate limit override", userId);
        }

        // Batch-fetch admin info for all unique admin IDs
        var adminIds = pagedOverrides.Select(o => o.CreatedByAdminId).Distinct().ToList();
        var adminProfiles = await _userProfileReadService.GetByIdsAsync(adminIds, cancellationToken).ConfigureAwait(false);
        var adminDictionary = adminProfiles.ToDictionary(u => u.Id);

        // Map to DTOs
        var dtos = new List<RateLimitOverrideListDto>();

        foreach (var o in pagedOverrides)
        {
            userDictionary.TryGetValue(o.UserId, out var user);

            // Handle deleted users
            var userName = user?.DisplayName ?? "[Deleted User]";
            var userEmail = user?.Email ?? "[deleted@unknown.com]";
            var sysTier = MapUserTier(user);

            adminDictionary.TryGetValue(o.CreatedByAdminId, out var admin);

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
                CreatedByAdminName = admin?.DisplayName ?? "[Unknown Admin]"
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
    /// Maps UserProfile tier/role strings to SystemConfiguration UserTier.
    /// Based on RateLimitEvaluator.MapUserTier logic.
    /// </summary>
    private static UserTier MapUserTier(UserProfileDto? user)
    {
        if (user == null) return UserTier.Free;

        // Check if user is admin via Role (Admin is Role-based, not Tier-based)
        if (user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
            user.Role.Equals("superadmin", StringComparison.OrdinalIgnoreCase))
        {
            return UserTier.Admin;
        }

        // Map tier string to SystemConfiguration.UserTier
        // Authentication: "free", "normal", "premium"
        // SystemConfiguration: Free(0), Premium(1), Pro(2), Admin(3)
        return user.Tier.ToLowerInvariant() switch
        {
            "free" => UserTier.Free,
            "normal" => UserTier.Premium,
            "premium" => UserTier.Pro,
            _ => UserTier.Free
        };
    }
}

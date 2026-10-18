using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Services;
using Api.SharedKernel.Services;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Services;

/// <summary>
/// Database-based implementation of game library quota service.
/// Tracks total games in library against the limit of the user's plan
/// (<c>TierLimits.MaxLibraryGames</c>, edited at <c>/admin/config/tiers</c>; issue #4212).
/// </summary>
internal sealed class GameLibraryQuotaService : IGameLibraryQuotaService
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ITierEnforcementService _tiers;
    private readonly ILogger<GameLibraryQuotaService> _logger;

    public GameLibraryQuotaService(
        IUserLibraryRepository libraryRepository,
        ITierEnforcementService tiers,
        ILogger<GameLibraryQuotaService> logger)
    {
        ArgumentNullException.ThrowIfNull(libraryRepository);
        _libraryRepository = libraryRepository;
        ArgumentNullException.ThrowIfNull(tiers);
        _tiers = tiers;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task<LibraryQuotaResult> CheckQuotaAsync(
        Guid userId,
        UserTier userTier,
        AuthRole userRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userTier);
        ArgumentNullException.ThrowIfNull(userRole);

        // Admin and Editor bypass quota checks (unlimited)
        if (userRole.HasPermission(AuthRole.Editor))
        {
            return LibraryQuotaResult.Unlimited();
        }

        var maxGames = await GetPlanLimitAsync(userId, cancellationToken).ConfigureAwait(false);
        var currentCount = await _libraryRepository.GetUserLibraryCountAsync(userId, cancellationToken).ConfigureAwait(false);

        // Check if user has reached the limit
        if (currentCount >= maxGames)
        {
            _logger.LogInformation(
                "User {UserId} has reached game library limit: {CurrentCount}/{MaxGames} for {Tier} tier",
                userId, currentCount, maxGames, userTier.Value);

            return LibraryQuotaResult.Denied(
                $"Game library limit reached ({maxGames} games for {userTier.Value} tier). Upgrade your subscription to add more games.",
                currentCount,
                maxGames);
        }

        return LibraryQuotaResult.Allowed(currentCount, maxGames);
    }

    public async Task<LibraryQuotaInfo> GetQuotaInfoAsync(
        Guid userId,
        UserTier userTier,
        AuthRole userRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userTier);
        ArgumentNullException.ThrowIfNull(userRole);

        // Admin and Editor have unlimited quota
        if (userRole.HasPermission(AuthRole.Editor))
        {
            // Still return actual count for admins (useful for analytics)
            var adminCount = await _libraryRepository.GetUserLibraryCountAsync(userId, cancellationToken).ConfigureAwait(false);
            return new LibraryQuotaInfo
            {
                GamesInLibrary = adminCount,
                MaxGames = int.MaxValue,
                RemainingSlots = int.MaxValue,
                IsUnlimited = true
            };
        }

        var maxGames = await GetPlanLimitAsync(userId, cancellationToken).ConfigureAwait(false);
        var currentCount = await _libraryRepository.GetUserLibraryCountAsync(userId, cancellationToken).ConfigureAwait(false);
        var remaining = Math.Max(0, maxGames - currentCount);

        return new LibraryQuotaInfo
        {
            GamesInLibrary = currentCount,
            MaxGames = maxGames,
            RemainingSlots = remaining,
            IsUnlimited = false
        };
    }

    /// <summary>
    /// The library limit of the user's plan. The plan resolution (contributors as premium, a
    /// missing plan falling back to free) is <see cref="ITierEnforcementService"/>'s, the same
    /// for every limit of the plan.
    /// </summary>
    private async Task<int> GetPlanLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var limits = await _tiers.GetLimitsAsync(userId, cancellationToken).ConfigureAwait(false);
        return limits.MaxLibraryGames;
    }
}

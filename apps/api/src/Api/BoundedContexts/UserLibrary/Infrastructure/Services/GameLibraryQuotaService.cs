using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Services;
using Api.Services;
using AuthRole = Api.BoundedContexts.Authentication.Domain.ValueObjects.Role;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Services;

/// <summary>
/// Database-based implementation of game library quota service.
/// Tracks total games in library with tier-based limits.
/// </summary>
internal sealed class GameLibraryQuotaService : IGameLibraryQuotaService
{
    /// <summary>
    /// Default game limits for each tier.
    /// </summary>
    private static class DefaultLimits
    {
        public const int FreeMaxGames = 5;
        public const int NormalMaxGames = 20;
        public const int PremiumMaxGames = 50;
    }

    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IConfigurationService _configService;
    private readonly ILogger<GameLibraryQuotaService> _logger;

    public GameLibraryQuotaService(
        IUserLibraryRepository libraryRepository,
        IConfigurationService configService,
        ILogger<GameLibraryQuotaService> logger)
    {
        ArgumentNullException.ThrowIfNull(libraryRepository);
        _libraryRepository = libraryRepository;
        ArgumentNullException.ThrowIfNull(configService);
        _configService = configService;
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
        if (userRole.IsAdmin() || userRole.IsEditor())
        {
            return LibraryQuotaResult.Unlimited();
        }

        var maxGames = await GetLimitForTierAsync(userTier).ConfigureAwait(false);
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
        if (userRole.IsAdmin() || userRole.IsEditor())
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

        var maxGames = await GetLimitForTierAsync(userTier).ConfigureAwait(false);
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

    private async Task<int> GetLimitForTierAsync(UserTier tier)
    {
        var configKey = $"LibraryLimits:{tier.Value}:MaxGames";

        // Get from configuration service (with fallback to defaults)
        var configuredLimit = await _configService.GetValueAsync<int?>(configKey, defaultValue: null).ConfigureAwait(false);

        if (configuredLimit.HasValue)
        {
            return configuredLimit.Value;
        }

        // Return default limits
        return tier.Value switch
        {
            "free" => DefaultLimits.FreeMaxGames,
            "normal" => DefaultLimits.NormalMaxGames,
            "premium" => DefaultLimits.PremiumMaxGames,
            _ => DefaultLimits.FreeMaxGames // Default to Free tier limits for unknown tiers
        };
    }
}

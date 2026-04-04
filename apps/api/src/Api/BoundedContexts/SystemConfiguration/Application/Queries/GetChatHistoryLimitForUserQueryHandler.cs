using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Returns the configurable max chat sessions for a user's tier.
/// Reads from SystemConfiguration (admin-configurable via #4918 endpoints).
/// Used by the sliding window archiving logic (Issue #4913).
///
/// Returns 0 to signal "unlimited" for Admin/Editor roles.
/// </summary>
internal class GetChatHistoryLimitForUserQueryHandler
    : IQueryHandler<GetChatHistoryLimitForUserQuery, int>
{
    private readonly IConfigurationService _configService;

    public GetChatHistoryLimitForUserQueryHandler(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<int> Handle(GetChatHistoryLimitForUserQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Admin and Editor roles always get unlimited chat history
        if (IsAdminOrEditor(query.UserRole))
            return 0; // 0 = unlimited convention

        // Map tier string to config key
        var configKey = MapTierToConfigKey(query.UserTier);
        if (configKey is null)
            return GetChatHistoryLimitsQueryHandler.DefaultFreeTierLimit;

        var config = await _configService.GetConfigurationByKeyAsync(
            configKey, null, cancellationToken).ConfigureAwait(false);

        if (config is null || !int.TryParse(
                config.Value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var limit))
        {
            return GetDefaultForTier(query.UserTier);
        }

        return limit;
    }

    private static bool IsAdminOrEditor(string? role) =>
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase);

    private static string? MapTierToConfigKey(string? tier) =>
        tier?.ToLowerInvariant() switch
        {
            "free" or "anonymous" => GetChatHistoryLimitsQueryHandler.FreeTierKey,
            "normal" or "user" => GetChatHistoryLimitsQueryHandler.NormalTierKey,
            "premium" or "pro" or "enterprise" => GetChatHistoryLimitsQueryHandler.PremiumTierKey,
            _ => null
        };

    private static int GetDefaultForTier(string? tier) =>
        tier?.ToLowerInvariant() switch
        {
            "normal" or "user" => GetChatHistoryLimitsQueryHandler.DefaultNormalTierLimit,
            "premium" or "pro" or "enterprise" => GetChatHistoryLimitsQueryHandler.DefaultPremiumTierLimit,
            _ => GetChatHistoryLimitsQueryHandler.DefaultFreeTierLimit
        };
}

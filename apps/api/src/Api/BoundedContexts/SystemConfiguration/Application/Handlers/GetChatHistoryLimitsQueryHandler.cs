using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles retrieval of chat history tier limits configuration.
/// Returns default values if configurations not found in database.
/// Issue #4918: Admin system config — chat history tier limits.
/// </summary>
internal class GetChatHistoryLimitsQueryHandler : IQueryHandler<GetChatHistoryLimitsQuery, ChatHistoryLimitsDto>
{
    private readonly IConfigurationService _configService;

    internal const string FreeTierKey = "ChatHistory:FreeTierLimit";
    internal const string NormalTierKey = "ChatHistory:NormalTierLimit";
    internal const string PremiumTierKey = "ChatHistory:PremiumTierLimit";

    internal const int DefaultFreeTierLimit = 10;
    internal const int DefaultNormalTierLimit = 100;
    internal const int DefaultPremiumTierLimit = 1000;

    public GetChatHistoryLimitsQueryHandler(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<ChatHistoryLimitsDto> Handle(GetChatHistoryLimitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var freeConfigTask = _configService.GetConfigurationByKeyAsync(FreeTierKey, null, cancellationToken);
        var normalConfigTask = _configService.GetConfigurationByKeyAsync(NormalTierKey, null, cancellationToken);
        var premiumConfigTask = _configService.GetConfigurationByKeyAsync(PremiumTierKey, null, cancellationToken);

        await Task.WhenAll(freeConfigTask, normalConfigTask, premiumConfigTask).ConfigureAwait(false);

        var freeConfig = await freeConfigTask.ConfigureAwait(false);
        var normalConfig = await normalConfigTask.ConfigureAwait(false);
        var premiumConfig = await premiumConfigTask.ConfigureAwait(false);

        var freeTierLimit = ParseIntOrDefault(freeConfig?.Value, DefaultFreeTierLimit);
        var normalTierLimit = ParseIntOrDefault(normalConfig?.Value, DefaultNormalTierLimit);
        var premiumTierLimit = ParseIntOrDefault(premiumConfig?.Value, DefaultPremiumTierLimit);

        var allConfigs = new[] { freeConfig, normalConfig, premiumConfig }.Where(c => c != null).ToList();
        var lastUpdatedAt = allConfigs.Count > 0
            ? allConfigs.Max(c => c!.UpdatedAt)
            : DateTime.UtcNow;

        var lastUpdatedByUserId = allConfigs
            .OrderByDescending(c => c!.UpdatedAt)
            .FirstOrDefault()?.UpdatedByUserId;

        return new ChatHistoryLimitsDto(
            FreeTierLimit: freeTierLimit,
            NormalTierLimit: normalTierLimit,
            PremiumTierLimit: premiumTierLimit,
            LastUpdatedAt: lastUpdatedAt,
            LastUpdatedByUserId: lastUpdatedByUserId
        );
    }

    private static int ParseIntOrDefault(string? value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return int.TryParse(value, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }
}

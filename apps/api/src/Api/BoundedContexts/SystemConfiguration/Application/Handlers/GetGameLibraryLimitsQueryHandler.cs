using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles retrieval of game library tier limits configuration.
/// Returns default values if configurations not found in database.
/// </summary>
internal class GetGameLibraryLimitsQueryHandler : IQueryHandler<GetGameLibraryLimitsQuery, GameLibraryLimitsDto>
{
    private readonly IConfigurationService _configService;

    private const string FreeTierKey = "GameLibrary:FreeTierLimit";
    private const string NormalTierKey = "GameLibrary:NormalTierLimit";
    private const string PremiumTierKey = "GameLibrary:PremiumTierLimit";

    private const int DefaultFreeTierLimit = 5;
    private const int DefaultNormalTierLimit = 20;
    private const int DefaultPremiumTierLimit = 50;

    public GetGameLibraryLimitsQueryHandler(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<GameLibraryLimitsDto> Handle(GetGameLibraryLimitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fetch all three configurations in parallel for efficiency
        var freeConfigTask = _configService.GetConfigurationByKeyAsync(FreeTierKey, null, cancellationToken);
        var normalConfigTask = _configService.GetConfigurationByKeyAsync(NormalTierKey, null, cancellationToken);
        var premiumConfigTask = _configService.GetConfigurationByKeyAsync(PremiumTierKey, null, cancellationToken);

        await Task.WhenAll(freeConfigTask, normalConfigTask, premiumConfigTask).ConfigureAwait(false);

        var freeConfig = await freeConfigTask.ConfigureAwait(false);
        var normalConfig = await normalConfigTask.ConfigureAwait(false);
        var premiumConfig = await premiumConfigTask.ConfigureAwait(false);

        // Parse values with defaults
        var freeTierLimit = ParseIntOrDefault(freeConfig?.Value, DefaultFreeTierLimit);
        var normalTierLimit = ParseIntOrDefault(normalConfig?.Value, DefaultNormalTierLimit);
        var premiumTierLimit = ParseIntOrDefault(premiumConfig?.Value, DefaultPremiumTierLimit);

        // Determine most recent update timestamp
        var allConfigs = new[] { freeConfig, normalConfig, premiumConfig }.Where(c => c != null).ToList();
        var lastUpdatedAt = allConfigs.Count > 0
            ? allConfigs.Max(c => c!.UpdatedAt)
            : DateTime.UtcNow;

        var lastUpdatedByUserId = allConfigs
            .OrderByDescending(c => c!.UpdatedAt)
            .FirstOrDefault()?.UpdatedByUserId;

        return new GameLibraryLimitsDto(
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

        return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }
}

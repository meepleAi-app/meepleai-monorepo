using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles retrieval of session tier limits configuration.
/// Returns default values if configurations not found in database.
/// Issue #3070: Session limits backend implementation.
/// </summary>
internal class GetSessionLimitsQueryHandler : IQueryHandler<GetSessionLimitsQuery, SessionLimitsDto>
{
    private readonly IConfigurationService _configService;

    private const string FreeTierKey = "SessionLimits:free:MaxSessions";
    private const string NormalTierKey = "SessionLimits:normal:MaxSessions";
    private const string PremiumTierKey = "SessionLimits:premium:MaxSessions";

    private const int DefaultFreeTierLimit = 3;
    private const int DefaultNormalTierLimit = 10;
    private const int DefaultPremiumTierLimit = -1; // -1 = unlimited

    public GetSessionLimitsQueryHandler(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<SessionLimitsDto> Handle(GetSessionLimitsQuery query, CancellationToken cancellationToken)
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

        return new SessionLimitsDto(
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

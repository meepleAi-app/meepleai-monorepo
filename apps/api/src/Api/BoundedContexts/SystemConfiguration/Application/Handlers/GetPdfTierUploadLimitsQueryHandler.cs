using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles retrieval of PDF upload tier limits configuration.
/// Returns default values if configurations not found in database.
/// Issue #3333: PDF Upload Limits Configuration UI
/// </summary>
internal class GetPdfTierUploadLimitsQueryHandler : IQueryHandler<GetPdfTierUploadLimitsQuery, PdfTierUploadLimitsDto>
{
    private readonly IConfigurationService _configService;

    // Configuration keys match PdfUploadQuotaService
    private const string FreeDailyKey = "UploadLimits:free:DailyLimit";
    private const string FreeWeeklyKey = "UploadLimits:free:WeeklyLimit";
    private const string NormalDailyKey = "UploadLimits:normal:DailyLimit";
    private const string NormalWeeklyKey = "UploadLimits:normal:WeeklyLimit";
    private const string PremiumDailyKey = "UploadLimits:premium:DailyLimit";
    private const string PremiumWeeklyKey = "UploadLimits:premium:WeeklyLimit";

    // Default values matching PdfUploadQuotaService.DefaultQuotas
    private const int DefaultFreeDailyLimit = 5;
    private const int DefaultFreeWeeklyLimit = 20;
    private const int DefaultNormalDailyLimit = 20;
    private const int DefaultNormalWeeklyLimit = 100;
    private const int DefaultPremiumDailyLimit = 100;
    private const int DefaultPremiumWeeklyLimit = 500;

    public GetPdfTierUploadLimitsQueryHandler(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<PdfTierUploadLimitsDto> Handle(GetPdfTierUploadLimitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fetch all six configurations in parallel for efficiency
        var freeDailyTask = _configService.GetConfigurationByKeyAsync(FreeDailyKey, null, cancellationToken);
        var freeWeeklyTask = _configService.GetConfigurationByKeyAsync(FreeWeeklyKey, null, cancellationToken);
        var normalDailyTask = _configService.GetConfigurationByKeyAsync(NormalDailyKey, null, cancellationToken);
        var normalWeeklyTask = _configService.GetConfigurationByKeyAsync(NormalWeeklyKey, null, cancellationToken);
        var premiumDailyTask = _configService.GetConfigurationByKeyAsync(PremiumDailyKey, null, cancellationToken);
        var premiumWeeklyTask = _configService.GetConfigurationByKeyAsync(PremiumWeeklyKey, null, cancellationToken);

        await Task.WhenAll(
            freeDailyTask, freeWeeklyTask,
            normalDailyTask, normalWeeklyTask,
            premiumDailyTask, premiumWeeklyTask
        ).ConfigureAwait(false);

        var freeDailyConfig = await freeDailyTask.ConfigureAwait(false);
        var freeWeeklyConfig = await freeWeeklyTask.ConfigureAwait(false);
        var normalDailyConfig = await normalDailyTask.ConfigureAwait(false);
        var normalWeeklyConfig = await normalWeeklyTask.ConfigureAwait(false);
        var premiumDailyConfig = await premiumDailyTask.ConfigureAwait(false);
        var premiumWeeklyConfig = await premiumWeeklyTask.ConfigureAwait(false);

        // Parse values with defaults
        var freeDailyLimit = ParseIntOrDefault(freeDailyConfig?.Value, DefaultFreeDailyLimit);
        var freeWeeklyLimit = ParseIntOrDefault(freeWeeklyConfig?.Value, DefaultFreeWeeklyLimit);
        var normalDailyLimit = ParseIntOrDefault(normalDailyConfig?.Value, DefaultNormalDailyLimit);
        var normalWeeklyLimit = ParseIntOrDefault(normalWeeklyConfig?.Value, DefaultNormalWeeklyLimit);
        var premiumDailyLimit = ParseIntOrDefault(premiumDailyConfig?.Value, DefaultPremiumDailyLimit);
        var premiumWeeklyLimit = ParseIntOrDefault(premiumWeeklyConfig?.Value, DefaultPremiumWeeklyLimit);

        // Determine most recent update timestamp
        var allConfigs = new[]
        {
            freeDailyConfig, freeWeeklyConfig,
            normalDailyConfig, normalWeeklyConfig,
            premiumDailyConfig, premiumWeeklyConfig
        }.Where(c => c != null).ToList();

        var lastUpdatedAt = allConfigs.Count > 0
            ? allConfigs.Max(c => c!.UpdatedAt)
            : DateTime.UtcNow;

        var lastUpdatedByUserId = allConfigs
            .OrderByDescending(c => c!.UpdatedAt)
            .FirstOrDefault()?.UpdatedByUserId;

        return new PdfTierUploadLimitsDto(
            FreeDailyLimit: freeDailyLimit,
            FreeWeeklyLimit: freeWeeklyLimit,
            NormalDailyLimit: normalDailyLimit,
            NormalWeeklyLimit: normalWeeklyLimit,
            PremiumDailyLimit: premiumDailyLimit,
            PremiumWeeklyLimit: premiumWeeklyLimit,
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

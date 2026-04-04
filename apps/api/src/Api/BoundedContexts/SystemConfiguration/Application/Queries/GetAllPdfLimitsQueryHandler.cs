using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Handles retrieval of PDF upload limits for all tiers.
/// Returns default values if configurations not found in database.
/// Issue #3673: PDF Upload Limits Admin UI
/// </summary>
internal sealed class GetAllPdfLimitsQueryHandler : IQueryHandler<GetAllPdfLimitsQuery, IReadOnlyList<PdfLimitConfigDto>>
{
    private readonly IConfigurationService _configService;

    // Default values matching PdfUploadQuotaService.DefaultQuotas
    private static readonly Dictionary<string, (int daily, int weekly, int perGame)> DefaultLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["free"] = (5, 20, 1),
        ["normal"] = (20, 100, 3),
        ["premium"] = (100, 500, 10)
    };

    public GetAllPdfLimitsQueryHandler(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<IReadOnlyList<PdfLimitConfigDto>> Handle(GetAllPdfLimitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = new List<PdfLimitConfigDto>();

        // Fetch limits for each tier
        foreach (var tier in new[] { "free", "normal", "premium" })
        {
            var limits = await GetLimitsForTierAsync(tier, cancellationToken).ConfigureAwait(false);
            result.Add(limits);
        }

        return result;
    }

    private async Task<PdfLimitConfigDto> GetLimitsForTierAsync(string tier, CancellationToken cancellationToken)
    {
        var dailyKey = $"UploadLimits:{tier}:DailyLimit";
        var weeklyKey = $"UploadLimits:{tier}:WeeklyLimit";
        var perGameKey = $"UploadLimits:{tier}:PerGameLimit";

        // Fetch all three configs in parallel
        var dailyTask = _configService.GetConfigurationByKeyAsync(dailyKey, null, cancellationToken);
        var weeklyTask = _configService.GetConfigurationByKeyAsync(weeklyKey, null, cancellationToken);
        var perGameTask = _configService.GetConfigurationByKeyAsync(perGameKey, null, cancellationToken);

        await Task.WhenAll(dailyTask, weeklyTask, perGameTask).ConfigureAwait(false);

        var dailyConfig = await dailyTask.ConfigureAwait(false);
        var weeklyConfig = await weeklyTask.ConfigureAwait(false);
        var perGameConfig = await perGameTask.ConfigureAwait(false);

        // Use defaults if not configured
        var defaults = DefaultLimits[tier];
        var maxPerDay = ParseIntOrDefault(dailyConfig?.Value, defaults.daily);
        var maxPerWeek = ParseIntOrDefault(weeklyConfig?.Value, defaults.weekly);
        var maxPerGame = ParseIntOrDefault(perGameConfig?.Value, defaults.perGame);

        // Determine most recent update
        var allConfigs = new[] { dailyConfig, weeklyConfig, perGameConfig }
            .Where(c => c != null)
            .ToList();

        DateTime? updatedAt = allConfigs.Count > 0
            ? allConfigs.Max(c => c!.UpdatedAt)
            : null;

        string? updatedBy = allConfigs
            .OrderByDescending(c => c!.UpdatedAt)
            .FirstOrDefault()?.UpdatedByUserId;

        return new PdfLimitConfigDto
        {
            Tier = tier,
            MaxPerDay = maxPerDay,
            MaxPerWeek = maxPerWeek,
            MaxPerGame = maxPerGame,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy
        };
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

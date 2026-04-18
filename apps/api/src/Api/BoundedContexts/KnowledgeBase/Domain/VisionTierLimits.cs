namespace Api.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Configuration limits for vision features per user tier.
/// </summary>
internal record VisionTierConfig(
    int MaxImagesPerMessage, int MaxSnapshotsPerSession,
    int MaxImagesPerSnapshot, int MaxImageResolution,
    bool GameStateExtractionEnabled);

/// <summary>
/// Static lookup for vision tier configurations.
/// Maps user tier names to their vision feature limits.
/// </summary>
internal static class VisionTierLimits
{
    private static readonly IReadOnlyDictionary<string, VisionTierConfig> TierConfigs =
        new Dictionary<string, VisionTierConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["alpha"] = new(5, 20, 5, 2048, true),
            ["free"] = new(2, 5, 3, 1024, false),
            ["premium"] = new(5, 30, 10, 2048, true),
        };

    private static readonly VisionTierConfig DefaultConfig = TierConfigs["free"];

    /// <summary>
    /// Get the vision tier configuration for the given tier name.
    /// Falls back to the "free" tier if the tier name is null or unrecognized.
    /// </summary>
    public static VisionTierConfig GetConfig(string? tier) =>
        tier is null ? DefaultConfig : TierConfigs.GetValueOrDefault(tier, DefaultConfig);
}

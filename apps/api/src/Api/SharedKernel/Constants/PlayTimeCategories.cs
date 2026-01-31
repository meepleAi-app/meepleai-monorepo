namespace Api.SharedKernel.Constants;

/// <summary>
/// Play time category thresholds for board games (in minutes).
/// </summary>
internal static class PlayTimeCategories
{
    /// <summary>
    /// Minimum play time (1 minute).
    /// </summary>
    public const int MinimumPlayTime = 1;

    /// <summary>
    /// Maximum play time (1440 minutes = 24 hours).
    /// </summary>
    public const int MaximumPlayTime = 1440;

    /// <summary>
    /// Threshold for "quick" games (30 minutes or less).
    /// </summary>
    public const int QuickGameThreshold = 30;

    /// <summary>
    /// Threshold for "medium" games (30-90 minutes).
    /// </summary>
    public const int MediumGameThreshold = 90;
}

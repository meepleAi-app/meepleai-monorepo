namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// DTO for user storage quota status.
/// Issue #2732: Storage quota tracking.
/// </summary>
public record StorageQuotaStatus
{
    /// <summary>
    /// Used storage in bytes.
    /// </summary>
    public required long UsedBytes { get; init; }

    /// <summary>
    /// Maximum storage allowed in bytes.
    /// </summary>
    public required long MaxBytes { get; init; }

    /// <summary>
    /// Usage percentage (0-100).
    /// </summary>
    public decimal UsagePercent =>
        MaxBytes > 0 ? (decimal)UsedBytes / MaxBytes * 100 : 0;

    /// <summary>
    /// Available storage in bytes.
    /// </summary>
    public long AvailableBytes =>
        Math.Max(0, MaxBytes - UsedBytes);

    /// <summary>
    /// Whether the quota is exceeded.
    /// </summary>
    public bool IsExceeded => UsedBytes >= MaxBytes;

    /// <summary>
    /// Whether the quota is near limit (>90%).
    /// </summary>
    public bool IsNearLimit => UsagePercent >= 90;
}

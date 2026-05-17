using Api.BoundedContexts.SystemConfiguration.Domain.Enums;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Issue #1089: Singleton-row entity for the global incident/status banner.
/// One row per DB (fixed Id) — updated in-place by admins.
/// </summary>
public sealed class IncidentBannerState
{
    /// <summary>
    /// Fixed singleton identifier — the table always has exactly one row with this Id.
    /// </summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    public const int MaxMessageLength = 500;

    public Guid Id { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public BannerSeverity Severity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    private IncidentBannerState() { } // EF Core

    private IncidentBannerState(
        Guid id,
        string message,
        BannerSeverity severity,
        bool isActive,
        DateTime? startsAt,
        DateTime? endsAt,
        string? updatedBy,
        DateTime nowUtc)
    {
        Id = id;
        Message = message;
        Severity = severity;
        IsActive = isActive;
        StartsAt = startsAt;
        EndsAt = endsAt;
        CreatedAt = nowUtc;
        UpdatedAt = nowUtc;
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// Factory: creates the singleton row with provided values.
    /// </summary>
    public static IncidentBannerState Create(
        string message,
        BannerSeverity severity,
        bool isActive,
        DateTime? startsAt,
        DateTime? endsAt,
        string? updatedBy)
    {
        ValidateMessage(message, isActive);
        ValidateWindow(startsAt, endsAt);

        return new IncidentBannerState(
            id: SingletonId,
            message: message ?? string.Empty,
            severity: severity,
            isActive: isActive,
            startsAt: startsAt,
            endsAt: endsAt,
            updatedBy: updatedBy,
            nowUtc: DateTime.UtcNow);
    }

    /// <summary>
    /// Mutates the singleton with new state.
    /// </summary>
    public void Update(
        string message,
        BannerSeverity severity,
        bool isActive,
        DateTime? startsAt,
        DateTime? endsAt,
        string? updatedBy)
    {
        ValidateMessage(message, isActive);
        ValidateWindow(startsAt, endsAt);

        Message = message ?? string.Empty;
        Severity = severity;
        IsActive = isActive;
        StartsAt = startsAt;
        EndsAt = endsAt;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// Returns true when the banner should be visible to users at the given time.
    /// </summary>
    public bool IsCurrentlyVisible(DateTime now)
    {
        if (!IsActive) return false;
        if (string.IsNullOrWhiteSpace(Message)) return false;
        if (StartsAt.HasValue && now < StartsAt.Value) return false;
        if (EndsAt.HasValue && now >= EndsAt.Value) return false;
        return true;
    }

    private static void ValidateMessage(string message, bool isActive)
    {
        if (isActive && string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required when banner is active", nameof(message));

        if (message != null && message.Length > MaxMessageLength)
            throw new ArgumentException($"Message must not exceed {MaxMessageLength} characters", nameof(message));
    }

    private static void ValidateWindow(DateTime? startsAt, DateTime? endsAt)
    {
        if (startsAt.HasValue && endsAt.HasValue && startsAt.Value >= endsAt.Value)
            throw new ArgumentException("StartsAt must be earlier than EndsAt", nameof(startsAt));
    }
}

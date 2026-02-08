namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// DTO representing a feature and the user's access to it.
/// Issue #3674: Feature Flags Verification - User features endpoint
/// </summary>
public sealed record UserFeatureDto
{
    /// <summary>
    /// Feature flag key (e.g., "advanced_rag", "multi_agent")
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Human-readable feature name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Feature description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether the user has access to this feature
    /// </summary>
    public required bool HasAccess { get; init; }

    /// <summary>
    /// Reason for access (e.g., "tier: premium", "role: admin", "default")
    /// </summary>
    public string? AccessReason { get; init; }
}

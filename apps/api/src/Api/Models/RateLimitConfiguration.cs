namespace Api.Models;

/// <summary>
/// Configuration for a specific role's rate limit.
/// </summary>
public record RoleLimitConfiguration
{
    /// <summary>
    /// Maximum number of tokens in the bucket (burst capacity).
    /// </summary>
    public int MaxTokens { get; init; }

    /// <summary>
    /// Number of tokens added per second (refill rate).
    /// </summary>
    public double RefillRate { get; init; }
}

/// <summary>
/// Rate limiting configuration for all user roles.
/// </summary>
public record RateLimitConfiguration
{
    /// <summary>
    /// Rate limits for admin users.
    /// </summary>
    public RoleLimitConfiguration Admin { get; init; } = new() { MaxTokens = 1000, RefillRate = 10.0 };

    /// <summary>
    /// Rate limits for editor users.
    /// </summary>
    public RoleLimitConfiguration Editor { get; init; } = new() { MaxTokens = 500, RefillRate = 5.0 };

    /// <summary>
    /// Rate limits for regular users.
    /// </summary>
    public RoleLimitConfiguration User { get; init; } = new() { MaxTokens = 100, RefillRate = 1.0 };

    /// <summary>
    /// Rate limits for anonymous (unauthenticated) users.
    /// </summary>
    public RoleLimitConfiguration Anonymous { get; init; } = new() { MaxTokens = 60, RefillRate = 1.0 };
}

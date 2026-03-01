namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

/// <summary>
/// Issue #5075: Snapshot of current RPM/TPM utilization for an OpenRouter provider.
/// </summary>
public sealed record RateLimitStatus
{
    /// <summary>Observed requests in the last 60 seconds (sliding window).</summary>
    public int CurrentRpm { get; init; }

    /// <summary>Configured RPM limit from <see cref="OpenRouterAccountStatus"/> (0 = unknown).</summary>
    public int LimitRpm { get; init; }

    /// <summary>Observed tokens in the last 60 seconds (sliding window).</summary>
    public int CurrentTpm { get; init; }

    /// <summary>Configured TPM limit (0 = unknown / not tracked).</summary>
    public int LimitTpm { get; init; }

    /// <summary>RPM utilization as a fraction [0..1]. 0 if LimitRpm is 0.</summary>
    public double UtilizationPercent { get; init; }

    /// <summary>True if CurrentRpm >= LimitRpm (and LimitRpm > 0).</summary>
    public bool IsThrottled { get; init; }
}

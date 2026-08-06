namespace Api.SharedKernel.Infrastructure.Http;

/// <summary>
/// Why a hardened egress fetch was refused. Maps 1:1 onto the bounded
/// <c>MeepleAiMetrics.EgressBlockReasons</c> labels (#3495 M2) — keep the two in sync so a blocked
/// fetch is always countable without ever putting a host or IP in a metric tag.
/// </summary>
public enum HardenedFetchBlockReason
{
    /// <summary>A hop was not absolute HTTPS (downgrade to http/file/gopher, or a relative target).</summary>
    Scheme,

    /// <summary>A hop targeted a non-default port — probing an internal service by port (#3495 H2).</summary>
    Port,

    /// <summary>The redirect chain looped or exceeded the hop cap.</summary>
    RedirectExhausted,

    /// <summary>The body exceeded the caller's byte ceiling (advertised or mid-stream).</summary>
    SizeCap,

    /// <summary>The TOTAL wall-clock budget for the exchange elapsed (#3495 C4).</summary>
    Timeout,
}

/// <summary>
/// Raised when <see cref="HardenedRedirectFetch"/> refuses an egress fetch. Carries a bounded
/// <see cref="Reason"/> so callers (and the API exception mapping) can react without string-matching
/// a message. The message itself stays generic — the host/IP that triggered the block belongs in the
/// log, never in a response body or a metric tag.
/// </summary>
public sealed class HardenedFetchException : Exception
{
    public HardenedFetchException(HardenedFetchBlockReason reason, string message)
        : base(message) => Reason = reason;

    public HardenedFetchException(HardenedFetchBlockReason reason, string message, Exception innerException)
        : base(message, innerException) => Reason = reason;

    public HardenedFetchBlockReason Reason { get; }
}

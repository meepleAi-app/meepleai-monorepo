namespace Api.SharedKernel.Infrastructure.Resilience;

/// <summary>
/// Detects Polly rejection exceptions by shape instead of by symbolic type catch.
/// <para>
/// Polly v8 (<c>Polly.Core</c>) and Polly v7 (transitive of <c>Microsoft.Extensions.Http.Polly</c>)
/// both export <c>Polly.CircuitBreaker.BrokenCircuitException</c> and
/// <c>Polly.Timeout.TimeoutRejectedException</c>, so a symbolic catch triggers CS0433 (the type
/// exists in two assemblies). Reflection-based detection avoids extern-aliasing the whole Polly
/// namespace, which would cascade through every consumer in the codebase.
/// </para>
/// <para>
/// Promoted to the SharedKernel in #3495 Slice E so egress infrastructure can classify rejections
/// without depending on a bounded context; <c>SharedGameCatalog</c>'s
/// <c>CircuitBreakerExceptionDetector</c> now delegates here (issue #1823 Wave 3 M13 originally).
/// </para>
/// </summary>
internal static class PollyRejectionDetector
{
    /// <summary>
    /// Returns <see langword="true"/> when the exception is a Polly broken-circuit exception
    /// (either the non-generic or the v8 generic variant).
    /// </summary>
    public static bool IsBrokenCircuit(Exception? ex)
    {
        if (ex is null)
        {
            return false;
        }

        var type = ex.GetType();
        if (!string.Equals(type.Namespace, "Polly.CircuitBreaker", StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(type.Name, "BrokenCircuitException", StringComparison.Ordinal)
            || type.Name.StartsWith("BrokenCircuitException`", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the exception is a Polly timeout rejection (the per-try
    /// budget elapsed), as opposed to a caller cancellation.
    /// </summary>
    public static bool IsTimeoutRejected(Exception? ex)
    {
        if (ex is null)
        {
            return false;
        }

        var type = ex.GetType();
        return string.Equals(type.Namespace, "Polly.Timeout", StringComparison.Ordinal)
            && string.Equals(type.Name, "TimeoutRejectedException", StringComparison.Ordinal);
    }
}

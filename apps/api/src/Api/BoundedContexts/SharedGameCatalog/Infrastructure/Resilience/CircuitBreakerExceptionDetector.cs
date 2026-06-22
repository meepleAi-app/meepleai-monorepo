namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Resilience;

/// <summary>
/// Helper that detects the Polly <c>BrokenCircuitException</c> via reflection
/// instead of by symbolic type catch. Polly v8 (<c>Polly.Core</c>) and Polly v7
/// (transitive of <c>Microsoft.Extensions.Http.Polly</c>) both export a type
/// named <c>Polly.CircuitBreaker.BrokenCircuitException</c>, so a symbolic
/// catch triggers CS0433 (type exists in two assemblies). Reflection-based
/// detection avoids extern-aliasing the entire Polly.Core namespace, which
/// would cascade through every consumer in the codebase.
/// Issue #1823 Wave 3 M13 / M10 follow-up.
/// </summary>
internal static class CircuitBreakerExceptionDetector
{
    /// <summary>
    /// Returns <see langword="true"/> when the supplied exception is a
    /// Polly broken-circuit exception (either generic or non-generic variant).
    /// </summary>
    public static bool IsBrokenCircuit(Exception ex)
    {
        if (ex is null) return false;

        var t = ex.GetType();
        if (!string.Equals(t.Namespace, "Polly.CircuitBreaker", StringComparison.Ordinal))
        {
            return false;
        }

        // BrokenCircuitException (non-generic) + BrokenCircuitException`1 (Polly v8
        // generic variant returning a captured result on open).
        return string.Equals(t.Name, "BrokenCircuitException", StringComparison.Ordinal)
            || t.Name.StartsWith("BrokenCircuitException`", StringComparison.Ordinal);
    }
}

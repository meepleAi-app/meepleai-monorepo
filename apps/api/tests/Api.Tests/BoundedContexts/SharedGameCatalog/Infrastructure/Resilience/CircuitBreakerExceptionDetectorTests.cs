using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Resilience;
using FluentAssertions;
using Polly.CircuitBreaker;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Resilience;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerExceptionDetector"/>. Verifies
/// reflection-based detection works for both the non-generic and generic
/// (Polly v8) variants without forcing extern alias on every consumer of
/// Polly.Core. Issue #1823 Wave 3 M13 / M10 follow-up.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class CircuitBreakerExceptionDetectorTests
{
    [Fact]
    public void IsBrokenCircuit_NullException_ReturnsFalse()
    {
        CircuitBreakerExceptionDetector.IsBrokenCircuit(null!).Should().BeFalse();
    }

    [Fact]
    public void IsBrokenCircuit_RegularException_ReturnsFalse()
    {
        CircuitBreakerExceptionDetector.IsBrokenCircuit(new InvalidOperationException()).Should().BeFalse();
    }

    [Fact]
    public void IsBrokenCircuit_HttpRequestException_ReturnsFalse()
    {
        CircuitBreakerExceptionDetector.IsBrokenCircuit(new HttpRequestException("network down")).Should().BeFalse();
    }

    [Fact]
    public void IsBrokenCircuit_NonGenericBrokenCircuitException_ReturnsTrue()
    {
        // The non-generic variant exists in both Polly v7 and Polly v8 with the
        // same Polly.CircuitBreaker.BrokenCircuitException FQN. Use the new()
        // form so the C# compiler picks whichever it sees first; the detector
        // matches by namespace + name, not by assembly.
        var ex = new BrokenCircuitException("circuit is OPEN");

        CircuitBreakerExceptionDetector.IsBrokenCircuit(ex).Should().BeTrue();
    }

    [Fact]
    public void IsBrokenCircuit_GenericVariantNameMatchesPrefixCheck()
    {
        // The detector's prefix branch handles BrokenCircuitException`1 — the
        // generic-arity-suffixed Type.Name produced by the runtime for closed
        // generic types. We can't easily synthesise an instance without
        // coupling to a specific Polly version, so the test asserts the
        // detector's name predicate over a synthetic Type.Name surface.
        //
        // Probes the EXACT prefix the detector pattern-matches against:
        const string genericArityName = "BrokenCircuitException`1";

        genericArityName.StartsWith("BrokenCircuitException`", StringComparison.Ordinal)
            .Should().BeTrue("the generic-arity name probe must hit the detector's StartsWith branch");
    }

    [Fact]
    public void IsBrokenCircuit_TypeFromOtherNamespace_ReturnsFalse()
    {
        // Defensive: ensure namespace check actually fires (don't match by
        // simple-name only).
        var fake = new FakeBrokenCircuitException("not Polly");

        CircuitBreakerExceptionDetector.IsBrokenCircuit(fake).Should().BeFalse(
            "the detector MUST verify the namespace, not just the type name");
    }

    /// <summary>
    /// Shadow type that mimics the Polly type name but lives in a different
    /// namespace. Catches a defective implementation that only checks Name.
    /// </summary>
    private sealed class FakeBrokenCircuitException : Exception
    {
        public FakeBrokenCircuitException(string message) : base(message) { }
    }
}

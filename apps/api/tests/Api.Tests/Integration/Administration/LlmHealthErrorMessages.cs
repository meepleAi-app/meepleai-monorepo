using FluentAssertions;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Expected status messages for LLM health monitoring.
/// Defined as constants to avoid magic strings in test assertions (Issue #1757).
///
/// IMPORTANT: These constants represent the API contract for LLM health status messages.
/// Changes to these messages should be coordinated with frontend/API consumers.
/// </summary>
public static class LlmHealthMessages
{
    /// <summary>
    /// Pattern indicating healthy provider status.
    /// Source: LLM health monitoring service
    /// </summary>
    public const string Healthy = "Healthy";

    /// <summary>
    /// Pattern for summary with healthy providers.
    /// Example: "2/2 providers healthy"
    /// </summary>
    public const string ProvidersHealthy = "providers healthy";
    /// <summary>
    /// Circuit breaker open state (provider unavailable).
    /// Source: Polly circuit breaker
    /// </summary>
    public const string CircuitOpen = "open";

    /// <summary>
    /// Circuit breaker closed state (provider operational).
    /// </summary>
    public const string CircuitClosed = "closed";

    /// <summary>
    /// Circuit breaker half-open state (testing recovery).
    /// </summary>
    public const string CircuitHalfOpen = "half-open";

    /// <summary>
    /// Circuit state unknown (provider not yet tested).
    /// </summary>
    public const string CircuitUnknown = "unknown";
    /// <summary>
    /// Latency stats indicating timeout condition.
    /// </summary>
    public const string LatencyTimeout = "timeout";

    /// <summary>
    /// Latency stats when no data available.
    /// </summary>
    public const string NoData = "No data";
    /// <summary>
    /// Summary pattern for no providers configured.
    /// Example: "0/0 providers"
    /// </summary>
    public const string ZeroProviders = "0/0";
}

/// <summary>
/// Helper extension methods for FluentAssertions to validate LLM health status messages.
/// Provides specific, non-magic-string assertions that catch regressions (Issue #1757).
/// </summary>
public static class LlmHealthAssertionExtensions
{
    /// <summary>
    /// Asserts that the status indicates healthy provider.
    /// </summary>
    public static void ShouldIndicateHealthy(this string? status, string because = "")
    {
        status.Should().NotBeNullOrWhiteSpace(because);
        status.Should().Contain(LlmHealthMessages.Healthy, because);
    }

    /// <summary>
    /// Asserts that the summary indicates providers are healthy.
    /// </summary>
    public static void ShouldIndicateProvidersHealthy(this string? summary, string because = "")
    {
        summary.Should().NotBeNullOrWhiteSpace(because);
        summary.Should().Contain(LlmHealthMessages.ProvidersHealthy, because);
    }

    /// <summary>
    /// Asserts that the circuit state is open (provider unavailable).
    /// </summary>
    public static void ShouldIndicateCircuitOpen(this string? circuitState, string because = "")
    {
        circuitState.Should().NotBeNullOrWhiteSpace(because);
        circuitState.Should().Be(LlmHealthMessages.CircuitOpen, because);
    }

    /// <summary>
    /// Asserts that the circuit state is closed (provider operational).
    /// </summary>
    public static void ShouldIndicateCircuitClosed(this string? circuitState, string because = "")
    {
        circuitState.Should().NotBeNullOrWhiteSpace(because);
        circuitState.Should().Be(LlmHealthMessages.CircuitClosed, because);
    }

    /// <summary>
    /// Asserts that the circuit state is half-open (testing recovery).
    /// </summary>
    public static void ShouldIndicateCircuitHalfOpen(this string? circuitState, string because = "")
    {
        circuitState.Should().NotBeNullOrWhiteSpace(because);
        circuitState.Should().Be(LlmHealthMessages.CircuitHalfOpen, because);
    }

    /// <summary>
    /// Asserts that the circuit state is unknown (provider not yet tested).
    /// </summary>
    public static void ShouldIndicateCircuitUnknown(this string? circuitState, string because = "")
    {
        circuitState.Should().NotBeNullOrWhiteSpace(because);
        circuitState.Should().Be(LlmHealthMessages.CircuitUnknown, because);
    }

    /// <summary>
    /// Asserts that the latency stats indicate timeout condition.
    /// </summary>
    public static void ShouldIndicateTimeout(this string? latencyStats, string because = "")
    {
        latencyStats.Should().NotBeNullOrWhiteSpace(because);
        latencyStats.Should().Contain(LlmHealthMessages.LatencyTimeout, because);
    }

    /// <summary>
    /// Asserts that the latency stats indicate no data available.
    /// </summary>
    public static void ShouldIndicateNoData(this string? latencyStats, string because = "")
    {
        latencyStats.Should().NotBeNullOrWhiteSpace(because);
        latencyStats.Should().Be(LlmHealthMessages.NoData, because);
    }

    /// <summary>
    /// Asserts that the summary indicates no providers configured (0/0).
    /// </summary>
    public static void ShouldIndicateZeroProviders(this string? summary, string because = "")
    {
        summary.Should().NotBeNullOrWhiteSpace(because);
        summary.Should().Contain(LlmHealthMessages.ZeroProviders, because);
    }
}

namespace Api.Tests.Constants;

/// <summary>
/// Centralized test category constants for xUnit [Trait] attribute.
/// Enables selective test execution in CI/CD pipelines and local development.
///
/// Usage: [Trait("Category", TestCategories.Unit)]
///
/// CI Execution Strategy (Issue #1820):
/// - Fast categories run first: Unit, Security
/// - Slow categories run after: Integration, Performance, Slow
/// </summary>
public static class TestCategories
{
    /// <summary>
    /// Unit tests - Fast, isolated tests with no external dependencies.
    /// Target: 70% of test suite (Test Pyramid).
    /// Execution time: &lt;1s per test.
    /// </summary>
    public const string Unit = "Unit";

    /// <summary>
    /// Integration tests - Tests with real infrastructure (Testcontainers, databases).
    /// Target: 20% of test suite.
    /// Execution time: 1-10s per test.
    /// </summary>
    public const string Integration = "Integration";

    /// <summary>
    /// Performance tests - Tests measuring throughput, latency, resource usage.
    /// Target: 5% of test suite.
    /// Execution time: 10-30s per test.
    /// </summary>
    public const string Performance = "Performance";

    /// <summary>
    /// Security tests - Penetration tests, vulnerability scans, attack simulations.
    /// Target: 5% of test suite.
    /// Execution time: Variable (1-60s).
    /// </summary>
    public const string Security = "Security";

    /// <summary>
    /// Slow tests - Long-running tests (>30s) that should run last or separately.
    /// Examples: Large file processing, complex E2E scenarios, stress tests.
    /// </summary>
    public const string Slow = "Slow";

    /// <summary>
    /// E2E (End-to-End) tests - Full system tests across multiple contexts.
    /// Execution time: 30-120s per test.
    /// </summary>
    public const string E2E = "E2E";
}

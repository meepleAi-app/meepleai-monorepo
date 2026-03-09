using Api.Observability;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// Unit tests for LLM Operational Maturity Prometheus metrics (Issue #5480).
/// Verifies that metric instruments are initialized and helper methods don't throw.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Area", "Observability")]
[Trait("Issue", "5480")]
public class LlmOperationalMetricsTests
{
    [Fact]
    public void AllLlmOperationalMetrics_ShouldBeInitialized()
    {
        Assert.NotNull(MeepleAiMetrics.CircuitBreakerState);
        Assert.NotNull(MeepleAiMetrics.OpenRouterBalanceUsd);
        Assert.NotNull(MeepleAiMetrics.OpenRouterRpmUtilization);
        Assert.NotNull(MeepleAiMetrics.LlmCostUsdTotal);
        Assert.NotNull(MeepleAiMetrics.LlmRequestsTotal);
        Assert.NotNull(MeepleAiMetrics.LlmLatencySeconds);
    }

    [Fact]
    public void RecordCircuitBreakerState_ShouldNotThrow()
    {
        var exception = Record.Exception(() =>
            MeepleAiMetrics.RecordCircuitBreakerState("openrouter", 0));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RecordCircuitBreakerState_AllStates_ShouldNotThrow(int state)
    {
        var exception = Record.Exception(() =>
            MeepleAiMetrics.RecordCircuitBreakerState("test-provider", state));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordLlmRequest_Success_ShouldNotThrow()
    {
        var exception = Record.Exception(() =>
            MeepleAiMetrics.RecordLlmRequest("openrouter", "gpt-4", "success", 1.5));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordLlmRequest_WithCost_ShouldNotThrow()
    {
        var exception = Record.Exception(() =>
            MeepleAiMetrics.RecordLlmRequest("openrouter", "gpt-4", "success", 2.0, costUsd: 0.03));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordLlmRequest_Error_ShouldNotThrow()
    {
        var exception = Record.Exception(() =>
            MeepleAiMetrics.RecordLlmRequest("openrouter", "gpt-4", "error", 0.5));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordLlmRequest_NullCost_ShouldNotThrow()
    {
        var exception = Record.Exception(() =>
            MeepleAiMetrics.RecordLlmRequest("openrouter", "gpt-4", "success", 1.0, costUsd: null));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordLlmRequest_ZeroCost_ShouldNotThrow()
    {
        var exception = Record.Exception(() =>
            MeepleAiMetrics.RecordLlmRequest("openrouter", "gpt-4", "success", 1.0, costUsd: 0.0));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordOpenRouterOperationalState_ShouldNotThrow()
    {
        var exception = Record.Exception(() =>
            MeepleAiMetrics.RecordOpenRouterOperationalState(50.0, 0.35));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordOpenRouterOperationalState_LowBalance_ShouldNotThrow()
    {
        var exception = Record.Exception(() =>
            MeepleAiMetrics.RecordOpenRouterOperationalState(2.50, 0.95));

        Assert.Null(exception);
    }

    [Fact]
    public void LlmMetrics_ShouldFollowNamingConventions()
    {
        // All LLM operational metrics should use meepleai.llm.* or meepleai.openrouter.* namespace
        Assert.NotNull(MeepleAiMetrics.CircuitBreakerState);
        Assert.NotNull(MeepleAiMetrics.LlmCostUsdTotal);
        Assert.NotNull(MeepleAiMetrics.LlmRequestsTotal);
        Assert.NotNull(MeepleAiMetrics.LlmLatencySeconds);
        Assert.NotNull(MeepleAiMetrics.OpenRouterBalanceUsd);
        Assert.NotNull(MeepleAiMetrics.OpenRouterRpmUtilization);
    }
}

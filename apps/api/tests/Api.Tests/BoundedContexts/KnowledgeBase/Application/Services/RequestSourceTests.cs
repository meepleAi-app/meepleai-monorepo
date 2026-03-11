using Api.Services;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for RequestSource enum.
/// Issue #5076: Phase 1 test suite — verifies all expected enum values exist
/// and have correct string representations for LlmRequestLogEntity persistence.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5076")]
public sealed class RequestSourceTests
{
    [Fact]
    public void RequestSource_HasExpectedValues()
    {
        // All expected values must be present (regression guard)
        var values = Enum.GetValues<RequestSource>();

        Assert.Contains(RequestSource.Manual, values);
        Assert.Contains(RequestSource.RagPipeline, values);
        Assert.Contains(RequestSource.EventDriven, values);
        Assert.Contains(RequestSource.AutomatedTest, values);
        Assert.Contains(RequestSource.AgentTask, values);
        Assert.Contains(RequestSource.AdminOperation, values);
        Assert.Contains(RequestSource.ABTesting, values);
    }

    [Theory]
    [InlineData(RequestSource.Manual, "Manual")]
    [InlineData(RequestSource.RagPipeline, "RagPipeline")]
    [InlineData(RequestSource.EventDriven, "EventDriven")]
    [InlineData(RequestSource.AutomatedTest, "AutomatedTest")]
    [InlineData(RequestSource.AgentTask, "AgentTask")]
    [InlineData(RequestSource.AdminOperation, "AdminOperation")]
    [InlineData(RequestSource.ABTesting, "ABTesting")]
    public void RequestSource_ToStringMatchesExpected(RequestSource source, string expected)
    {
        Assert.Equal(expected, source.ToString());
    }

    [Theory]
    [InlineData("Manual", RequestSource.Manual)]
    [InlineData("RagPipeline", RequestSource.RagPipeline)]
    [InlineData("EventDriven", RequestSource.EventDriven)]
    [InlineData("AutomatedTest", RequestSource.AutomatedTest)]
    [InlineData("AgentTask", RequestSource.AgentTask)]
    [InlineData("AdminOperation", RequestSource.AdminOperation)]
    [InlineData("ABTesting", RequestSource.ABTesting)]
    public void RequestSource_ParsesFromString(string name, RequestSource expected)
    {
        var parsed = Enum.Parse<RequestSource>(name);
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void RequestSource_HasExactly7Values()
    {
        // Regression guard: adding/removing values requires updating monitoring logic
        var count = Enum.GetValues<RequestSource>().Length;
        Assert.Equal(7, count);
    }
}

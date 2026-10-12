using Api.Services;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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

        values.Should().Contain(RequestSource.Manual);
        values.Should().Contain(RequestSource.RagPipeline);
        values.Should().Contain(RequestSource.EventDriven);
        values.Should().Contain(RequestSource.AutomatedTest);
        values.Should().Contain(RequestSource.AgentTask);
        values.Should().Contain(RequestSource.AdminOperation);
        values.Should().Contain(RequestSource.ABTesting);
        values.Should().Contain(RequestSource.RagClassification);
        values.Should().Contain(RequestSource.Translation);
        values.Should().Contain(RequestSource.ChunkTranslation);
        values.Should().Contain(RequestSource.Evaluation);
        values.Should().Contain(RequestSource.SessionAssistant);
    }

    [Theory]
    [InlineData(RequestSource.Manual, "Manual")]
    [InlineData(RequestSource.RagPipeline, "RagPipeline")]
    [InlineData(RequestSource.EventDriven, "EventDriven")]
    [InlineData(RequestSource.AutomatedTest, "AutomatedTest")]
    [InlineData(RequestSource.AgentTask, "AgentTask")]
    [InlineData(RequestSource.AdminOperation, "AdminOperation")]
    [InlineData(RequestSource.ABTesting, "ABTesting")]
    [InlineData(RequestSource.RagClassification, "RagClassification")]
    [InlineData(RequestSource.Translation, "Translation")]
    [InlineData(RequestSource.ChunkTranslation, "ChunkTranslation")]
    [InlineData(RequestSource.Evaluation, "Evaluation")]
    [InlineData(RequestSource.SessionAssistant, "SessionAssistant")]
    public void RequestSource_ToStringMatchesExpected(RequestSource source, string expected)
    {
        source.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("Manual", RequestSource.Manual)]
    [InlineData("RagPipeline", RequestSource.RagPipeline)]
    [InlineData("EventDriven", RequestSource.EventDriven)]
    [InlineData("AutomatedTest", RequestSource.AutomatedTest)]
    [InlineData("AgentTask", RequestSource.AgentTask)]
    [InlineData("AdminOperation", RequestSource.AdminOperation)]
    [InlineData("ABTesting", RequestSource.ABTesting)]
    [InlineData("RagClassification", RequestSource.RagClassification)]
    [InlineData("Translation", RequestSource.Translation)]
    [InlineData("ChunkTranslation", RequestSource.ChunkTranslation)]
    [InlineData("Evaluation", RequestSource.Evaluation)]
    [InlineData("SessionAssistant", RequestSource.SessionAssistant)]
    public void RequestSource_ParsesFromString(string name, RequestSource expected)
    {
        var parsed = Enum.Parse<RequestSource>(name);
        parsed.Should().Be(expected);
    }

    [Fact]
    public void RequestSource_HasExactly12Values()
    {
        // Regression guard: a new value must be a conscious choice. The name is persisted as a string
        // (llm_cost_logs/llm_request_logs.request_source), so append, never rename. Checked on #4165:
        // no code, dashboard or infra config enumerates the values today. #4166: each value also needs
        // a budget class (LlmBudgetClassification), which LlmBudgetClassificationTests checks.
        // ADR-097: SessionAssistant is the in-game assistant (spec 2026-10-11 §3.2).
        var count = Enum.GetValues<RequestSource>().Length;
        count.Should().Be(12);
    }
}

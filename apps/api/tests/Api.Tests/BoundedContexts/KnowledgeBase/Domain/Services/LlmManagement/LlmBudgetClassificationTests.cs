using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;

/// <summary>
/// #4166 (ADR-095 D6): ogni sorgente di chiamata LLM va a una quota del tetto unico.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4166")]
public sealed class LlmBudgetClassificationTests
{
    /// <summary>Un valore nuovo di RequestSource senza classe farebbe lanciare il gateway a ogni chiamata.</summary>
    [Fact]
    public void EveryRequestSource_HasABudgetClass()
    {
        foreach (var source in Enum.GetValues<RequestSource>())
        {
            var classify = () => LlmBudgetClassification.For(source);
            classify.Should().NotThrow($"{source} deve avere una classe di budget");
        }
    }

    [Theory]
    [InlineData(RequestSource.Manual, "Answer")]
    [InlineData(RequestSource.RagPipeline, "Answer")]
    [InlineData(RequestSource.RagClassification, "Answer")]
    [InlineData(RequestSource.Translation, "Answer")]
    [InlineData(RequestSource.AgentTask, "Answer")] // chat dell'agente di sessione, risposta ancorata, arbitro
    [InlineData(RequestSource.SessionAssistant, "Answer")] // assistente in partita: l'host aspetta la risposta
    [InlineData(RequestSource.ChunkTranslation, "Background")]
    [InlineData(RequestSource.AdminOperation, "Background")]
    [InlineData(RequestSource.EventDriven, "Background")]
    [InlineData(RequestSource.AutomatedTest, "Background")]
    [InlineData(RequestSource.Evaluation, "Evaluation")]
    [InlineData(RequestSource.ABTesting, "Evaluation")]
    public void Classification(RequestSource source, string expected) =>
        LlmBudgetClassification.For(source).ToString().Should().Be(expected);
}

using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Contenuto di una versione di <see cref="Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentProfile"/>
/// (D1 di ADR-095): una versione non valida non deve poter esistere, nemmeno come bozza.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4167")]
public sealed class AgentProfileContentTests
{
    private static AgentProfileContent Build(
        string primaryModelId = "deepseek-chat",
        decimal temperature = 0.3m,
        int maxResponseTokens = 1500,
        Dictionary<string, string>? systemPrompts = null,
        string fallbackLanguage = "en",
        int candidatePoolSize = 20,
        int finalTopK = 5,
        decimal minScore = 0.55m,
        decimal vectorWeight = 0.7m,
        decimal keywordWeight = 0.3m,
        DocumentCategory[]? allowedCategories = null) =>
        AgentProfileContent.Create(
            name: "MeepleAI",
            description: "d",
            persona: "p",
            primaryModelId: primaryModelId,
            fallbackModelId: null,
            temperature: temperature,
            maxResponseTokens: maxResponseTokens,
            citationStyle: CitationStyle.PageReferencesInText,
            systemPrompts: systemPrompts ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = "prompt" },
            fallbackLanguage: fallbackLanguage,
            candidatePoolSize: candidatePoolSize,
            finalTopK: finalTopK,
            minScore: minScore,
            vectorWeight: vectorWeight,
            keywordWeight: keywordWeight,
            rerankerEnabled: true,
            allowedCategories: allowedCategories ?? [DocumentCategory.Rulebook]);

    [Fact]
    public void Create_WithValidValues_KeepsThem()
    {
        var content = Build();

        content.PrimaryModelId.Should().Be("deepseek-chat");
        content.Temperature.Should().Be(0.3m);
        content.MaxResponseTokens.Should().Be(1500);
        content.SystemPrompts["en"].Should().Be("prompt");
        content.FinalTopK.Should().Be(5);
    }

    [Fact]
    public void TwoContents_WithTheSameValues_AreEqual()
    {
        Build().Should().Be(Build());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutAPrimaryModel_IsRejected(string model)
    {
        var act = () => Build(primaryModelId: model);
        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.01)]
    public void Create_WithTemperatureOutside0To2_IsRejected(double temperature)
    {
        var act = () => Build(temperature: (decimal)temperature);
        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32001)]
    public void Create_WithMaxResponseTokensOutsideRange_IsRejected(int tokens)
    {
        var act = () => Build(maxResponseTokens: tokens);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_WithoutAPromptForTheFallbackLanguage_IsRejected()
    {
        var act = () => Build(
            systemPrompts: new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "prompt" },
            fallbackLanguage: "en");
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_WithAnEmptyPrompt_IsRejected()
    {
        var act = () => Build(systemPrompts: new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = " " });
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_WithFinalTopKAboveTheCandidatePool_IsRejected()
    {
        var act = () => Build(candidatePoolSize: 5, finalTopK: 6);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_WithANonPositiveFinalTopK_IsRejected()
    {
        var act = () => Build(finalTopK: 0);
        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_WithMinScoreOutside0To1_IsRejected(double minScore)
    {
        var act = () => Build(minScore: (decimal)minScore);
        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData(-0.1, 0.3)]
    [InlineData(0.7, -0.1)]
    [InlineData(0, 0)]
    public void Create_WithNegativeOrAllZeroFusionWeights_IsRejected(double vector, double keyword)
    {
        var act = () => Build(vectorWeight: (decimal)vector, keywordWeight: (decimal)keyword);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_WithoutAllowedCategories_IsRejected()
    {
        var act = () => Build(allowedCategories: []);
        act.Should().Throw<ValidationException>();
    }
}

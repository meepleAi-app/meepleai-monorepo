using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class TypologyProfileTests
{
    [Fact]
    public void Tutor_HasCorrectRagParams()
    {
        var profile = TypologyProfile.Tutor();
        profile.Name.Should().Be("Tutor");
        profile.TopK.Should().Be(8);
        profile.MinScore.Should().Be(0.50);
        profile.SearchStrategy.Should().Be("HybridSearch");
        profile.Temperature.Should().Be(0.6f);
        profile.MaxTokens.Should().Be(2048);
    }

    [Fact]
    public void Arbitro_HasCorrectRagParams()
    {
        var profile = TypologyProfile.Arbitro();
        profile.Name.Should().Be("Arbitro");
        profile.TopK.Should().Be(5);
        profile.MinScore.Should().Be(0.70);
        profile.SearchStrategy.Should().Be("VectorOnly");
        profile.Temperature.Should().Be(0.3f);
        profile.MaxTokens.Should().Be(1024);
    }

    [Fact]
    public void Stratega_HasCorrectRagParams()
    {
        var profile = TypologyProfile.Stratega();
        profile.Name.Should().Be("Stratega");
        profile.TopK.Should().Be(10);
        profile.MinScore.Should().Be(0.55);
        profile.SearchStrategy.Should().Be("HybridSearch");
        profile.Temperature.Should().Be(0.7f);
        profile.MaxTokens.Should().Be(2048);
    }

    [Fact]
    public void Narratore_HasCorrectRagParams()
    {
        var profile = TypologyProfile.Narratore();
        profile.Name.Should().Be("Narratore");
        profile.TopK.Should().Be(6);
        profile.MinScore.Should().Be(0.45);
        profile.SearchStrategy.Should().Be("HybridSearch");
        profile.Temperature.Should().Be(0.9f);
        profile.MaxTokens.Should().Be(2048);
    }

    [Fact]
    public void Custom_HasBalancedDefaults()
    {
        var profile = TypologyProfile.Custom();
        profile.Name.Should().Be("Custom");
        profile.TopK.Should().Be(8);
        profile.MinScore.Should().Be(0.55);
        profile.Temperature.Should().Be(0.7f);
    }

    [Theory]
    [InlineData("Tutor", "Tutor")]
    [InlineData("Arbitro", "Arbitro")]
    [InlineData("Stratega", "Stratega")]
    [InlineData("Narratore", "Narratore")]
    [InlineData("UnknownType", "Custom")]
    [InlineData(null, "Custom")]
    [InlineData("", "Custom")]
    public void FromName_ResolvesCorrectProfile(string? name, string expectedName)
    {
        var profile = TypologyProfile.FromName(name);
        profile.Name.Should().Be(expectedName);
    }

    [Fact]
    public void BuildSystemPrompt_ReplacesPlaceholders()
    {
        var profile = TypologyProfile.Tutor();
        var prompt = profile.BuildSystemPrompt("MyCatanTutor", "Catan", hasHistory: false);
        prompt.Should().Contain("MyCatanTutor");
        prompt.Should().Contain("Catan");
        prompt.Should().Contain("Tutor");
    }

    [Fact]
    public void BuildSystemPrompt_WithHistory_AddsGuidelines()
    {
        var profile = TypologyProfile.Tutor();
        var prompt = profile.BuildSystemPrompt("Agent", "Catan", hasHistory: true);
        prompt.Should().Contain("Conversation Guidelines");
    }

    [Fact]
    public void BuildSystemPrompt_WithNullGameName_UsesGenericPlaceholder()
    {
        var profile = TypologyProfile.Arbitro();
        var prompt = profile.BuildSystemPrompt("Agent", null, hasHistory: false);
        prompt.Should().Contain("Agent");
        prompt.Should().NotContain("{gameName}");
    }

    [Fact]
    public void Tutor_PromptContainsTypologyKeywords()
    {
        var profile = TypologyProfile.Tutor();
        var prompt = profile.BuildSystemPrompt("Agent", "Catan", false);
        prompt.Should().Contain("Tutor");
        // Should contain didactic keywords
        prompt.Should().ContainAny("spiega", "esempi", "passi");
    }

    [Fact]
    public void Arbitro_PromptContainsTypologyKeywords()
    {
        var profile = TypologyProfile.Arbitro();
        var prompt = profile.BuildSystemPrompt("Agent", "Catan", false);
        prompt.Should().Contain("Arbitro");
        prompt.Should().ContainAny("verdetto", "regola", "citazione");
    }

    [Fact]
    public void Stratega_PromptContainsTypologyKeywords()
    {
        var profile = TypologyProfile.Stratega();
        var prompt = profile.BuildSystemPrompt("Agent", "Catan", false);
        prompt.Should().Contain("Stratega");
        prompt.Should().ContainAny("tattico", "alternative", "raccomandazione");
    }

    [Fact]
    public void Narratore_PromptContainsTypologyKeywords()
    {
        var profile = TypologyProfile.Narratore();
        var prompt = profile.BuildSystemPrompt("Agent", "Catan", false);
        prompt.Should().Contain("Narratore");
        prompt.Should().ContainAny("evocativo", "immersivo", "narrazione");
    }
}

using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for the LlmProvider enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 9
/// </summary>
[Trait("Category", "Unit")]
public sealed class LlmProviderTests
{
    #region Enum Value Tests

    [Fact]
    public void OpenRouter_HasCorrectValue()
    {
        ((int)LlmProvider.OpenRouter).Should().Be(0);
    }

    [Fact]
    public void Ollama_HasCorrectValue()
    {
        ((int)LlmProvider.Ollama).Should().Be(1);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void LlmProvider_HasTwoValues()
    {
        var values = Enum.GetValues<LlmProvider>();
        values.Should().HaveCount(2);
    }

    [Fact]
    public void LlmProvider_AllValuesCanBeParsed()
    {
        var names = new[] { "OpenRouter", "Ollama" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<LlmProvider>(name);
            parsed.Should().BeOneOf(Enum.GetValues<LlmProvider>());
        }
    }

    [Fact]
    public void LlmProvider_ToString_ReturnsExpectedNames()
    {
        LlmProvider.OpenRouter.ToString().Should().Be("OpenRouter");
        LlmProvider.Ollama.ToString().Should().Be("Ollama");
    }

    #endregion

    #region Semantic Tests

    [Fact]
    public void OpenRouter_IsDefaultProvider()
    {
        // OpenRouter = 0 is the default cloud-based provider
        var defaultProvider = default(LlmProvider);
        defaultProvider.Should().Be(LlmProvider.OpenRouter);
    }

    [Fact]
    public void Ollama_IsLocalProvider()
    {
        // Ollama is for local LLM deployment
        ((int)LlmProvider.Ollama).Should().BeGreaterThan((int)LlmProvider.OpenRouter);
    }

    #endregion

    #region Conversion Tests

    [Theory]
    [InlineData(0, LlmProvider.OpenRouter)]
    [InlineData(1, LlmProvider.Ollama)]
    public void LlmProvider_CastFromInt_ReturnsCorrectValue(int value, LlmProvider expected)
    {
        ((LlmProvider)value).Should().Be(expected);
    }

    [Fact]
    public void LlmProvider_IsDefined_ReturnsTrueForValidValues()
    {
        Enum.IsDefined(typeof(LlmProvider), 0).Should().BeTrue();
        Enum.IsDefined(typeof(LlmProvider), 1).Should().BeTrue();
    }

    [Fact]
    public void LlmProvider_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(LlmProvider), 2).Should().BeFalse();
        Enum.IsDefined(typeof(LlmProvider), -1).Should().BeFalse();
    }

    #endregion
}

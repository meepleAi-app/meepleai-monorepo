using Api.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;

/// <summary>
/// Unit tests for <see cref="ProviderResolver"/> (Issue #1838 SP5 F4-C5).
/// Covers category fast-path, modelId parsing for TokenUsage, and fallback semantics.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
[Trait("Issue", "1838")]
public sealed class ProviderResolverTests
{
    [Theory]
    [InlineData(LedgerCategory.Infrastructure, "Infrastructure")]
    [InlineData(LedgerCategory.Subscription, "Subscription")]
    [InlineData(LedgerCategory.TokenPurchase, "TokenPurchase")]
    [InlineData(LedgerCategory.PlatformFee, "PlatformFee")]
    [InlineData(LedgerCategory.Refund, "Refund")]
    [InlineData(LedgerCategory.Marketing, "Marketing")]
    [InlineData(LedgerCategory.Operational, "Operational")]
    [InlineData(LedgerCategory.Other, "Other")]
    public void Resolve_CategoryFastPath_ReturnsCategoryName(LedgerCategory category, string expected)
    {
        ProviderResolver.Resolve(category, metadataJson: null).Should().Be(expected);
    }

    [Theory]
    [InlineData("openai/gpt-4o-mini", "OpenAI")]
    [InlineData("OPENAI/GPT-4O", "OpenAI")]
    [InlineData("anthropic/claude-sonnet-4-6", "Anthropic")]
    [InlineData("deepseek/deepseek-chat", "DeepSeek")]
    [InlineData("openrouter/gpt-4o", "OpenRouter")]
    [InlineData("meta-llama/llama-3-70b", "Meta")]
    [InlineData("mistralai/mistral-large", "Mistral")]
    [InlineData("google/gemini-1.5-pro", "Google")]
    public void Resolve_TokenUsageWithKnownProvider_NormalizesToCanonicalName(string modelId, string expected)
    {
        var json = $$"""{"modelId":"{{modelId}}"}""";
        ProviderResolver.Resolve(LedgerCategory.TokenUsage, json).Should().Be(expected);
    }

    [Fact]
    public void Resolve_TokenUsageWithUnknownProvider_TitleCases()
    {
        var json = """{"modelId":"newprovider/some-model"}""";
        ProviderResolver.Resolve(LedgerCategory.TokenUsage, json).Should().Be("Newprovider");
    }

    [Fact]
    public void Resolve_TokenUsageWithoutSlash_TreatsWholeStringAsProvider()
    {
        var json = """{"modelId":"deepseek"}""";
        ProviderResolver.Resolve(LedgerCategory.TokenUsage, json).Should().Be("DeepSeek");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_TokenUsageWithNullOrEmptyMetadata_ReturnsUnknown(string? metadataJson)
    {
        ProviderResolver.Resolve(LedgerCategory.TokenUsage, metadataJson).Should().Be(ProviderResolver.Unknown);
    }

    [Fact]
    public void Resolve_TokenUsageWithMalformedJson_ReturnsUnknown()
    {
        ProviderResolver.Resolve(LedgerCategory.TokenUsage, "not-json").Should().Be(ProviderResolver.Unknown);
    }

    [Fact]
    public void Resolve_TokenUsageWithMissingModelIdField_ReturnsUnknown()
    {
        var json = """{"someOtherField":"value"}""";
        ProviderResolver.Resolve(LedgerCategory.TokenUsage, json).Should().Be(ProviderResolver.Unknown);
    }

    [Fact]
    public void Resolve_TokenUsageWithBlankModelId_ReturnsUnknown()
    {
        var json = """{"modelId":""}""";
        ProviderResolver.Resolve(LedgerCategory.TokenUsage, json).Should().Be(ProviderResolver.Unknown);
    }

    [Theory]
    [InlineData(LedgerCategory.Subscription, "Subscription")]
    [InlineData(LedgerCategory.TokenPurchase, "Token Purchase")]
    [InlineData(LedgerCategory.TokenUsage, "AI Token Usage")]
    [InlineData(LedgerCategory.PlatformFee, "Platform Fee")]
    [InlineData(LedgerCategory.Refund, "Refund")]
    [InlineData(LedgerCategory.Operational, "Operational")]
    [InlineData(LedgerCategory.Marketing, "Marketing")]
    [InlineData(LedgerCategory.Infrastructure, "Infrastructure")]
    [InlineData(LedgerCategory.Other, "Other")]
    public void ResolveFeatureName_MapsAllCategories(LedgerCategory category, string expected)
    {
        ProviderResolver.ResolveFeatureName(category).Should().Be(expected);
    }
}

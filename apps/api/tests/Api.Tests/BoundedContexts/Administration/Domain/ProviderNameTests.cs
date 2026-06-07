using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public sealed class ProviderNameTests
{
    [Theory]
    [InlineData("deepseek")]
    [InlineData("openrouter")]
    [InlineData("DeepSeek")]
    [InlineData("OPENROUTER")]
    public void Create_AllowedValue_ReturnsNormalizedLowercase(string raw)
    {
        var name = ProviderName.Create(raw);
        name.Value.Should().Be(raw.ToLowerInvariant());
    }

    [Theory]
    [InlineData("ollama")]
    [InlineData("gpt4")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_DisallowedValue_Throws(string raw)
    {
        var act = () => ProviderName.Create(raw);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Allowed_ContainsExpectedSet()
    {
        ProviderName.Allowed.Should().BeEquivalentTo(new[] { "deepseek", "openrouter" });
    }

    [Fact]
    public void Equality_SameValueDifferentCase_AreEqual()
    {
        var a = ProviderName.Create("deepseek");
        var b = ProviderName.Create("DEEPSEEK");
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        ProviderName.Create("deepseek").ToString().Should().Be("deepseek");
    }
}

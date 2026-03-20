using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GameRulesExternalUrlTests
{
    [Fact]
    public void CreateFromUrl_WithValidHttpsUrl_ShouldCreate()
    {
        var rules = GameRules.CreateFromUrl("https://example.com/rules.pdf");

        rules.ExternalUrl.Should().Be("https://example.com/rules.pdf");
        rules.Content.Should().Be(string.Empty);
        rules.Language.Should().Be(string.Empty);
    }

    [Fact]
    public void CreateFromUrl_WithEmptyUrl_ShouldThrow()
    {
        var act = () => GameRules.CreateFromUrl("");
act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromUrl_WithWhitespaceUrl_ShouldThrow()
    {
        var act = () => GameRules.CreateFromUrl("   ");
act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromUrl_WithHttpUrl_ShouldThrow()
    {
        var act = () => GameRules.CreateFromUrl("http://insecure.com/rules.pdf");
act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromUrl_WithInvalidUrl_ShouldThrow()
    {
        var act = () => GameRules.CreateFromUrl("not-a-url");
act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithExternalUrl_ShouldSetAllFields()
    {
        var rules = GameRules.Create("content", "en", "https://example.com/rules.pdf");

        rules.Content.Should().Be("content");
        rules.Language.Should().Be("en");
        rules.ExternalUrl.Should().Be("https://example.com/rules.pdf");
    }

    [Fact]
    public void Create_WithoutExternalUrl_ShouldHaveNullExternalUrl()
    {
        var rules = GameRules.Create("content", "en");

        rules.Content.Should().Be("content");
        rules.Language.Should().Be("en");
        rules.ExternalUrl.Should().BeNull();
    }

    [Fact]
    public void Create_WithInvalidExternalUrl_ShouldThrow()
    {
        var act = () => GameRules.Create("content", "en", "http://insecure.com");
act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameContentLanguageUrl_ShouldBeEqual()
    {
        var rules1 = GameRules.Create("content", "en", "https://example.com/rules.pdf");
        var rules2 = GameRules.Create("content", "en", "https://example.com/rules.pdf");

        rules2.Should().Be(rules1);
    }

    [Fact]
    public void Equality_DifferentUrl_ShouldNotBeEqual()
    {
        var rules1 = GameRules.Create("content", "en", "https://example.com/rules1.pdf");
        var rules2 = GameRules.Create("content", "en", "https://example.com/rules2.pdf");

        rules2.Should().NotBe(rules1);
    }
}
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GameRulesExternalUrlTests
{
    [Fact]
    public void CreateFromUrl_WithValidHttpsUrl_ShouldCreate()
    {
        var rules = GameRules.CreateFromUrl("https://example.com/rules.pdf");

        Assert.Equal("https://example.com/rules.pdf", rules.ExternalUrl);
        Assert.Equal(string.Empty, rules.Content);
        Assert.Equal(string.Empty, rules.Language);
    }

    [Fact]
    public void CreateFromUrl_WithEmptyUrl_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => GameRules.CreateFromUrl(""));
    }

    [Fact]
    public void CreateFromUrl_WithWhitespaceUrl_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => GameRules.CreateFromUrl("   "));
    }

    [Fact]
    public void CreateFromUrl_WithHttpUrl_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => GameRules.CreateFromUrl("http://insecure.com/rules.pdf"));
    }

    [Fact]
    public void CreateFromUrl_WithInvalidUrl_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => GameRules.CreateFromUrl("not-a-url"));
    }

    [Fact]
    public void Create_WithExternalUrl_ShouldSetAllFields()
    {
        var rules = GameRules.Create("content", "en", "https://example.com/rules.pdf");

        Assert.Equal("content", rules.Content);
        Assert.Equal("en", rules.Language);
        Assert.Equal("https://example.com/rules.pdf", rules.ExternalUrl);
    }

    [Fact]
    public void Create_WithoutExternalUrl_ShouldHaveNullExternalUrl()
    {
        var rules = GameRules.Create("content", "en");

        Assert.Equal("content", rules.Content);
        Assert.Equal("en", rules.Language);
        Assert.Null(rules.ExternalUrl);
    }

    [Fact]
    public void Create_WithInvalidExternalUrl_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => GameRules.Create("content", "en", "http://insecure.com"));
    }

    [Fact]
    public void Equality_SameContentLanguageUrl_ShouldBeEqual()
    {
        var rules1 = GameRules.Create("content", "en", "https://example.com/rules.pdf");
        var rules2 = GameRules.Create("content", "en", "https://example.com/rules.pdf");

        Assert.Equal(rules1, rules2);
    }

    [Fact]
    public void Equality_DifferentUrl_ShouldNotBeEqual()
    {
        var rules1 = GameRules.Create("content", "en", "https://example.com/rules1.pdf");
        var rules2 = GameRules.Create("content", "en", "https://example.com/rules2.pdf");

        Assert.NotEqual(rules1, rules2);
    }
}

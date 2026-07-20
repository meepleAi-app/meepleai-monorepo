using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// #2569 follow-up (RAG answer-quality): keyword FTS now honours per-game language.
/// <see cref="KeywordSearchService.ResolveFtsConfig"/> maps a language code to a PostgreSQL FTS
/// config. English keeps using the indexed 'english' search_vector column; non-english is matched
/// against a query-time to_tsvector with the SAME config, so the query/vector configs always agree
/// (sidestepping the #2569 footgun without a multilingual column). Unknown -> 'simple'.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KeywordSearchServiceTests
{
    [Theory]
    [InlineData("en", "english")]
    [InlineData("eng", "english")]
    [InlineData("English", "english")]
    [InlineData("it", "italian")]
    [InlineData("ita", "italian")]
    [InlineData("italian", "italian")]
    [InlineData("italiano", "italian")]
    [InlineData("de", "german")]
    [InlineData("fr", "french")]
    [InlineData("es", "spanish")]
    [InlineData("pt", "portuguese")]
    [InlineData("nl", "dutch")]
    [InlineData("", "english")]   // empty/unspecified -> default english
    [InlineData("xx", "simple")]  // unknown language -> simple (safe under query-time to_tsvector)
    public void ResolveFtsConfig_MapsLanguageToPostgresConfig(string language, string expected)
    {
        KeywordSearchService.ResolveFtsConfig(language).Should().Be(expected);
    }
}

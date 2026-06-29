using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// #2569 footgun guard: the <c>search_vector</c> column on text_chunks/pdf_documents is a
/// single-config GENERATED column built with the <c>'english'</c> FTS config. The keyword FTS
/// query config MUST therefore be <c>'english'</c> regardless of any requested language —
/// otherwise an explicit <c>language: "it"</c> would issue <c>to_tsquery('italian', …)</c>
/// against the 'english' column and the <c>@@</c> operator would silently return nothing
/// (the exact bug class fixed in #2569). Per-query language FTS is not supported until a
/// multilingual column exists (ADR-016 follow-up); until then the config is pinned to english.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KeywordSearchServiceTests
{
    [Theory]
    [InlineData("it")]
    [InlineData("italian")]
    [InlineData("en")]
    [InlineData("english")]
    [InlineData("")]
    [InlineData("xx")]
    public void ResolveFtsConfig_AlwaysReturnsEnglish_RegardlessOfLanguage(string language)
    {
        KeywordSearchService.ResolveFtsConfig(language).Should().Be("english");
    }
}

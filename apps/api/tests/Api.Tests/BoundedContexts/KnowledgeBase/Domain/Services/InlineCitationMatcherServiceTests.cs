using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class InlineCitationMatcherServiceTests
{
    private readonly InlineCitationMatcherService _service = new();

    // ========================================================================
    // Exact substring match
    // ========================================================================

    [Fact]
    public void Match_ExactSubstring_ReturnsHighConfidence()
    {
        // Arrange
        var answer = "The player must roll two dice before moving any pieces on the board.";
        var snippets = new List<Snippet>
        {
            new("The player must roll two dice before moving any pieces on the board.",
                "PDF:d290f1ee-6c54-4b01-90e6-d701748f0851", 3, 0, 0.95f)
        };

        // Act
        var result = _service.Match(answer, snippets);

        // Assert
        result.Should().NotBeEmpty();
        result[0].Confidence.Should().Be(1.0);
        result[0].SnippetIndex.Should().Be(0);
        result[0].PageNumber.Should().Be(3);
        result[0].StartOffset.Should().Be(0);
    }

    // ========================================================================
    // No overlap returns empty
    // ========================================================================

    [Fact]
    public void Match_NoOverlap_ReturnsEmpty()
    {
        // Arrange
        var answer = "This is a completely unrelated answer about cooking recipes.";
        var snippets = new List<Snippet>
        {
            new("Board games require strategic thinking and careful planning of every move during gameplay sessions.",
                "PDF:aaaa0000-0000-0000-0000-000000000001", 1, 0, 0.9f)
        };

        // Act
        var result = _service.Match(answer, snippets);

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // Fuzzy match returns medium confidence
    // ========================================================================

    [Fact]
    public void Match_FuzzyMatch_ReturnsMediumConfidence()
    {
        // Arrange — phrase slightly modified (typo / minor rewording)
        var originalPhrase = "Each player receives five resource cards at the beginning of the game setup phase";
        var modifiedPhrase = "Each player receives five resourse cards at the beginning of the game setup phase";
        var answer = $"According to the rules, {modifiedPhrase}. Then play begins.";
        var snippets = new List<Snippet>
        {
            new(originalPhrase,
                "PDF:bbbb0000-0000-0000-0000-000000000002", 7, 0, 0.88f)
        };

        // Act
        var result = _service.Match(answer, snippets);

        // Assert
        result.Should().NotBeEmpty();
        result[0].Confidence.Should().BeLessThan(1.0);
        result[0].Confidence.Should().BeGreaterThanOrEqualTo(0.6);
    }

    // ========================================================================
    // Overlapping matches keep highest confidence
    // ========================================================================

    [Fact]
    public void Match_OverlappingMatches_KeepsHighestConfidence()
    {
        // Arrange — two snippets that cover the same text in the answer
        var sharedText = "Players take turns rolling dice and moving their pieces around the game board clockwise";
        var answer = $"In this game, {sharedText}. The first to finish wins.";
        var snippets = new List<Snippet>
        {
            // Exact match — should win
            new(sharedText,
                "PDF:cccc0000-0000-0000-0000-000000000003", 2, 0, 0.92f),
            // Contains the same text embedded in longer context
            new($"Introduction: {sharedText} following the standard rules.",
                "PDF:dddd0000-0000-0000-0000-000000000004", 5, 0, 0.85f)
        };

        // Act
        var result = _service.Match(answer, snippets);

        // Assert — should not have overlapping regions
        for (int i = 0; i < result.Count - 1; i++)
        {
            result[i].EndOffset.Should().BeLessThanOrEqualTo(result[i + 1].StartOffset,
                "matches should not overlap");
        }

        // The match with highest confidence should be present
        result.Should().Contain(m => m.Confidence >= 0.99);
    }

    // ========================================================================
    // Empty inputs return empty
    // ========================================================================

    [Fact]
    public void Match_EmptyAnswer_ReturnsEmpty()
    {
        var snippets = new List<Snippet>
        {
            new("Some snippet text with enough words to be significant for matching.", "PDF:eeee0000-0000-0000-0000-000000000005", 1, 0, 0.9f)
        };

        _service.Match("", snippets).Should().BeEmpty();
        _service.Match(null!, snippets).Should().BeEmpty();
    }

    [Fact]
    public void Match_EmptySnippets_ReturnsEmpty()
    {
        _service.Match("Some answer text.", new List<Snippet>()).Should().BeEmpty();
        _service.Match("Some answer text.", null!).Should().BeEmpty();
    }

    // ========================================================================
    // PdfDocumentId extraction
    // ========================================================================

    [Fact]
    public void ExtractPdfDocumentId_FromPdfFormat_ReturnsId()
    {
        var id = InlineCitationMatcherService.ExtractPdfDocumentId("PDF:d290f1ee-6c54-4b01-90e6-d701748f0851");
        id.Should().Be("d290f1ee-6c54-4b01-90e6-d701748f0851");
    }

    [Fact]
    public void ExtractPdfDocumentId_WithoutPrefix_ReturnsSource()
    {
        var id = InlineCitationMatcherService.ExtractPdfDocumentId("some-other-source");
        id.Should().Be("some-other-source");
    }

    [Fact]
    public void ExtractPdfDocumentId_Empty_ReturnsEmpty()
    {
        InlineCitationMatcherService.ExtractPdfDocumentId("").Should().BeEmpty();
        InlineCitationMatcherService.ExtractPdfDocumentId(null!).Should().BeEmpty();
    }

    // ========================================================================
    // Significant phrase extraction
    // ========================================================================

    [Fact]
    public void ExtractSignificantPhrases_FiltersShortSentences()
    {
        var text = "Short. This sentence has more than five words in it. Also short.";
        var phrases = InlineCitationMatcherService.ExtractSignificantPhrases(text);

        phrases.Should().HaveCount(1);
        phrases[0].Should().Contain("more than five words");
    }

    // ========================================================================
    // Levenshtein distance
    // ========================================================================

    [Fact]
    public void LevenshteinDistance_IdenticalStrings_ReturnsZero()
    {
        InlineCitationMatcherService.LevenshteinDistance("hello", "hello").Should().Be(0);
    }

    [Fact]
    public void LevenshteinDistance_SingleCharDifference_ReturnsOne()
    {
        InlineCitationMatcherService.LevenshteinDistance("hello", "hallo").Should().Be(1);
    }
}

using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class CitationTests
{
    [Fact]
    public void Create_ValidCitation_Succeeds()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var pageNumber = 42;
        var snippet = "This is a valid citation snippet";
        var relevanceScore = 0.85;

        // Act
        var citation = new Citation(documentId, pageNumber, snippet, relevanceScore);

        // Assert
        citation.DocumentId.Should().Be(documentId);
        citation.PageNumber.Should().Be(pageNumber);
        citation.Snippet.Should().Be(snippet);
        citation.RelevanceScore.Should().Be(relevanceScore);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_InvalidPageNumber_ThrowsValidationException(int invalidPage)
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var snippet = "Valid snippet";
        var relevanceScore = 0.75;

        // Act
        var act = () => new Citation(documentId, invalidPage, snippet, relevanceScore);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Page number must be positive*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_NullOrEmptySnippet_ThrowsValidationException(string? invalidSnippet)
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var pageNumber = 10;
        var relevanceScore = 0.75;

        // Act
        var act = () => new Citation(documentId, pageNumber, invalidSnippet!, relevanceScore);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Snippet cannot be empty*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    public void Create_InvalidRelevanceScore_ThrowsValidationException(double invalidScore)
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var pageNumber = 10;
        var snippet = "Valid snippet";

        // Act
        var act = () => new Citation(documentId, pageNumber, snippet, invalidScore);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Relevance score must be between 0 and 1*");
    }

    [Fact]
    public void Create_SnippetWithWhitespace_TrimsSnippet()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var pageNumber = 5;
        var snippetWithWhitespace = "  Citation with spaces  ";
        var relevanceScore = 0.80;

        // Act
        var citation = new Citation(documentId, pageNumber, snippetWithWhitespace, relevanceScore);

        // Assert
        citation.Snippet.Should().Be("Citation with spaces");
    }

    [Fact]
    public void ToString_ValidCitation_ReturnsFormattedString()
    {
        // Arrange
        var citation = new Citation(
            Guid.NewGuid(),
            pageNumber: 15,
            snippet: "Test snippet",
            relevanceScore: 0.87);

        // Act
        var result = citation.ToString();

        // Assert
        result.Should().Be("Page 15 (Score: 87%)");
    }

    [Fact]
    public void Equals_TwoCitationsWithSameValues_AreEqual()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var citation1 = new Citation(documentId, 10, "Same snippet", 0.75);
        var citation2 = new Citation(documentId, 10, "Same snippet", 0.75);

        // Act & Assert
        citation1.Should().Be(citation2);
        citation1.GetHashCode().Should().Be(citation2.GetHashCode());
    }

    [Fact]
    public void Equals_TwoCitationsWithDifferentValues_AreNotEqual()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var citation1 = new Citation(documentId, 10, "Snippet 1", 0.75);
        var citation2 = new Citation(documentId, 11, "Snippet 2", 0.80);

        // Act & Assert
        citation1.Should().NotBe(citation2);
    }

    [Fact]
    public void GetEqualityComponents_ReturnsAllProperties()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var citation = new Citation(documentId, 20, "Test", 0.90);

        // Act - Use reflection to access protected method for testing
        var method = typeof(Citation).GetMethod("GetEqualityComponents",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var components = method?.Invoke(citation, null) as IEnumerable<object?>;

        // Assert
        components.Should().NotBeNull();
        var componentList = components!.ToList();
        componentList.Should().HaveCount(4);
        componentList.Should().ContainInOrder(documentId, 20, "Test", 0.90);
    }

    [Theory]
    [InlineData(0.0, "0%")]
    [InlineData(0.5, "50%")]
    [InlineData(1.0, "100%")]
    public void ToString_DifferentRelevanceScores_FormatsCorrectly(double score, string expectedScore)
    {
        // Arrange
        var citation = new Citation(Guid.NewGuid(), 1, "Test", score);

        // Act
        var result = citation.ToString();

        // Assert
        result.Should().Contain(expectedScore);
    }
}

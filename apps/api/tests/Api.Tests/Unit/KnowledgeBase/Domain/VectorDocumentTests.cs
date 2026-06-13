using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for the <see cref="VectorDocument"/> aggregate root.
/// Pins the domain-event emission contract that <c>Create</c> raises
/// <see cref="VectorDocumentIndexedEvent"/> exactly once at construction.
/// Regression guard for the root-cause primario described in epic #2242:
/// bypassing the domain entity via direct <c>VectorDocumentEntity</c> EF
/// init leaves <c>shared_games.has_knowledge_base</c> stale forever.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class VectorDocumentTests
{
    [Fact]
    public void Create_WithValidArguments_RaisesIndexedEventOnce()
    {
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        var doc = VectorDocument.Create(
            pdfDocumentId: pdfId,
            gameId: gameId,
            totalChunks: 42,
            language: "en",
            sharedGameId: sharedGameId);

        doc.DomainEvents.Should().HaveCount(1);
        var indexed = doc.DomainEvents.OfType<VectorDocumentIndexedEvent>().Single();
        indexed.GameId.Should().Be(gameId);
        indexed.ChunkCount.Should().Be(42);
        indexed.SharedGameId.Should().Be(sharedGameId);
        indexed.DocumentId.Should().Be(doc.Id);
    }

    [Fact]
    public void Create_WithoutSharedGameId_RaisesIndexedEventWithNullSharedGameId()
    {
        var doc = VectorDocument.Create(
            pdfDocumentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            totalChunks: 1,
            language: "it");

        var indexed = doc.DomainEvents.OfType<VectorDocumentIndexedEvent>().Single();
        indexed.SharedGameId.Should().BeNull();
    }

    [Fact]
    public void Create_AllocatesFreshGuid_ForEachInvocation()
    {
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var a = VectorDocument.Create(pdfId, gameId, 1, "en");
        var b = VectorDocument.Create(pdfId, gameId, 1, "en");

        a.Id.Should().NotBe(b.Id);
        a.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_NormalizesLanguageToLowerInvariant()
    {
        var doc = VectorDocument.Create(
            pdfDocumentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            totalChunks: 1,
            language: "EN-US");

        doc.Language.Should().Be("en-us");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankLanguage_ThrowsArgumentException(string language)
    {
        Action act = () => VectorDocument.Create(
            pdfDocumentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            totalChunks: 1,
            language: language);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("language");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveTotalChunks_ThrowsArgumentException(int totalChunks)
    {
        Action act = () => VectorDocument.Create(
            pdfDocumentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            totalChunks: totalChunks,
            language: "en");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("totalChunks");
    }
}

using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Unit tests for <see cref="PdfDeduplicationService"/>.
/// Verifies the centralized dedup rule (Task 1 of the PDF cover-config plan):
/// catalog (shared game) PDFs use a GLOBAL content-hash lookup; private-game
/// PDFs use a PER-USER lookup. A match in <see cref="PdfProcessingState.Failed"/>
/// is treated as "no reusable match" (NewUpload); any other match is reused.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class PdfDeduplicationServiceTests
{
    private readonly Mock<IPdfDocumentRepository> _repo = new();

    private PdfDeduplicationService Sut() => new(_repo.Object);

    [Fact]
    public async Task Evaluate_CatalogHashKnownAndReady_ReturnsReuseExisting()
    {
        var existing = PdfDocumentTestFactory.Ready();
        _repo.Setup(r => r.FindByContentHashAsync("h", It.IsAny<CancellationToken>()))
             .ReturnsAsync(existing);

        var result = await Sut().EvaluateAsync("h", sharedGameId: Guid.NewGuid(),
            privateGameId: null, userId: Guid.NewGuid(), CancellationToken.None);

        result.Decision.Should().Be(PdfDedupDecision.ReuseExisting);
        result.ExistingPdfDocumentId.Should().Be(existing.Id);
    }

    [Fact]
    public async Task Evaluate_CatalogHashKnownButFailed_ReturnsNewUpload()
    {
        var existing = PdfDocumentTestFactory.Failed();
        _repo.Setup(r => r.FindByContentHashAsync("h", It.IsAny<CancellationToken>()))
             .ReturnsAsync(existing);

        var result = await Sut().EvaluateAsync("h", Guid.NewGuid(), null, Guid.NewGuid(), CancellationToken.None);

        result.Decision.Should().Be(PdfDedupDecision.NewUpload);
    }

    [Fact]
    public async Task Evaluate_PrivateGame_UsesPerUserLookupNotGlobal()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.FindByContentHashForUserAsync("h", userId, It.IsAny<CancellationToken>()))
             .ReturnsAsync((PdfDocument?)null);

        var result = await Sut().EvaluateAsync("h", sharedGameId: null,
            privateGameId: Guid.NewGuid(), userId: userId, CancellationToken.None);

        result.Decision.Should().Be(PdfDedupDecision.NewUpload);
        _repo.Verify(r => r.FindByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.FindByContentHashForUserAsync("h", userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
/// Test helper building <see cref="PdfDocument"/> instances via the domain factory,
/// advancing state with the existing transition methods (no reflection).
/// </summary>
internal static class PdfDocumentTestFactory
{
    private static PdfDocument Create()
    {
        return new PdfDocument(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            fileName: new FileName("rulebook.pdf"),
            filePath: "/tmp/rulebook.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: Guid.NewGuid());
    }

    /// <summary>Builds a <see cref="PdfDocument"/> advanced to <see cref="PdfProcessingState.Ready"/>.</summary>
    public static PdfDocument Ready()
    {
        var document = Create();
        document.TransitionTo(PdfProcessingState.Uploading);
        document.TransitionTo(PdfProcessingState.Extracting);
        document.TransitionTo(PdfProcessingState.Chunking);
        document.TransitionTo(PdfProcessingState.Embedding);
        document.TransitionTo(PdfProcessingState.Indexing);
        document.TransitionTo(PdfProcessingState.Ready);
        return document;
    }

    /// <summary>Builds a <see cref="PdfDocument"/> advanced to <see cref="PdfProcessingState.Failed"/>.</summary>
    public static PdfDocument Failed()
    {
        var document = Create();
        document.MarkAsFailed("simulated failure", ErrorCategory.Unknown, PdfProcessingState.Extracting);
        return document;
    }
}

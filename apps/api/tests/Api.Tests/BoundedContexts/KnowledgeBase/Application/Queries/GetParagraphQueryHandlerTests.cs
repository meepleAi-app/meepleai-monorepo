using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for <see cref="GetParagraphQueryHandler"/>.
///
/// Coverage:
///   - Happy path (ByPage): numbered lookup returns text directly (no fallback).
///   - Happy path (ByParagraph): paragraph-number lookup returns matching page text (#747 PR-B).
///   - Dispatch: handler routes ByParagraphNumber to the correct repo method.
///   - Authorization: batch not owned by the requesting user → <see cref="NotFoundException"/>.
///   - Validation: page/paragraph number &lt; 1 → <see cref="ArgumentOutOfRangeException"/> (eager guard).
///   - Page overflow: page number exceeds batch total → <see cref="ArgumentOutOfRangeException"/>.
///
/// NOT covered here (deferred to integration tests):
///   - Semantic fallback path: <see cref="SearchQueryHandler"/> is a concrete class with a
///     non-virtual <c>Handle</c> method and six infrastructure dependencies. Spinning up a
///     real instance in unit tests would require mocking all six deps, coupling this test to
///     <c>SearchQueryHandler</c> internals. The fallback branch is validated by integration
///     tests against a real DB + embedding service instead.
///     See: docs/superpowers/plans/2026-05-04-libro-game-assistant-phase3-detailed.md §G4.
///
/// Libro Game AI Assistant MVP Phase 3 — G4. Issue #747 PR-B.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetParagraphQueryHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // -----------------------------------------------------------------------
    // Happy path — ByPageNumber numbered lookup returns OCR text without fallback
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_ByPage_ValidPageWithText_ReturnsParagraphWithoutFallback()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const int pageNumber = 5;
        const string expectedText = "Regola 5: il giocatore di turno muove il pedone di una casella.";

        var repoMock = new Mock<IPhotoBatchUploadRepository>();
        repoMock
            .Setup(r => r.BelongsToUserAsync(uploadId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repoMock
            .Setup(r => r.GetPageTextAsync(uploadId, pageNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedText);

        var sut = CreateHandler(repoMock.Object);

        // Act
        var result = await sut.Handle(
            GetParagraphQuery.ByPage(uploadId, pageNumber, userId),
            Token);

        // Assert
        result.PageNumber.Should().Be(pageNumber);
        result.Text.Should().Be(expectedText);
        result.FallbackUsed.Should().BeFalse();
        result.FallbackMethod.Should().BeNull();
        result.ParagraphNumber.Should().BeNull("ByPage lookups do not surface a paragraph number");

        // GetByIdAsync must NOT be called when numbered text is found — no extra round-trip.
        repoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        // ByPage lookup must NOT touch the paragraph-number repo path.
        repoMock.Verify(r => r.GetPageTextByParagraphNumberAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // Happy path — ByParagraphNumber dispatches to the new repo method (#747)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_ByParagraph_MatchingParagraph_ReturnsPageTextWithParagraphNumber()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const int paragraphNumber = 42;
        const int matchingPhysicalPage = 7;
        const string expectedText = "Paragrafo 42: nei boschi di Avalon ti imbatti in un cavaliere.";

        var repoMock = new Mock<IPhotoBatchUploadRepository>();
        repoMock
            .Setup(r => r.BelongsToUserAsync(uploadId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repoMock
            .Setup(r => r.GetPageTextByParagraphNumberAsync(uploadId, paragraphNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((matchingPhysicalPage, (string?)expectedText));

        var sut = CreateHandler(repoMock.Object);

        // Act
        var result = await sut.Handle(
            GetParagraphQuery.ByParagraph(uploadId, paragraphNumber, userId),
            Token);

        // Assert
        result.PageNumber.Should().Be(matchingPhysicalPage, "the response surfaces the physical page that contained the matched paragraph");
        result.ParagraphNumber.Should().Be(paragraphNumber, "ByParagraph lookups echo the requested paragraph number for the client");
        result.Text.Should().Be(expectedText);
        result.FallbackUsed.Should().BeFalse();
        result.FallbackMethod.Should().BeNull();

        // ByParagraph lookup must NOT touch the page-number repo path.
        repoMock.Verify(r => r.GetPageTextAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        // Numbered match short-circuits the semantic fallback (no GetByIdAsync call).
        repoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // Authorization failure — batch does not belong to the requesting user
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_BatchNotOwnedByUser_ThrowsNotFoundException()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var repoMock = new Mock<IPhotoBatchUploadRepository>();
        repoMock
            .Setup(r => r.BelongsToUserAsync(uploadId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = CreateHandler(repoMock.Object);

        // Act
        Func<Task> act = () => sut.Handle(
            GetParagraphQuery.ByPage(uploadId, 1, userId),
            Token);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{uploadId}*");

        // No page text lookup should occur after the ownership check fails.
        repoMock.Verify(r => r.GetPageTextAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.GetPageTextByParagraphNumberAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // Validation — page number below minimum (eager guard, fires before any DB call)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task Handle_ByPage_InvalidPageNumber_ThrowsArgumentOutOfRange(int invalidPage)
    {
        // Arrange — repo is never reached, so no setup required.
        var sut = CreateHandler(new Mock<IPhotoBatchUploadRepository>().Object);

        // Act
        Func<Task> act = () => sut.Handle(
            GetParagraphQuery.ByPage(Guid.NewGuid(), invalidPage, Guid.NewGuid()),
            Token);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task Handle_ByParagraph_InvalidParagraphNumber_ThrowsArgumentOutOfRange(int invalidParagraph)
    {
        // Arrange
        var sut = CreateHandler(new Mock<IPhotoBatchUploadRepository>().Object);

        // Act
        Func<Task> act = () => sut.Handle(
            GetParagraphQuery.ByParagraph(Guid.NewGuid(), invalidParagraph, Guid.NewGuid()),
            Token);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // -----------------------------------------------------------------------
    // Page overflow — page number exceeds batch.TotalPages (fires after ownership + numbered lookup)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_ByPage_PageExceedsBatchTotal_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const int totalPages = 10;
        const int outOfRangePage = 99;

        var batch = PhotoBatchUpload.Create(userId, Guid.NewGuid(), "en", totalPages);

        var repoMock = new Mock<IPhotoBatchUploadRepository>();
        repoMock
            .Setup(r => r.BelongsToUserAsync(uploadId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repoMock
            .Setup(r => r.GetPageTextAsync(uploadId, outOfRangePage, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null); // No text → triggers fallback path
        repoMock
            .Setup(r => r.GetByIdAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        var sut = CreateHandler(repoMock.Object);

        // Act
        Func<Task> act = () => sut.Handle(
            GetParagraphQuery.ByPage(uploadId, outOfRangePage, userId),
            Token);

        // Assert — ThrowIfGreaterThan produces a message containing the out-of-range value.
        var ex = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        ex.Which.ActualValue.Should().Be(outOfRangePage);
    }

    // -----------------------------------------------------------------------
    // Factory helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="GetParagraphQueryHandler"/> wired with the supplied repository
    /// and a null-object <see cref="SearchQueryHandler"/> that is never invoked in unit tests.
    ///
    /// The <c>SearchQueryHandler</c> parameter is a concrete class; see class-level doc for
    /// why its semantic-fallback invocation is deferred to integration tests.
    /// The handler is passed as <c>null!</c> and should not be reached by any of the covered
    /// unit-test paths.
    /// </summary>
    private static GetParagraphQueryHandler CreateHandler(IPhotoBatchUploadRepository repo)
    {
        // SearchQueryHandler requires six infrastructure deps. In these unit tests none of
        // the covered paths reach the semantic-fallback branch, so we pass null and rely on
        // C# null-tolerance at the call site (null! suppresses the nullable warning only —
        // an actual invocation would throw NullReferenceException, which would make a test
        // fail loudly if a future change accidentally routes a covered path through it).
        return new GetParagraphQueryHandler(
            repo,
            null!,
            new Mock<ILogger<GetParagraphQueryHandler>>().Object);
    }
}

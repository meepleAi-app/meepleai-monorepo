using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Unit tests for PhotoBatchUpload aggregate root.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.1.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class PhotoBatchUploadTests
{
    [Fact]
    public void Create_WithValidParams_InitializesPending()
    {
        var batch = PhotoBatchUpload.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sourceLanguage: "en",
            totalPages: 50);

        batch.Status.Should().Be(PhotoBatchStatus.Pending);
        batch.IndexedPages.Should().Be(0);
        batch.TotalPages.Should().Be(50);
    }

    [Fact]
    public void Create_WithZeroPages_ThrowsArgumentException()
    {
        Action act = () => PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 0);
        act.Should().Throw<ArgumentException>().WithMessage("*positive*");
    }

    [Fact]
    public void RecordPageIndexed_WhenStatusPending_ThrowsInvalidOperationException()
    {
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 2);
        // Did NOT call StartProcessing()

        Action act = () => batch.RecordPageIndexed(1, 0.9, Array.Empty<string>());
        act.Should().Throw<InvalidOperationException>().WithMessage("*Pending*");
    }

    [Fact]
    public void RecordPageIndexed_WhenAllIndexed_RaisesCompletedEventAndSetsCompleted()
    {
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 2);
        batch.StartProcessing();

        batch.RecordPageIndexed(pageNumber: 1, confidence: 0.9, warnings: Array.Empty<string>());
        batch.Status.Should().Be(PhotoBatchStatus.Processing);
        batch.DomainEvents.Should().BeEmpty();

        batch.RecordPageIndexed(pageNumber: 2, confidence: 0.85, warnings: Array.Empty<string>());
        batch.Status.Should().Be(PhotoBatchStatus.Completed);
        batch.DomainEvents.Should().ContainSingle(e => e is PhotoBatchCompletedEvent);
    }

    [Fact]
    public void RecordPageIndexed_WithLowConfidencePages_ReportsCorrectCountInEvent()
    {
        // Arrange: 3-page batch; pages 2 and 3 are below the 0.7 threshold.
        // The accumulator must work from the confidence parameter, not from _pages,
        // so the count is correct even when _pages is empty (no .Include on load).
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 3);
        batch.StartProcessing();

        // Act
        batch.RecordPageIndexed(pageNumber: 1, confidence: 0.9, warnings: Array.Empty<string>());  // high
        batch.RecordPageIndexed(pageNumber: 2, confidence: 0.5, warnings: Array.Empty<string>());  // low
        batch.RecordPageIndexed(pageNumber: 3, confidence: 0.6, warnings: Array.Empty<string>());  // low

        // Assert
        var evt = batch.DomainEvents.OfType<PhotoBatchCompletedEvent>().Single();
        evt.LowConfidencePages.Should().Be(2);
    }

    [Fact]
    public void Fail_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange: complete the batch first
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 1);
        batch.StartProcessing();
        batch.RecordPageIndexed(pageNumber: 1, confidence: 0.9, warnings: Array.Empty<string>());
        batch.Status.Should().Be(PhotoBatchStatus.Completed);

        // Act & Assert: Fail on a terminal aggregate must throw to protect the audit invariant
        Action act = () => batch.Fail("late timeout");
        act.Should().Throw<InvalidOperationException>().WithMessage("*terminal*");
    }
}

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
}

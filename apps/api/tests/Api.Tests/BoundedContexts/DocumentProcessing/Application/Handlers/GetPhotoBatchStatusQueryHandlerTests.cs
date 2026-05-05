using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Unit tests for <see cref="GetPhotoBatchStatusQueryHandler"/>.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.7.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class GetPhotoBatchStatusQueryHandlerTests
{
    private readonly IPhotoBatchUploadRepository _repo = Substitute.For<IPhotoBatchUploadRepository>();
    private readonly IBlobStorageService _blob = Substitute.For<IBlobStorageService>();

    private GetPhotoBatchStatusQueryHandler CreateSut() =>
        new(_repo, _blob);

    [Fact]
    public async Task Handle_OwnerRequestsBatch_ReturnsStatusWithCorrectFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var batch = PhotoBatchUpload.Create(userId, Guid.NewGuid(), "en", 2);

        _repo.FindByIdWithPagesAsync(batch.Id, Arg.Any<CancellationToken>())
             .Returns(batch);

        // GetPresignedDownloadUrlAsync has no CancellationToken param per interface contract
        _blob.GetPresignedDownloadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>())
             .Returns(Task.FromResult<string?>("https://blob/thumb.jpg"));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetPhotoBatchStatusQuery(userId, batch.Id),
            CancellationToken.None);

        // Assert
        result.BatchId.Should().Be(batch.Id);
        result.Status.Should().Be("Pending");
        result.TotalPages.Should().Be(2);
        result.IndexedPages.Should().Be(0);
        result.Pages.Should().BeEmpty("no pages have been attached yet");
    }

    [Fact]
    public async Task Handle_BatchNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _repo.FindByIdWithPagesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((PhotoBatchUpload?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.Handle(
            new GetPhotoBatchStatusQuery(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DifferentUser_ThrowsForbiddenException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var batch = PhotoBatchUpload.Create(ownerId, Guid.NewGuid(), "en", 1);

        _repo.FindByIdWithPagesAsync(batch.Id, Arg.Any<CancellationToken>())
             .Returns(batch);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.Handle(
            new GetPhotoBatchStatusQuery(attackerId, batch.Id),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_BatchWithNoPages_ReturnsZeroLowConfidenceAndEmptyPagesArray()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var batch = PhotoBatchUpload.Create(userId, Guid.NewGuid(), "it", 3);

        _repo.FindByIdWithPagesAsync(batch.Id, Arg.Any<CancellationToken>())
             .Returns(batch);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetPhotoBatchStatusQuery(userId, batch.Id),
            CancellationToken.None);

        // Assert
        result.LowConfidencePages.Should().Be(0);
        result.Pages.Should().BeEmpty();
        result.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OwnerRequest_DoesNotCallBlobForBatchWithNoPages()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var batch = PhotoBatchUpload.Create(userId, Guid.NewGuid(), "en", 1);

        _repo.FindByIdWithPagesAsync(batch.Id, Arg.Any<CancellationToken>())
             .Returns(batch);

        var sut = CreateSut();

        // Act
        await sut.Handle(new GetPhotoBatchStatusQuery(userId, batch.Id), CancellationToken.None);

        // Assert — no blob calls when batch has no pages
        await _blob.DidNotReceive().GetPresignedDownloadUrlAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>());
    }
}

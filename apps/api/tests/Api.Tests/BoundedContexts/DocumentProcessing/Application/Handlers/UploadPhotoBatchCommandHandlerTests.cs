using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Unit tests for <see cref="UploadPhotoBatchCommandHandler"/>.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.5.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class UploadPhotoBatchCommandHandlerTests
{
    private readonly IPhotoBatchUploadRepository _repo = Substitute.For<IPhotoBatchUploadRepository>();
    private readonly IBlobStorageService _blob = Substitute.For<IBlobStorageService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly UploadPhotoBatchCommandHandler _sut;

    public UploadPhotoBatchCommandHandlerTests()
    {
        // Stub StoreAsync to return a success result (signature: string gameId, not Guid)
        _blob
            .StoreAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<BlobCategory>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BlobStorageResult(
                Success: true,
                FileId: Guid.NewGuid().ToString(),
                FilePath: "/test/path",
                FileSizeBytes: 2L));

        // Stub mediator Send to return Unit for EnqueuePhotoBatchProcessingCommand
        _mediator
            .Send(Arg.Any<EnqueuePhotoBatchProcessingCommand>(), Arg.Any<CancellationToken>())
            .Returns(MediatR.Unit.Value);

        _sut = new UploadPhotoBatchCommandHandler(
            _repo,
            _blob,
            _uow,
            _mediator,
            NullLogger<UploadPhotoBatchCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ValidBatch_StoresPhotosAndQueuesProcessJob()
    {
        // Arrange
        var photoBytes = new byte[] { 0x01, 0x02 };
        var base64 = Convert.ToBase64String(photoBytes);
        var cmd = new UploadPhotoBatchCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            SourceLanguage: "en",
            Photos: [new PhotoUploadDto("p1.jpg", base64)]);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.BatchId.Should().NotBeEmpty();
        result.AcceptedCount.Should().Be(1);

        await _repo.Received(1).AddAsync(Arg.Any<PhotoBatchUpload>(), Arg.Any<CancellationToken>());
        await _blob.Received(1).StoreAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            BlobCategory.PhotoBatch,
            cmd.GameId.ToString(),
            Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(
            Arg.Is<EnqueuePhotoBatchProcessingCommand>(c => c.BatchId == result.BatchId),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MultiplePhotos_StoresAllAndAcceptsAll()
    {
        // Arrange
        var b64 = Convert.ToBase64String(new byte[] { 0x01 });
        var cmd = new UploadPhotoBatchCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            SourceLanguage: "en",
            Photos:
            [
                new PhotoUploadDto("p1.jpg", b64),
                new PhotoUploadDto("p2.jpg", b64),
                new PhotoUploadDto("p3.jpg", b64),
            ]);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.AcceptedCount.Should().Be(3);
        await _blob.Received(3).StoreAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            BlobCategory.PhotoBatch,
            cmd.GameId.ToString(),
            Arg.Any<CancellationToken>());
    }
}

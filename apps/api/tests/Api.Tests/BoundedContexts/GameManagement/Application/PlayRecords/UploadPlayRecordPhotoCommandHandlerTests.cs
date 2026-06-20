using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2436")]
public class UploadPlayRecordPhotoCommandHandlerTests
{
    private readonly Mock<IPlayRecordRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<IPhotoPreprocessor> _ocr = new();

    private UploadPlayRecordPhotoCommandHandler CreateSut() =>
        new(_repo.Object, _uow.Object, _blob.Object,
            new PlayRecordPermissionChecker(_repo.Object), _ocr.Object,
            TimeProvider.System,
            NullLogger<UploadPlayRecordPhotoCommandHandler>.Instance);

    private static PlayRecord NewRecord(Guid creator) =>
        PlayRecord.CreateFreeForm(Guid.NewGuid(), "Catan", creator, DateTime.UtcNow.AddDays(-1),
            PlayRecordVisibility.Private, SessionScoringConfig.CreateDefault());

    /// <summary>
    /// Creates a minimal 10x10 blank PNG byte array.
    /// Using a shared byte[] ensures deterministic SHA256 for dedup tests.
    /// </summary>
    private static byte[] MakePngBytes()
    {
        var ms = new MemoryStream();
        using var img = new Image<Rgba32>(10, 10);
        img.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    [Fact]
    public async Task Handle_NonCreator_ThrowsForbidden()
    {
        var record = NewRecord(Guid.NewGuid());
        var stranger = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _repo.Setup(r => r.CanUserEditAsync(stranger, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var pngBytes = MakePngBytes();
        var act = () => CreateSut().Handle(
            new UploadPlayRecordPhotoCommand(record.Id, stranger, new MemoryStream(pngBytes), pngBytes.Length, "image/png", false, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        _blob.Verify(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingRecord_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((PlayRecord?)null);
        var pngBytes = MakePngBytes();
        var act = () => CreateSut().Handle(
            new UploadPlayRecordPhotoCommand(id, Guid.NewGuid(), new MemoryStream(pngBytes), pngBytes.Length, "image/png", false, null),
            CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_Creator_StoresPhotoAndSaves()
    {
        var creator = Guid.NewGuid();
        var record = NewRecord(creator);
        _repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _repo.Setup(r => r.CanUserEditAsync(creator, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _blob.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), BlobCategory.PlayRecordPhoto, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "fid", "play-record-photos/abc/fid_12345678.png", 100));
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://signed/url");

        var pngBytes = MakePngBytes();
        var result = await CreateSut().Handle(
            new UploadPlayRecordPhotoCommand(record.Id, creator, new MemoryStream(pngBytes), pngBytes.Length, "image/png", false, null),
            CancellationToken.None);

        result.PhotoId.Should().NotBeEmpty();
        record.Photos.Should().HaveCount(1);
        _repo.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _ocr.Verify(o => o.PreprocessAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never); // flag false
    }

    [Fact]
    public async Task Handle_ExtractScore_RunsOcrAndStoresText()
    {
        var creator = Guid.NewGuid();
        var record = NewRecord(creator);
        _repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _repo.Setup(r => r.CanUserEditAsync(creator, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _blob.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "fid", "play-record-photos/abc/fid_12345678.png", 100));
        _ocr.Setup(o => o.PreprocessAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoPreprocessResult(Array.Empty<byte>(), "Alice 10 Bob 8", 0.93, PageOrientation.Portrait, false, Array.Empty<string>()));

        var pngBytes = MakePngBytes();
        var result = await CreateSut().Handle(
            new UploadPlayRecordPhotoCommand(record.Id, creator, new MemoryStream(pngBytes), pngBytes.Length, "image/png", true, null),
            CancellationToken.None);

        result.OcrText.Should().Be("Alice 10 Bob 8");
        record.Photos[0].OcrText.Should().Be("Alice 10 Bob 8");
    }

    [Fact]
    public async Task Handle_DuplicateSha_ReturnsExistingWithoutSecondStore()
    {
        var creator = Guid.NewGuid();
        var record = NewRecord(creator);
        _repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _repo.Setup(r => r.CanUserEditAsync(creator, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _blob.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "fid", "play-record-photos/abc/fid_12345678.png", 100));
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://signed/url");

        // Use the same byte array for both commands → identical SHA256 → dedup triggers on cmd2.
        var pngBytes = MakePngBytes();

        var cmd1 = new UploadPlayRecordPhotoCommand(record.Id, creator, new MemoryStream(pngBytes), pngBytes.Length, "image/png", false, null);
        await CreateSut().Handle(cmd1, CancellationToken.None);

        // Second upload with same bytes: record already has the photo with the same SHA256.
        var cmd2 = new UploadPlayRecordPhotoCommand(record.Id, creator, new MemoryStream(pngBytes), pngBytes.Length, "image/png", false, null);
        var result2 = await CreateSut().Handle(cmd2, CancellationToken.None);

        record.Photos.Should().HaveCount(1);              // same bytes → deduped
        result2.WasDeduplicated.Should().BeTrue();
        // cmd1 triggers 2 StoreAsync calls (original + thumbnail); cmd2 short-circuits at dedup → 0 extra calls.
        // Total = 2, NOT 3 or 4.
        _blob.Verify(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}

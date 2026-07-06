using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ImageMagick;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2724")]
public class UploadGameNightPhotoCommandHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _events = new();
    private readonly Mock<IGameNightPhotoRepository> _photos = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<IPhotoPreprocessor> _ocr = new();

    private UploadGameNightPhotoCommandHandler CreateSut() =>
        new(_events.Object, _photos.Object, _uow.Object, _blob.Object, _ocr.Object,
            TimeProvider.System, NullLogger<UploadGameNightPhotoCommandHandler>.Instance);

    private static GameNightEvent NewNight(Guid organizer) =>
        GameNightEvent.Create(organizer, "Serata", DateTimeOffset.UtcNow.AddDays(1));

    private static byte[] MakePngBytes()
    {
        var ms = new MemoryStream();
        using var img = new MagickImage(MagickColors.Black, 10, 10);
        img.Format = MagickFormat.Png;
        img.Write(ms);
        return ms.ToArray();
    }

    private void StubStore() =>
        _blob.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), BlobCategory.GameNightPhoto, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "fid", "game-night-photos/abc/fid_12345678.png", 100));

    [Fact]
    public async Task Handle_NonParticipant_ThrowsForbidden()
    {
        var night = NewNight(Guid.NewGuid());
        var stranger = Guid.NewGuid();
        _events.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);

        var png = MakePngBytes();
        var act = () => CreateSut().Handle(
            new UploadGameNightPhotoCommand(night.Id, stranger, new MemoryStream(png), png.Length, "image/png", false, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        _blob.Verify(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingNight_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _events.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((GameNightEvent?)null);

        var png = MakePngBytes();
        var act = () => CreateSut().Handle(
            new UploadGameNightPhotoCommand(id, Guid.NewGuid(), new MemoryStream(png), png.Length, "image/png", false, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_Organizer_StoresPhotoAndSaves()
    {
        var organizer = Guid.NewGuid();
        var night = NewNight(organizer);
        _events.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);
        _photos.Setup(p => p.GetBySha256Async(night.Id, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((GameNightPhotoEntity?)null);
        StubStore();
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://signed/url");

        var png = MakePngBytes();
        var result = await CreateSut().Handle(
            new UploadGameNightPhotoCommand(night.Id, organizer, new MemoryStream(png), png.Length, "image/png", false, null),
            CancellationToken.None);

        result.PhotoId.Should().NotBeEmpty();
        result.WasDeduplicated.Should().BeFalse();
        _photos.Verify(p => p.AddAsync(It.Is<GameNightPhotoEntity>(e => e.GameNightId == night.Id && e.UploadedByUserId == organizer), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _ocr.Verify(o => o.PreprocessAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExtractScore_RunsOcrAndStoresText()
    {
        var organizer = Guid.NewGuid();
        var night = NewNight(organizer);
        _events.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);
        _photos.Setup(p => p.GetBySha256Async(night.Id, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((GameNightPhotoEntity?)null);
        StubStore();
        _ocr.Setup(o => o.PreprocessAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoPreprocessResult(Array.Empty<byte>(), "Alice 10 Bob 8", 0.93, PageOrientation.Portrait, false, Array.Empty<string>()));

        var png = MakePngBytes();
        var result = await CreateSut().Handle(
            new UploadGameNightPhotoCommand(night.Id, organizer, new MemoryStream(png), png.Length, "image/png", true, null),
            CancellationToken.None);

        result.OcrText.Should().Be("Alice 10 Bob 8");
        _photos.Verify(p => p.AddAsync(It.Is<GameNightPhotoEntity>(e => e.OcrText == "Alice 10 Bob 8"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateSha_ReturnsExistingWithoutStore()
    {
        var organizer = Guid.NewGuid();
        var night = NewNight(organizer);
        _events.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);

        var existing = new GameNightPhotoEntity
        {
            Id = Guid.NewGuid(),
            GameNightId = night.Id,
            BlobUrl = "game-night-photos/abc/existing_12345678.png",
            Sha256Hash = "will-be-overwritten",
            UploadedByUserId = organizer,
            UploadedAt = DateTime.UtcNow,
        };
        _photos.Setup(p => p.GetBySha256Async(night.Id, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://signed/url");

        var png = MakePngBytes();
        var result = await CreateSut().Handle(
            new UploadGameNightPhotoCommand(night.Id, organizer, new MemoryStream(png), png.Length, "image/png", false, null),
            CancellationToken.None);

        result.WasDeduplicated.Should().BeTrue();
        result.PhotoId.Should().Be(existing.Id);
        _blob.Verify(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _photos.Verify(p => p.AddAsync(It.IsAny<GameNightPhotoEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_BadMagicBytes_ThrowsValidation()
    {
        var organizer = Guid.NewGuid();
        var night = NewNight(organizer);
        _events.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);

        // Declared PNG but bytes are not a PNG → magic-byte validation fails.
        var notAnImage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        var act = () => CreateSut().Handle(
            new UploadGameNightPhotoCommand(night.Id, organizer, new MemoryStream(notAnImage), notAnImage.Length, "image/png", false, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();
        _blob.Verify(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

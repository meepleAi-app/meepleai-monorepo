using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2724")]
public class GameNightPhotoGalleryHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _events = new();
    private readonly Mock<IGameNightPhotoRepository> _photos = new();
    private readonly Mock<IBlobStorageService> _blob = new();

    private static GameNightEvent NewNight(Guid organizer) =>
        GameNightEvent.Create(organizer, "Serata", DateTimeOffset.UtcNow.AddDays(1));

    private static GameNightPhotoEntity Photo(Guid nightId, Guid uploader) => new()
    {
        Id = Guid.NewGuid(),
        GameNightId = nightId,
        BlobUrl = "game-night-photos/abc/fid_12345678.png",
        Caption = "Vittoria!",
        UploadedByUserId = uploader,
        UploadedAt = DateTime.UtcNow,
    };

    // ── GetGameNightPhotosQuery (participant-scoped) ─────────────────────────

    [Fact]
    public async Task GetPhotos_NonParticipant_ThrowsForbidden()
    {
        var night = NewNight(Guid.NewGuid());
        _events.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);
        var sut = new GetGameNightPhotosQueryHandler(_events.Object, _photos.Object, _blob.Object);

        var act = () => sut.Handle(new GetGameNightPhotosQuery(night.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetPhotos_MissingNight_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _events.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((GameNightEvent?)null);
        var sut = new GetGameNightPhotosQueryHandler(_events.Object, _photos.Object, _blob.Object);

        var act = () => sut.Handle(new GetGameNightPhotosQuery(id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetPhotos_Organizer_ReturnsPresignedList()
    {
        var organizer = Guid.NewGuid();
        var night = NewNight(organizer);
        _events.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);
        _photos.Setup(p => p.GetByGameNightIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameNightPhotoEntity> { Photo(night.Id, organizer) });
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://signed/url");
        var sut = new GetGameNightPhotosQueryHandler(_events.Object, _photos.Object, _blob.Object);

        var result = await sut.Handle(new GetGameNightPhotosQuery(night.Id, organizer), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].PhotoUrl.Should().Be("https://signed/url");
        result[0].Caption.Should().Be("Vittoria!");
    }

    // ── GetGameNightPhotosByShareTokenQuery (anonymous) ──────────────────────

    [Fact]
    public async Task GetSharedPhotos_UnknownToken_ThrowsNotFound()
    {
        _events.Setup(r => r.GetByShareTokenAsync("nope", It.IsAny<CancellationToken>())).ReturnsAsync((GameNightEvent?)null);
        var sut = new GetGameNightPhotosByShareTokenQueryHandler(_events.Object, _photos.Object, _blob.Object);

        var act = () => sut.Handle(new GetGameNightPhotosByShareTokenQuery("nope"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetSharedPhotos_KnownToken_ReturnsList()
    {
        var night = NewNight(Guid.NewGuid());
        _events.Setup(r => r.GetByShareTokenAsync("tok", It.IsAny<CancellationToken>())).ReturnsAsync(night);
        _photos.Setup(p => p.GetByGameNightIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameNightPhotoEntity> { Photo(night.Id, Guid.NewGuid()) });
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://signed/url");
        var sut = new GetGameNightPhotosByShareTokenQueryHandler(_events.Object, _photos.Object, _blob.Object);

        var result = await sut.Handle(new GetGameNightPhotosByShareTokenQuery("tok"), CancellationToken.None);

        result.Should().HaveCount(1);
    }

    // ── DeleteGameNightPhotoCommand ──────────────────────────────────────────

    private DeleteGameNightPhotoCommandHandler CreateDeleteSut() =>
        new(_events.Object, _photos.Object, Mock.Of<IUnitOfWork>(), _blob.Object,
            NullLogger<DeleteGameNightPhotoCommandHandler>.Instance);

    [Fact]
    public async Task Delete_MissingPhoto_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _photos.Setup(p => p.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((GameNightPhotoEntity?)null);

        var act = () => CreateDeleteSut().Handle(new DeleteGameNightPhotoCommand(Guid.NewGuid(), id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_PhotoOfDifferentNight_ThrowsNotFound()
    {
        var nightId = Guid.NewGuid();
        var photo = Photo(Guid.NewGuid(), Guid.NewGuid()); // belongs to a different night
        _photos.Setup(p => p.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(photo);

        var act = () => CreateDeleteSut().Handle(new DeleteGameNightPhotoCommand(nightId, photo.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_NonUploaderNonOrganizer_ThrowsForbidden()
    {
        var organizer = Guid.NewGuid();
        var uploader = Guid.NewGuid();
        var night = NewNight(organizer);
        var photo = Photo(night.Id, uploader);
        _photos.Setup(p => p.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(photo);
        _events.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);

        var act = () => CreateDeleteSut().Handle(new DeleteGameNightPhotoCommand(night.Id, photo.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Delete_ByUploader_Removes()
    {
        var organizer = Guid.NewGuid();
        var uploader = Guid.NewGuid();
        var night = NewNight(organizer);
        var photo = Photo(night.Id, uploader);
        _photos.Setup(p => p.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(photo);
        _events.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);

        await CreateDeleteSut().Handle(new DeleteGameNightPhotoCommand(night.Id, photo.Id, uploader), CancellationToken.None);

        _photos.Verify(p => p.RemoveAsync(photo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ByOrganizer_Removes()
    {
        var organizer = Guid.NewGuid();
        var uploader = Guid.NewGuid();
        var night = NewNight(organizer);
        var photo = Photo(night.Id, uploader);
        _photos.Setup(p => p.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(photo);
        _events.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);

        await CreateDeleteSut().Handle(new DeleteGameNightPhotoCommand(night.Id, photo.Id, organizer), CancellationToken.None);

        _photos.Verify(p => p.RemoveAsync(photo, It.IsAny<CancellationToken>()), Times.Once);
    }
}

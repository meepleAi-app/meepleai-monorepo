using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ImageMagick;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Testcontainers round-trip for the GameNight recap photo gallery (#2724):
/// participant upload → list, IDOR guards, SHA256 dedup (DB unique index),
/// share-token gallery read, and delete. Validates the EF config + migration.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2724")]
public sealed class GameNightPhotoUploadTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _connectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext _dbContext = null!;
    private IServiceProvider? _serviceProvider;

    public GameNightPhotoUploadTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    private IServiceProvider Sp => _serviceProvider ?? throw new InvalidOperationException("SP not initialized");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct = default)
        {
            var fileId = Guid.NewGuid().ToString("N");
            return Task.FromResult(new BlobStorageResult(true, fileId, $"{category.ToS3Folder()}/{resourceKey}/{fileId}_{fileName}", 100));
        }

        public Task<Stream?> RetrieveAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
            => Task.FromResult<Stream?>(null);
        public Task<bool> DeleteAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task<bool> ExistsAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public string GetStoragePath(string fileId, BlobCategory category, string resourceKey, string fileName)
            => $"{category.ToS3Folder()}/{resourceKey}/{fileId}_{fileName}";
        public Task<string?> GetPresignedDownloadUrlAsync(string fileId, BlobCategory category, string resourceKey, int? expirySeconds = null)
            => Task.FromResult<string?>(null);
        public Task<string?> GetPresignedUrlForRawKeyAsync(string rawKey, int? expirySeconds = null)
            => Task.FromResult<string?>(null);
        public Task<bool> DeleteRawKeyAsync(string rawKey, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> StoreRawKeyAsync(string rawKey, Stream stream, string contentType, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"gamenight_photo_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_connectionString);
        services.AddScoped<IGameNightEventRepository, GameNightEventRepository>();
        services.AddScoped<IGameNightPhotoRepository, GameNightPhotoRepository>();
        services.AddScoped<IBlobStorageService, FakeBlobStorageService>();

        var ocrMock = new Mock<IPhotoPreprocessor>();
        ocrMock.Setup(o => o.PreprocessAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoPreprocessResult(Array.Empty<byte>(), "Alice 10 Bob 8", 0.93, PageOrientation.Portrait, false, Array.Empty<string>()));
        services.AddScoped<IPhotoPreprocessor>(_ => ocrMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = Sp.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        if (_serviceProvider is IAsyncDisposable d) await d.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task<(Guid EventId, Guid Organizer, Guid Participant)> SeedAsync(string? shareToken = null)
    {
        var eventId = Guid.NewGuid();
        var organizer = Guid.NewGuid();
        var participant = Guid.NewGuid();

        _dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = organizer,
            Title = "Serata foto",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1),
            GameIdsJson = JsonSerializer.Serialize(new List<Guid> { Guid.NewGuid() }),
            Status = "Completed",
            CreatedAt = DateTimeOffset.UtcNow,
            ShareToken = shareToken,
            IsShared = shareToken != null,
            Rsvps =
            {
                new GameNightRsvpEntity
                {
                    Id = Guid.NewGuid(), EventId = eventId, UserId = participant,
                    Status = "Accepted", CreatedAt = DateTimeOffset.UtcNow
                }
            },
        });
        await _dbContext.SaveChangesAsync(Ct);
        return (eventId, organizer, participant);
    }

    private static byte[] MakePngBytes()
    {
        using var image = new MagickImage(MagickColors.Black, 10, 10) { Format = MagickFormat.Png };
        return image.ToByteArray();
    }

    private async Task<GameNightPhotoUploadResult> UploadAsync(Guid eventId, Guid userId, byte[] bytes, bool ocr = false, string? caption = null)
    {
        using var scope = Sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(
            new UploadGameNightPhotoCommand(eventId, userId, new MemoryStream(bytes), bytes.Length, "image/png", ocr, caption), Ct);
    }

    [Fact]
    public async Task Upload_ByParticipant_ThenListedForOrganizer()
    {
        var (eventId, organizer, participant) = await SeedAsync();

        var result = await UploadAsync(eventId, participant, MakePngBytes(), caption: "Vittoria!");
        result.WasDeduplicated.Should().BeFalse();

        using var scope = Sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var photos = await mediator.Send(new GetGameNightPhotosQuery(eventId, organizer), Ct);

        photos.Should().HaveCount(1);
        photos[0].Caption.Should().Be("Vittoria!");
        photos[0].UploadedByUserId.Should().Be(participant);
    }

    [Fact]
    public async Task Upload_ByNonParticipant_ThrowsForbidden()
    {
        var (eventId, _, _) = await SeedAsync();
        var stranger = Guid.NewGuid();

        var act = () => UploadAsync(eventId, stranger, MakePngBytes());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task List_ByNonParticipant_ThrowsForbidden()
    {
        var (eventId, organizer, _) = await SeedAsync();
        await UploadAsync(eventId, organizer, MakePngBytes());

        using var scope = Sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = () => mediator.Send(new GetGameNightPhotosQuery(eventId, Guid.NewGuid()), Ct);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Upload_SameBytesTwice_DeduplicatesToOneRow()
    {
        var (eventId, organizer, _) = await SeedAsync();
        var bytes = MakePngBytes();

        var first = await UploadAsync(eventId, organizer, bytes);
        var second = await UploadAsync(eventId, organizer, bytes);

        first.WasDeduplicated.Should().BeFalse();
        second.WasDeduplicated.Should().BeTrue();
        second.PhotoId.Should().Be(first.PhotoId);

        var rows = await _dbContext.GameNightPhotos.Where(p => p.GameNightId == eventId).CountAsync(Ct);
        rows.Should().Be(1);
    }

    [Fact]
    public async Task SharedPhotos_KnownToken_ReturnsList_UnknownToken_ThrowsNotFound()
    {
        var (eventId, organizer, _) = await SeedAsync(shareToken: "public-token");
        await UploadAsync(eventId, organizer, MakePngBytes());

        using var scope = Sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var shared = await mediator.Send(new GetGameNightPhotosByShareTokenQuery("public-token"), Ct);
        shared.Should().HaveCount(1);

        var act = () => mediator.Send(new GetGameNightPhotosByShareTokenQuery("nope"), Ct);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_ByUploader_RemovesRow()
    {
        var (eventId, _, participant) = await SeedAsync();
        var uploaded = await UploadAsync(eventId, participant, MakePngBytes());

        using var scope = Sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new DeleteGameNightPhotoCommand(eventId, uploaded.PhotoId, participant), Ct);

        var rows = await _dbContext.GameNightPhotos.Where(p => p.GameNightId == eventId).CountAsync(Ct);
        rows.Should().Be(0);
    }

    [Fact]
    public async Task Delete_ByNonOwner_ThrowsForbidden()
    {
        var (eventId, _, participant) = await SeedAsync();
        var uploaded = await UploadAsync(eventId, participant, MakePngBytes());
        // A second participant who is neither the uploader nor the organizer.
        var otherUser = Guid.NewGuid();

        using var scope = Sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = () => mediator.Send(new DeleteGameNightPhotoCommand(eventId, uploaded.PhotoId, otherUser), Ct);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}

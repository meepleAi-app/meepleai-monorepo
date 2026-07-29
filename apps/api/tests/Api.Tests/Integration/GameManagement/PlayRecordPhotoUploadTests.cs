using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ImageMagick;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for <see cref="UploadPlayRecordPhotoCommandHandler"/>:
/// Testcontainers Postgres round-trip covering creator upload, forbidden guard,
/// and SHA256 dedup (aggregate + DB unique index). Issue #2436 PR-B.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2436")]
public sealed class PlayRecordPhotoUploadTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private readonly TestTimeProvider _timeProvider = new();

    public PlayRecordPhotoUploadTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider ServiceProvider => _serviceProvider
        ?? throw new InvalidOperationException("Service provider not initialized.");
    private MeepleAiDbContext DbContext => _dbContext
        ?? throw new InvalidOperationException("DbContext not initialized.");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------------
    // Fake IBlobStorageService — returns a deterministic in-memory result so the
    // integration test does not need a real filesystem or S3 bucket.
    // ---------------------------------------------------------------------------
    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<BlobStorageResult> StoreAsync(
            Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct = default)
        {
            var fileId = Guid.NewGuid().ToString("N");
            // Compose a path that mirrors the local BlobStorageService pattern:
            // {category.ToS3Folder()}/{resourceKey}/{fileId}_{fileName}
            var filePath = $"{category.ToS3Folder()}/{resourceKey}/{fileId}_{fileName}";
            return Task.FromResult(new BlobStorageResult(true, fileId, filePath, 100));
        }

        public Task<Stream?> RetrieveAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
            => Task.FromResult<Stream?>(null);

        public Task<bool> DeleteAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
            => Task.FromResult(true);

        public string GetStoragePath(string fileId, BlobCategory category, string resourceKey, string fileName)
            => $"{category.ToS3Folder()}/{resourceKey}/{fileId}_{fileName}";

        public Task<bool> ExistsAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        // Local storage returns null (handler falls back to raw path).
        public Task<string?> GetPresignedDownloadUrlAsync(string fileId, BlobCategory category, string resourceKey, int? expirySeconds = null)
            => Task.FromResult<string?>(null);
        public Task<string?> GetPresignedUrlForRawKeyAsync(string rawKey, int? expirySeconds = null)
            => Task.FromResult<string?>(null);
        public Task<bool> DeleteRawKeyAsync(string rawKey, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> StoreRawKeyAsync(string rawKey, Stream stream, string contentType, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    // ---------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_playrecordphoto_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // PlayRecord stack
        services.AddScoped<IPlayRecordRepository, PlayRecordRepository>();
        services.AddScoped<PlayRecordPermissionChecker>();

        // SharedGameCatalog repos needed by CreatePlayRecordCommandHandler
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();
        services.AddScoped<IGameCoreDataProvider, GameCoreDataProvider>();

        // Blob storage: register the fake so the handler can resolve IBlobStorageService
        services.AddScoped<IBlobStorageService, FakeBlobStorageService>();

        // OCR preprocessor: Moq returning empty text (OCR off by default in tests)
        var ocrMock = new Mock<IPhotoPreprocessor>();
        ocrMock.Setup(o => o.PreprocessAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoPreprocessResult(
                Array.Empty<byte>(), "Alice 10 Bob 8", 0.93,
                PageOrientation.Portrait, false, Array.Empty<string>()));
        services.AddScoped<IPhotoPreprocessor>(_ => ocrMock.Object);

        // Override the singleton TimeProvider registered by CreateBase with our fake
        services.AddSingleton<TimeProvider>(_timeProvider);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        await DbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();

        // Fixture handles container lifecycle — we only drop our isolated DB
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generates a minimal but valid PNG byte array using Magick.NET.
    /// Issue #2055 Phase G DEC-3d-1: migrated from SixLabors.ImageSharp 3.x to Magick.NET-Q8-AnyCPU 14.x (Apache 2.0).
    /// </summary>
    private static byte[] MakePngBytes(int width = 10, int height = 10)
    {
        using var image = new MagickImage(MagickColors.Black, (uint)width, (uint)height);
        image.Format = MagickFormat.Png;
        using var ms = new MemoryStream();
        image.Write(ms);
        return ms.ToArray();
    }

    private async Task<Guid> SeedTestUserAsync(Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = id,
            Email = $"photo-test-{id:N}@meepleai.dev",
            DisplayName = "Photo Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    private async Task<Guid> CreateTestRecordAsync(Guid creatorUserId)
    {
        // We need a shared game for the FK
        var gameId = Guid.NewGuid();
        using (var scope = ServiceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            db.SharedGames.Add(new SharedGameEntity
            {
                Id = gameId,
                Title = "Photo Test Game",
                MinPlayers = 1,
                MaxPlayers = 4,
                PlayingTimeMinutes = 60,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(TestCancellationToken);
        }

        var command = new CreatePlayRecordCommand(
            creatorUserId, gameId, "Photo Test Game",
            _timeProvider.UtcNow.AddHours(-1), PlayRecordVisibility.Private);

        return await SendInScopeAsync(command);
    }

    private async Task<TResult> SendInScopeAsync<TResult>(IRequest<TResult> command)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(command, TestCancellationToken);
    }

    private async Task SendInScopeAsync(IRequest command)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command, TestCancellationToken);
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creator uploads a PNG → command succeeds, one row is written to play_record_photos,
    /// and the PlayRecord.Photos collection contains the persisted photo.
    /// </summary>
    [Fact]
    public async Task UploadPhoto_AsCreator_PersistsPhotoRow()
    {
        // Arrange
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var pngBytes = MakePngBytes();

        var command = new UploadPlayRecordPhotoCommand(
            recordId, creatorId,
            new MemoryStream(pngBytes), pngBytes.Length,
            "image/png", false, "test caption");

        // Act
        var result = await SendInScopeAsync(command);

        // Assert — result contract
        result.Should().NotBeNull();
        result.PhotoId.Should().NotBeEmpty();
        result.WasDeduplicated.Should().BeFalse();
        result.PhotoUrl.Should().NotBeNullOrWhiteSpace();

        // Assert — DB row via a fresh scope
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var photos = await db.PlayRecordPhotos
            .Where(p => p.PlayRecordId == recordId)
            .ToListAsync(TestCancellationToken);

        photos.Should().HaveCount(1);
        photos[0].Id.Should().Be(result.PhotoId);
        photos[0].BlobUrl.Should().NotBeNullOrWhiteSpace();
        photos[0].Sha256Hash.Should().NotBeNullOrWhiteSpace();
        photos[0].FileSizeBytes.Should().Be(pngBytes.Length);
        photos[0].Caption.Should().Be("test caption");
        photos[0].UploadedByUserId.Should().Be(creatorId);
    }

    /// <summary>
    /// A user who is NOT the record creator receives <see cref="ForbiddenException"/>.
    /// The blob store must NOT be called.
    /// </summary>
    [Fact]
    public async Task UploadPhoto_AsNonCreator_ThrowsForbiddenException()
    {
        // Arrange
        var creatorId = await SeedTestUserAsync();
        var strangerId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var pngBytes = MakePngBytes();

        var command = new UploadPlayRecordPhotoCommand(
            recordId, strangerId,
            new MemoryStream(pngBytes), pngBytes.Length,
            "image/png", false, null);

        // Act & Assert
        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<ForbiddenException>();

        // Verify no photo was written
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var count = await db.PlayRecordPhotos
            .CountAsync(p => p.PlayRecordId == recordId, TestCancellationToken);
        count.Should().Be(0);
    }

    /// <summary>
    /// Uploading the identical PNG bytes twice returns WasDeduplicated=true on the second
    /// call and leaves only one row in play_record_photos (aggregate-level SHA256 dedup).
    /// </summary>
    [Fact]
    public async Task UploadPhoto_SameBytes_SecondCallReturnsDeduplicated()
    {
        // Arrange — both uploads use byte-identical PNGs (same 10×10 blank image)
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var pngBytes = MakePngBytes();

        var cmd1 = new UploadPlayRecordPhotoCommand(
            recordId, creatorId,
            new MemoryStream(pngBytes), pngBytes.Length,
            "image/png", false, null);

        var result1 = await SendInScopeAsync(cmd1);
        result1.WasDeduplicated.Should().BeFalse();

        // Note: The second upload must create a FRESH MemoryStream from the same bytes
        // (the first command consumed the stream).
        var cmd2 = new UploadPlayRecordPhotoCommand(
            recordId, creatorId,
            new MemoryStream(pngBytes), pngBytes.Length,
            "image/png", false, null);

        // Act
        var result2 = await SendInScopeAsync(cmd2);

        // Assert
        result2.WasDeduplicated.Should().BeTrue();
        result2.PhotoId.Should().Be(result1.PhotoId);

        // Only ONE row in the DB
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var count = await db.PlayRecordPhotos
            .CountAsync(p => p.PlayRecordId == recordId, TestCancellationToken);
        count.Should().Be(1);
    }

    /// <summary>
    /// Verifies that the xmin optimistic-concurrency token prevents a stale write from
    /// succeeding. Load the same row into two separate domain aggregates, mutate+save the
    /// first (advancing the row's xmin in Postgres), then attempt to save the second —
    /// EF must throw <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
    /// because the second aggregate carries the old xmin value.
    /// ADR-060 / #2436 PR-B / #2437-1.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ConcurrentWrite_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange — seed a record so we have a row with a known xmin.
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);

        // Load scope 1 — aggregateA holds the initial xmin.
        Api.BoundedContexts.GameManagement.Domain.Entities.PlayRecord aggregateA;
        using (var scope = ServiceProvider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
            aggregateA = (await repo.GetByIdAsync(recordId, TestCancellationToken))!;
        }

        // Load scope 2 — aggregateB also holds the same initial xmin.
        Api.BoundedContexts.GameManagement.Domain.Entities.PlayRecord aggregateB;
        using (var scope = ServiceProvider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
            aggregateB = (await repo.GetByIdAsync(recordId, TestCancellationToken))!;
        }

        // Mutate + save aggregateA in its own scope → Postgres advances xmin for this row.
        using (var scope = ServiceProvider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork>();
            aggregateA.UpdateDetails(notes: "first writer wins", timeProvider: _timeProvider);
            await repo.UpdateAsync(aggregateA, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
        }

        // Act — attempt to save aggregateB (stale xmin) in a new scope.
        // EF should throw DbUpdateConcurrencyException because the xmin it sends
        // no longer matches the row's current xmin in Postgres.
        var act = async () =>
        {
            using var scope = ServiceProvider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork>();
            aggregateB.UpdateDetails(notes: "second writer should lose", timeProvider: _timeProvider);
            await repo.UpdateAsync(aggregateB, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
        };

        // Assert — the losing write must surface as a concurrency conflict (→ 409 via middleware).
        await act.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException>(
            "xmin mismatch means the row was already updated; EF must reject the stale write");
    }
}

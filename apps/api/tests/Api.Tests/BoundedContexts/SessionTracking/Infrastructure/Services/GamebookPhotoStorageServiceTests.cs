using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Services.Pdf;
using FluentAssertions;
using ImageMagick;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="GamebookPhotoStorageService"/>: EXIF strip + adapter over IBlobStorageService.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class GamebookPhotoStorageServiceTests
{
    // ── Fake IBlobStorageService ──────────────────────────────────────────────

    private sealed class FakeBlobStorage : IBlobStorageService
    {
        private readonly Dictionary<string, byte[]> _store = new();

        /// <summary>The last stream bytes passed to StoreAsync (for assertion).</summary>
        public byte[]? LastStoredBytes { get; private set; }

        public Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct = default)
        {
            _ = category;
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            LastStoredBytes = ms.ToArray();
            var fileId = Guid.NewGuid().ToString("N");
            _store[fileId] = LastStoredBytes;
            return Task.FromResult(new BlobStorageResult(true, fileId, $"{resourceKey}/{fileName}", LastStoredBytes.Length));
        }

        public Task<Stream?> RetrieveAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
        {
            _ = (category, resourceKey);
            if (_store.TryGetValue(fileId, out var bytes))
                return Task.FromResult<Stream?>(new MemoryStream(bytes));
            return Task.FromResult<Stream?>(null);
        }

        public Task<bool> DeleteAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
        {
            _ = (category, resourceKey);
            _store.Remove(fileId);
            return Task.FromResult(true);
        }

        public string GetStoragePath(string fileId, BlobCategory category, string resourceKey, string fileName)
        {
            _ = category;
            return $"{resourceKey}/{fileId}/{fileName}";
        }

        public Task<bool> ExistsAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken cancellationToken = default)
        {
            _ = (category, resourceKey);
            return Task.FromResult(_store.ContainsKey(fileId));
        }

        public Task<string?> GetPresignedDownloadUrlAsync(string fileId, BlobCategory category, string resourceKey, int? expirySeconds = null)
        {
            _ = (fileId, category, resourceKey, expirySeconds);
            return Task.FromResult<string?>(null);
        }

        public Task<bool> DeleteRawKeyAsync(string rawKey, CancellationToken ct = default)
        {
            _ = (rawKey, ct);
            return Task.FromResult(true);
        }

        public Task<bool> StoreRawKeyAsync(string rawKey, Stream stream, string contentType, CancellationToken ct = default)
        {
            _ = (rawKey, stream, contentType, ct);
            return Task.FromResult(true);
        }

        public Task<string?> GetPresignedUrlForRawKeyAsync(string rawKey, int? expirySeconds = null)
        {
            _ = (rawKey, expirySeconds);
            return Task.FromResult<string?>(null);
        }

        public Task<Stream?> RetrieveRawKeyAsync(string rawKey, CancellationToken ct = default)
        {
            _ = (rawKey, ct);
            return Task.FromResult<Stream?>(null);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a 64×64 JPEG stream with an EXIF GPS latitude tag embedded.
    /// ADR DEC-3d-1 (issue #2055 Phase G): Magick.NET 14.x replaces ImageSharp.
    /// </summary>
    private static MemoryStream BuildJpegWithExif()
    {
        using var img = new MagickImage(MagickColors.Black, 64u, 64u);

        // ExifProfile in Magick.NET 14.x: set the GPS latitude (rational triple
        // degrees/minutes/seconds) so the strip operation has something to remove.
        var exif = new ExifProfile();
        exif.SetValue(
            ExifTag.GPSLatitude,
            new Rational[] { new(48, 1), new(51, 1), new(30, 1) });
        img.SetProfile(exif);

        img.Format = MagickFormat.Jpeg;
        img.Quality = 90;
        var ms = new MemoryStream();
        img.Write(ms);
        ms.Position = 0;
        return ms;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_StripsExifFromStoredBytes()
    {
        // Arrange
        var fake = new FakeBlobStorage();
        var sut = new GamebookPhotoStorageService(fake);
        using var jpegWithExif = BuildJpegWithExif();

        // Act
        await sut.UploadAsync(jpegWithExif, "image/jpeg", Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        // Assert: reload stored bytes and confirm EXIF GPS is gone
        fake.LastStoredBytes.Should().NotBeNullOrEmpty();
        using var reloaded = new MagickImage(fake.LastStoredBytes!);
        // ImageMagick.Strip() removes all profiles; GetExifProfile() returns
        // null after the round-trip. Belt-and-braces: even if a residual EXIF
        // profile survives, the GPSLatitude tag MUST be absent.
        var exifProfile = reloaded.GetExifProfile();
        bool hasGps = exifProfile is not null && exifProfile.GetValue(ExifTag.GPSLatitude) is not null;
        hasGps.Should().BeFalse("EXIF GPS latitude must be stripped before storage");
    }

    [Fact]
    public async Task UploadAsync_ReturnedKey_RoundTripsViaRetrieve()
    {
        // Arrange
        var fake = new FakeBlobStorage();
        var sut = new GamebookPhotoStorageService(fake);
        using var jpeg = BuildJpegWithExif();
        var campaignId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        // Act
        var key = await sut.UploadAsync(jpeg, "image/jpeg", campaignId, photoId, CancellationToken.None);

        // Assert: key can be resolved back to a stream
        var retrieved = await sut.RetrieveAsync(key, CancellationToken.None);
        await using var _ = retrieved;
        retrieved.Should().NotBeNull();
        retrieved.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UploadAsync_EmptyCampaignId_ThrowsArgumentException()
    {
        // Arrange
        var sut = new GamebookPhotoStorageService(new FakeBlobStorage());
        using var jpeg = BuildJpegWithExif();

        // Act & Assert
        var act = () => sut.UploadAsync(jpeg, "image/jpeg", Guid.Empty, Guid.NewGuid(), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*campaignId*");
    }

    [Fact]
    public async Task StorageKey_ContainsCampaignIdPrefix()
    {
        // Arrange
        var fake = new FakeBlobStorage();
        var sut = new GamebookPhotoStorageService(fake);
        using var jpeg = BuildJpegWithExif();
        var campaignId = Guid.NewGuid();

        // Act
        var key = await sut.UploadAsync(jpeg, "image/jpeg", campaignId, Guid.NewGuid(), CancellationToken.None);

        // Assert: key encodes campaignId for routing
        key.Should().Contain(campaignId.ToString("N"), "storage key must embed campaignId for retrieval routing");
        key.Should().Contain("|", "storage key must use | delimiter to separate gameId from fileId");
    }
}

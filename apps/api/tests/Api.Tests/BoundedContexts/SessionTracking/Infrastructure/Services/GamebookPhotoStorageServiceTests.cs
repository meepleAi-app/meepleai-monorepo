using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Services.Pdf;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
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

        public Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct = default)
        {
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            LastStoredBytes = ms.ToArray();
            var fileId = Guid.NewGuid().ToString("N");
            _store[fileId] = LastStoredBytes;
            return Task.FromResult(new BlobStorageResult(true, fileId, $"{gameId}/{fileName}", LastStoredBytes.Length));
        }

        public Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct = default)
        {
            if (_store.TryGetValue(fileId, out var bytes))
                return Task.FromResult<Stream?>(new MemoryStream(bytes));
            return Task.FromResult<Stream?>(null);
        }

        public Task<bool> DeleteAsync(string fileId, string gameId, CancellationToken ct = default)
        {
            _store.Remove(fileId);
            return Task.FromResult(true);
        }

        public string GetStoragePath(string fileId, string gameId, string fileName) => $"{gameId}/{fileId}/{fileName}";

        public Task<bool> ExistsAsync(string fileId, string gameId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.ContainsKey(fileId));

        public Task<string?> GetPresignedDownloadUrlAsync(string fileId, string gameId, int? expirySeconds = null)
            => Task.FromResult<string?>(null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a 64×64 JPEG stream with an EXIF GPS latitude tag embedded.
    /// </summary>
    private static MemoryStream BuildJpegWithExif()
    {
        using var img = new Image<Rgb24>(64, 64);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.GPSLatitude, new Rational[] { new(48, 1), new(51, 1), new(30, 1) });
        img.Metadata.ExifProfile = exif;
        var ms = new MemoryStream();
        img.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
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
        using var reloaded = Image.Load(fake.LastStoredBytes!);
        var exifProfile = reloaded.Metadata.ExifProfile;
        // After stripping, either no profile or no GPS tag present
        bool hasGps = exifProfile != null && exifProfile.TryGetValue(ExifTag.GPSLatitude, out _);
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

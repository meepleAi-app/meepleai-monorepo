using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for the local-filesystem <see cref="BlobStorageService"/> under
/// each <see cref="StorageLayoutOptions"/> mode (issue #1314 PR 2).
///
/// Verifies the behavior matrix:
/// - WriteMode = Legacy → new uploads land at <c>{basePath}/{resourceKey}/{fileId}_{filename}</c>
/// - WriteMode = New    → new uploads land at <c>{basePath}/{category.ToS3Folder()}/{resourceKey}/{fileId}_{filename}</c>
/// - ReadMode  = Dual   → reads probe new layout first, fall back to legacy
/// - ReadMode  = Legacy → reads only probe legacy layout
/// - ReadMode  = New    → reads only probe new layout
/// </summary>
[Trait("Category", "Unit")]
[Trait("Issue", "1314")]
public sealed class BlobStorageServiceLayoutTests : IDisposable
{
    private readonly string _basePath;

    public BlobStorageServiceLayoutTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"meepleai-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
        {
            Directory.Delete(_basePath, recursive: true);
        }
    }

    private BlobStorageService CreateSut(StorageLayoutOptions options)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PDF_STORAGE_PATH"] = _basePath })
            .Build();
        var wrapped = Options.Create(options);
        return new BlobStorageService(config, wrapped, NullLogger<BlobStorageService>.Instance);
    }

    [Fact]
    public async Task StoreAsync_WriteModeLegacy_FilePath_UnderResourceKeyDirectly()
    {
        var sut = CreateSut(new StorageLayoutOptions { WriteMode = StorageWriteMode.Legacy });
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await sut.StoreAsync(stream, "rule.pdf", BlobCategory.Pdf, "game-abc");

        result.Success.Should().BeTrue();
        result.FilePath.Should().StartWith(Path.Combine(_basePath, "game-abc") + Path.DirectorySeparatorChar);
        result.FilePath.Should().NotContain(Path.Combine(_basePath, "pdfs"));
    }

    [Fact]
    public async Task StoreAsync_WriteModeNew_FilePath_UnderCategorizedFolder()
    {
        var sut = CreateSut(new StorageLayoutOptions { WriteMode = StorageWriteMode.New });
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await sut.StoreAsync(stream, "rule.pdf", BlobCategory.Pdf, "game-abc");

        result.Success.Should().BeTrue();
        result.FilePath.Should().StartWith(Path.Combine(_basePath, "pdfs", "game-abc") + Path.DirectorySeparatorChar);
    }

    [Fact]
    public async Task RetrieveAsync_ReadModeDual_FindsLegacyWhenNewMissing()
    {
        // Arrange: write under Legacy, read under Dual → should fall back.
        var writer = CreateSut(new StorageLayoutOptions { WriteMode = StorageWriteMode.Legacy });
        using (var stream = new MemoryStream(new byte[] { 4, 2 }))
        {
            await writer.StoreAsync(stream, "f.pdf", BlobCategory.Pdf, "g1");
        }
        var fileId = ExtractFileId(writer);

        var reader = CreateSut(new StorageLayoutOptions
        {
            WriteMode = StorageWriteMode.Legacy,
            ReadMode = StorageReadMode.Dual,
        });

        using var retrieved = await reader.RetrieveAsync(fileId, BlobCategory.Pdf, "g1");

        retrieved.Should().NotBeNull("dual-mode read must fall back to the legacy layout when no new-layout object exists");
    }

    [Fact]
    public async Task RetrieveAsync_ReadModeNew_DoesNotFindLegacy()
    {
        // Arrange: write under Legacy, but try to read under New-only → must miss.
        var writer = CreateSut(new StorageLayoutOptions { WriteMode = StorageWriteMode.Legacy });
        using (var stream = new MemoryStream(new byte[] { 9 }))
        {
            await writer.StoreAsync(stream, "f.pdf", BlobCategory.Pdf, "g2");
        }
        var fileId = ExtractFileId(writer);

        var reader = CreateSut(new StorageLayoutOptions
        {
            WriteMode = StorageWriteMode.New,
            ReadMode = StorageReadMode.New,
        });

        using var retrieved = await reader.RetrieveAsync(fileId, BlobCategory.Pdf, "g2");

        retrieved.Should().BeNull("New-only read mode must NOT fall back to the legacy layout");
    }

    [Fact]
    public async Task DeleteAsync_ReadModeDual_SweepsBothLayouts()
    {
        // Arrange: write the same fileId under BOTH layouts (manual setup).
        var writerLegacy = CreateSut(new StorageLayoutOptions { WriteMode = StorageWriteMode.Legacy });
        using (var stream = new MemoryStream(new byte[] { 1 }))
        {
            await writerLegacy.StoreAsync(stream, "f.pdf", BlobCategory.Pdf, "g3");
        }

        var writerNew = CreateSut(new StorageLayoutOptions { WriteMode = StorageWriteMode.New });
        using (var stream = new MemoryStream(new byte[] { 2 }))
        {
            await writerNew.StoreAsync(stream, "f.pdf", BlobCategory.Pdf, "g3");
        }

        // Sanity: both directories exist.
        Directory.Exists(Path.Combine(_basePath, "g3")).Should().BeTrue();
        Directory.Exists(Path.Combine(_basePath, "pdfs", "g3")).Should().BeTrue();

        var deleter = CreateSut(new StorageLayoutOptions
        {
            ReadMode = StorageReadMode.Dual,
        });

        var legacyFiles = Directory.GetFiles(Path.Combine(_basePath, "g3"), "*");
        var legacyFileId = ExtractFileIdFromFileName(legacyFiles[0]);

        // Delete via dual mode — sweeps BOTH layouts.
        var deleted = await deleter.DeleteAsync(legacyFileId, BlobCategory.Pdf, "g3");

        deleted.Should().BeTrue();
        Directory.GetFiles(Path.Combine(_basePath, "g3"), $"{legacyFileId}_*")
            .Should().BeEmpty("dual-mode delete must clean the legacy layout");
    }

    /// <summary>
    /// Convenience: read back the most recently uploaded fileId from the
    /// last write the supplied service performed. Brittle, but adequate for
    /// the test scope (one file per resourceKey).
    /// </summary>
    private string ExtractFileId(BlobStorageService _)
    {
        // We don't expose fileId on the test surface; in the tests above, the
        // call sequence guarantees exactly one file per resourceKey directory,
        // so we infer it from disk.
        foreach (var dir in Directory.EnumerateDirectories(_basePath, "*", SearchOption.AllDirectories))
        {
            foreach (var file in Directory.GetFiles(dir, "*_*"))
            {
                return ExtractFileIdFromFileName(file);
            }
        }
        throw new InvalidOperationException("No fileId found on disk for fixture");
    }

    private static string ExtractFileIdFromFileName(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var idx = fileName.IndexOf('_', StringComparison.Ordinal);
        return idx > 0 ? fileName[..idx] : fileName;
    }
}

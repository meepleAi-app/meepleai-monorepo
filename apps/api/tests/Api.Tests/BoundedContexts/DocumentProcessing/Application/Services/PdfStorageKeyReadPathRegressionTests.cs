using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Issue #2671 regression: the PDF blob READ path must recover the RANDOM fileId that
/// <see cref="IBlobStorageService.StoreAsync"/> embeds in the returned <c>FilePath</c>.
/// Reconstructing the fileId from <see cref="PdfStorageKey.ForPdf"/> (which is only the
/// resourceKey folder) never matches the stored blob, so extraction 404s.
/// Exercised end-to-end against the real local <see cref="BlobStorageService"/> (filesystem,
/// no Docker/S3) which shares the exact <c>{resourceKey}/{randomFileId}_{name}</c> layout
/// with <see cref="S3BlobStorageService"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfStorageKeyReadPathRegressionTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly BlobStorageService _storage;

    public PdfStorageKeyReadPathRegressionTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "meepleai-2671-" + Guid.NewGuid().ToString("N"));
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["PDF_STORAGE_PATH"]).Returns(_tempRoot);
        _storage = new BlobStorageService(config.Object, NullLogger<BlobStorageService>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
#pragma warning disable CA1031 // best-effort temp cleanup
        catch
        {
            // Ignore cleanup failures (locked file / antivirus) — temp dir, not load-bearing.
        }
#pragma warning restore CA1031
    }

    [Fact]
    public async Task ReadPath_RecoversRandomFileIdFromFilePath_FindsBlob()
    {
        // Arrange — WRITE: StoreAsync generates a random fileId (≠ pdfId), persisted in FilePath.
        var pdfId = Guid.NewGuid();
        var resourceKey = PdfStorageKey.ForPdf(pdfId);
        var content = "the-real-pdf-bytes"u8.ToArray();

        BlobStorageResult writeResult;
        using (var ms = new MemoryStream(content))
        {
            writeResult = await _storage.StoreAsync(ms, "file", BlobCategory.Pdf, resourceKey, CancellationToken.None);
        }

        writeResult.Success.Should().BeTrue();
        writeResult.FileId.Should().NotBeNullOrEmpty();
        writeResult.FileId.Should().NotBe(resourceKey, "the write fileId is a random GUID, not the pdfId");
        writeResult.FilePath.Should().NotBeNullOrEmpty();

        // Act — FIXED READ path (Issue #2671): recover fileId from FilePath; resourceKey = ForPdf(Id).
        var recoveredFileId = PdfStorageKey.FileIdFromPath(writeResult.FilePath);
        recoveredFileId.Should().Be(writeResult.FileId);

        var stream = await _storage.RetrieveAsync(recoveredFileId!, BlobCategory.Pdf, resourceKey, CancellationToken.None);

        // Assert — the fixed path locates the blob and returns the exact bytes written.
        stream.Should().NotBeNull("the fixed READ path must locate the blob written by StoreAsync");
        await using (stream!.ConfigureAwait(false))
        {
            using var read = new MemoryStream();
            await stream.CopyToAsync(read, CancellationToken.None);
            read.ToArray().Should().Equal(content);
        }
    }

    [Fact]
    public async Task ReadPath_OldBehaviour_UsingForPdfAsFileId_DoesNotFindBlob_RedProof()
    {
        // Arrange — same WRITE as the fixed test.
        var pdfId = Guid.NewGuid();
        var resourceKey = PdfStorageKey.ForPdf(pdfId);
        using (var ms = new MemoryStream("bytes"u8.ToArray()))
        {
            var writeResult = await _storage.StoreAsync(ms, "file", BlobCategory.Pdf, resourceKey, CancellationToken.None);
            writeResult.Success.Should().BeTrue();
            writeResult.FileId.Should().NotBe(resourceKey);
        }

        // Act — OLD (pre-#2671, buggy) READ path: ForPdf(Id) passed as BOTH fileId and resourceKey.
        var buggyFileId = PdfStorageKey.ForPdf(pdfId);
        var stream = await _storage.RetrieveAsync(buggyFileId, BlobCategory.Pdf, buggyFileId, CancellationToken.None);

        // Assert — RED proof: the old fileId==pdfId lookup never matches the random StoreAsync fileId.
        stream.Should().BeNull("the buggy fileId==pdfId lookup cannot match the random StoreAsync fileId");
    }

    [Fact]
    public async Task ReadPath_LegacyEmptyFilePath_FallsBackToForPdf_FindsBlob()
    {
        // A legacy blob written the pre-#2671 way (fileId == pdfId) with a record whose FilePath is
        // empty/unparsable must still resolve via the ForPdf(Id) fallback (`FileIdFromPath(x) ?? ForPdf(Id)`).
        var pdfId = Guid.NewGuid();
        var resourceKey = PdfStorageKey.ForPdf(pdfId);
        var legacyContent = "legacy-pdf-bytes"u8.ToArray();

        // Materialise a legacy on-disk blob whose fileId segment IS the pdfId, using the service's
        // own path contract (avoids hard-coding the internal on-disk layout).
        var legacyBlobPath = _storage.GetStoragePath(resourceKey, BlobCategory.Pdf, resourceKey, "file.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyBlobPath)!);
        await File.WriteAllBytesAsync(legacyBlobPath, legacyContent, CancellationToken.None);

        // Act — READ path with an empty FilePath → helper returns null → fallback to ForPdf(Id).
        var fileId = PdfStorageKey.FileIdFromPath(string.Empty) ?? resourceKey;
        fileId.Should().Be(resourceKey, "empty FilePath must fall back to the pdfId-derived key");

        var stream = await _storage.RetrieveAsync(fileId, BlobCategory.Pdf, resourceKey, CancellationToken.None);

        // Assert — the fallback preserves the pre-#2671 behaviour for legacy records.
        stream.Should().NotBeNull("legacy records without a parsable FilePath must resolve via the ForPdf(Id) fallback");
        await using (stream!.ConfigureAwait(false))
        {
            using var read = new MemoryStream();
            await stream.CopyToAsync(read, CancellationToken.None);
            read.ToArray().Should().Equal(legacyContent);
        }
    }
}

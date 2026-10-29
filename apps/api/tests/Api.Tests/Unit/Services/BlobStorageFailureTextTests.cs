using Amazon.S3;
using Amazon.S3.Model;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Services;

/// <summary>
/// #4660 — il messaggio di un <c>StoreAsync</c> fallito arriva a chi ha caricato (il risultato dell'upload
/// standard e privato, la sessione del caricamento a pezzi): è un testo fisso, mai il messaggio dell'eccezione, che
/// può nominare un host, un bucket o un percorso. Il dettaglio sta nel log, dove i servizi lo scrivono già con
/// l'eccezione.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4660")]
public sealed class BlobStorageFailureTextTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "meepleai-4660-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    // ── S3 ──────────────────────────────────────────────────────────────────────

    private static (S3BlobStorageService Sut, Mock<IAmazonS3> Client, RecordingLogger<S3BlobStorageService> Log) CreateS3()
    {
        var client = new Mock<IAmazonS3>();
        var log = new RecordingLogger<S3BlobStorageService>();
        var options = new S3StorageOptions
        {
            Endpoint = "https://test.r2.cloudflarestorage.com",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
            BucketName = "secret-bucket-name",
            Region = "auto",
            PresignedUrlExpirySeconds = 3600,
            EnableEncryption = false,
            ForcePathStyle = false,
        };
        return (new S3BlobStorageService(client.Object, options, log), client, log);
    }

    [Fact]
    public async Task S3_WhenTheServiceRejectsTheWrite_ReturnsTheFixedText_AndLogsTheDetail()
    {
        var (sut, client, log) = CreateS3();
        client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("The bucket secret-bucket-name rejected the key at 10.0.0.9"));
        using var stream = new MemoryStream("PDF"u8.ToArray());

        var result = await sut.StoreAsync(stream, "rules.pdf", BlobCategory.Pdf, "game123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(BlobStorageFailureText.S3);
        result.ErrorMessage.Should().NotContain("secret-bucket-name").And.NotContain("10.0.0.9");
        log.Entries.Should().Contain(l => l.StartsWith("Error:", StringComparison.Ordinal) && l.Contains("10.0.0.9", StringComparison.Ordinal),
            "the detail is in the log, as an error, with the exception");
    }

    [Fact]
    public async Task S3_WhenSomethingUnexpectedThrows_ReturnsTheFixedText_AndLogsTheDetail()
    {
        var (sut, client, log) = CreateS3();
        client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection to 10.0.0.9:9000 refused"));
        using var stream = new MemoryStream("PDF"u8.ToArray());

        var result = await sut.StoreAsync(stream, "rules.pdf", BlobCategory.Pdf, "game123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(BlobStorageFailureText.Generic);
        result.ErrorMessage.Should().NotContain("10.0.0.9");
        log.Entries.Should().Contain(l => l.StartsWith("Error:", StringComparison.Ordinal) && l.Contains("10.0.0.9", StringComparison.Ordinal));
    }

    // ── disco locale ────────────────────────────────────────────────────────────

    private (BlobStorageService Sut, RecordingLogger<BlobStorageService> Log) CreateLocal()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["PDF_STORAGE_PATH"]).Returns(_tempRoot);
        var log = new RecordingLogger<BlobStorageService>();
        return (new BlobStorageService(config.Object, log), log);
    }

    /// <summary>
    /// Un file dove dovrebbe stare la cartella della risorsa: <c>Directory.CreateDirectory</c> lancia una
    /// <c>IOException</c> il cui messaggio nomina il percorso intero.
    /// </summary>
    [Fact]
    public async Task Local_WhenTheDiskRefusesTheWrite_ReturnsTheFixedText_AndLogsTheDetail()
    {
        var (sut, log) = CreateLocal();
        var categoryDir = Path.Combine(_tempRoot, BlobCategory.Pdf.ToS3Folder());
        Directory.CreateDirectory(categoryDir);
        await File.WriteAllTextAsync(Path.Combine(categoryDir, "game123"), "a file where the directory should be");
        using var stream = new MemoryStream("PDF"u8.ToArray());

        var result = await sut.StoreAsync(stream, "rules.pdf", BlobCategory.Pdf, "game123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(BlobStorageFailureText.IO);
        result.ErrorMessage.Should().NotContain(_tempRoot);
        log.Entries.Should().Contain(l => l.StartsWith("Error:", StringComparison.Ordinal) && l.Contains(_tempRoot, StringComparison.Ordinal),
            "the path is in the log, with the exception");
    }

    [Fact]
    public async Task Local_WhenTheResourceKeyIsRejected_ReturnsTheFixedText_NotTheValidationMessage()
    {
        var (sut, log) = CreateLocal();
        using var stream = new MemoryStream("PDF"u8.ToArray());

        var result = await sut.StoreAsync(stream, "rules.pdf", BlobCategory.Pdf, "../../etc/passwd");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(BlobStorageFailureText.Generic);
        result.ErrorMessage.Should().NotContain("passwd");
        log.Entries.Should().Contain(l => l.StartsWith("Error:", StringComparison.Ordinal));
    }
}

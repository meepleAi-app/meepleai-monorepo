using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #4442 con lo storage locale, su cui la copia della pipeline rimossa funzionava: il passaggio alla
/// coda non deve romperlo. I test stanno in <see cref="CompleteChunkedUploadIntegrationTestsBase"/>.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4442")]
public sealed class CompleteChunkedUploadLocalStorageIntegrationTests(SharedTestcontainersFixture fixture)
    : CompleteChunkedUploadIntegrationTestsBase(fixture)
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"meepleai_4442_{Guid.NewGuid():N}");

    protected override string StorageName => "local";

    protected override Task StartStorageAsync()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PDF_STORAGE_PATH"] = _storageRoot })
            .Build();
        Storage = new BlobStorageService(config, NullLogger<BlobStorageService>.Instance);
        return Task.CompletedTask;
    }

    protected override ValueTask DisposeStorageAsync()
    {
        try
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Niente da pulire.
        }

        return ValueTask.CompletedTask;
    }

    protected override void RequireStorage()
    {
        // Una cartella temporanea: non c'è un servizio che possa mancare.
    }

    protected override void AssertFilePathShape(string filePath)
    {
        Path.IsPathRooted(filePath).Should().BeTrue("con lo storage locale il FilePath è il percorso del file");
        File.Exists(filePath).Should().BeTrue();
    }

    protected override Task<IReadOnlyList<string>> ListStoredPdfsAsync()
    {
        var gameDir = Path.Combine(_storageRoot, BlobCategory.Pdf.ToS3Folder(), TestSharedGameId.ToString());
        IReadOnlyList<string> files = Directory.Exists(gameDir) ? Directory.GetFiles(gameDir) : [];
        return Task.FromResult(files);
    }
}

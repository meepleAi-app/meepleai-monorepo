using Api.Services.Pdf;
using Api.Tests.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #5082 with the local storage. The tests are in <see cref="ShareRequestDocumentCopyIntegrationTestsBase"/>.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "5082")]
public sealed class ShareRequestDocumentCopyLocalStorageIntegrationTests : ShareRequestDocumentCopyIntegrationTestsBase
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"meepleai_5082_{Guid.NewGuid():N}");

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
            // Nothing was written.
        }

        return ValueTask.CompletedTask;
    }

    protected override void RequireStorage()
    {
        // The local storage has no service to start.
    }
}

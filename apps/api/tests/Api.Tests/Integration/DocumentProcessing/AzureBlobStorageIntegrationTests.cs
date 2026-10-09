using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>Ciclo completo Store → Exists → Retrieve → SAS → Delete contro Azurite vero (un container per classe).</summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Feature", "AzureBlobStorage")]
public sealed class AzureBlobStorageIntegrationTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _azurite;

    public AzureBlobStorageIntegrationTests(AzuriteFixture azurite)
    {
        _azurite = azurite;
    }

    private AzureBlobStorageService CreateSut()
    {
        var serviceClient = new BlobServiceClient(_azurite.ConnectionString);
        var container = serviceClient.GetBlobContainerClient(TestcontainersConfiguration.AzuriteTestContainer);
        AzureStorageConnectionString.TryParse(_azurite.ConnectionString, out var accountName, out var accountKey);
        return new AzureBlobStorageService(serviceClient, container, new AzureBlobStorageOptions
        {
            ConnectionString = _azurite.ConnectionString,
            ContainerName = TestcontainersConfiguration.AzuriteTestContainer,
            PublicBlobEndpoint = _azurite.PublicEndpoint,
            SasExpirySeconds = 300,
            AccountName = accountName,
            AccountKey = accountKey,
        }, Mock.Of<ILogger<AzureBlobStorageService>>());
    }

    [Fact]
    public async Task StoreExistsRetrieveSignDelete_RoundTrip()
    {
        _azurite.SkipIfUnavailable();
        var sut = CreateSut();
        var payload = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
        using var content = new MemoryStream(payload);

        var stored = await sut.StoreAsync(content, "rulebook.pdf", BlobCategory.Pdf, "game-rt");
        stored.Success.Should().BeTrue(stored.ErrorMessage);

        (await sut.ExistsAsync(stored.FileId!, BlobCategory.Pdf, "game-rt")).Should().BeTrue();

        await using (var retrieved = await sut.RetrieveAsync(stored.FileId!, BlobCategory.Pdf, "game-rt"))
        {
            retrieved.Should().NotBeNull();
            using var copy = new MemoryStream();
            await retrieved!.CopyToAsync(copy);
            copy.ToArray().Should().Equal(payload);
        }

        var url = await sut.GetPresignedDownloadUrlAsync(stored.FileId!, BlobCategory.Pdf, "game-rt");
        url.Should().NotBeNull().And.StartWith(_azurite.PublicEndpoint);
        using (var http = new HttpClient())
        {
            (await http.GetByteArrayAsync(new Uri(url!))).Should().Equal(payload);
        }

        (await sut.DeleteAsync(stored.FileId!, BlobCategory.Pdf, "game-rt")).Should().BeTrue();
        (await sut.ExistsAsync(stored.FileId!, BlobCategory.Pdf, "game-rt")).Should().BeFalse();
    }

    [Fact]
    public async Task RawKey_StoreSignRetrieveDelete_RoundTrip()
    {
        _azurite.SkipIfUnavailable();
        var sut = CreateSut();
        var payload = new byte[] { 1, 2, 3, 4 };
        using var content = new MemoryStream(payload);

        (await sut.StoreRawKeyAsync("covers/game-rt/cover.png", content, "image/png")).Should().BeTrue();

        var url = await sut.GetPresignedUrlForRawKeyAsync("covers/game-rt/cover.png", 60);
        url.Should().NotBeNull().And.Contain("covers/game-rt/cover.png?");
        using (var http = new HttpClient())
        {
            (await http.GetByteArrayAsync(new Uri(url!))).Should().Equal(payload);
        }

        await using (var retrieved = await sut.RetrieveRawKeyAsync("covers/game-rt/cover.png"))
        {
            using var copy = new MemoryStream();
            await retrieved!.CopyToAsync(copy);
            copy.ToArray().Should().Equal(payload);
        }

        (await sut.DeleteRawKeyAsync("covers/game-rt/cover.png")).Should().BeTrue();
        (await sut.GetPresignedUrlForRawKeyAsync("covers/game-rt/cover.png")).Should().BeNull();
    }
}

using Api.Infrastructure.AzureSdk;
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Services.Pdf;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class BlobStorageServiceFactoryAzureTests
{
    private static ServiceProvider Build(params (string Key, string? Value)[] pairs)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddAzureSdkHttpClients(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Create_AzureBlobWithoutEndpoint_Throws()
    {
        using var sp = Build(("STORAGE_PROVIDER", "azureblob"), ("MEEPLEAI_PROFILE", "azure-local"));

        var act = () => BlobStorageServiceFactory.Create(sp);

        act.Should().Throw<InvalidOperationException>().WithMessage("*AZURE_STORAGE_CONNECTION_STRING*AZURE_STORAGE_ACCOUNT_URL*");
    }

    [Fact]
    public void Create_AzureBlobWithConnectionString_ReturnsAzureServiceWithParsedKey()
    {
        using var sp = Build(
            ("STORAGE_PROVIDER", "AzureBlob"),
            ("MEEPLEAI_PROFILE", "azure-local"),
            ("AZURE_STORAGE_CONNECTION_STRING", "UseDevelopmentStorage=true"),
            ("AZURE_STORAGE_CONTAINER", "uploads-test"),
            ("AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT", "http://localhost:20000"),
            ("AZURE_STORAGE_SAS_EXPIRY", "120"));

        var azure = BlobStorageServiceFactory.Create(sp).Should().BeOfType<AzureBlobStorageService>().Subject;

        azure.Options.ContainerName.Should().Be("uploads-test");
        azure.Options.PublicBlobEndpoint.Should().Be("http://localhost:20000");
        azure.Options.SasExpirySeconds.Should().Be(120);
        azure.Options.AccountName.Should().Be("devstoreaccount1");
        azure.Options.AccountKey.Should().Be(AzureStorageConnectionString.DevelopmentStorageKey);
    }

    [Fact]
    public void Create_UnknownProvider_StillFallsBackToLocal()
    {
        using var sp = Build(("STORAGE_PROVIDER", "nope"));

        BlobStorageServiceFactory.Create(sp).Should().BeOfType<BlobStorageService>();
    }
}

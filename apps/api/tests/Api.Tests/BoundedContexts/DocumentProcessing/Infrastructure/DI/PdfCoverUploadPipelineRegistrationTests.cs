using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.DI;

/// <summary>
/// Issue #3363 regression: the R2 <see cref="IPdfCoverUploadPipeline"/> factory hard-throws
/// "S3_ENDPOINT is required" at DI resolution. Because <c>PdfProcessingPipelineService</c> resolves
/// it during every PDF upload, the previous UNCONDITIONAL registration failed every upload in any
/// local-storage environment (the CI seed-snapshot bake + <c>make dev</c> locally). It must be
/// registered ONLY when <c>STORAGE_PROVIDER=s3</c> (mirroring <c>BlobStorageServiceFactory</c>); in
/// local mode it stays unregistered so the optional injection resolves to null and the best-effort
/// cover step is skipped.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3363")]
public sealed class PdfCoverUploadPipelineRegistrationTests
{
    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string> overrides)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();

        var settings = new Dictionary<string, string>
        {
            ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
            ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = "http://test:8002",
            ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
            ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
            ["PdfProcessing:Quality:MinCharsPerPage"] = "500",
        };
        foreach (var kv in overrides)
        {
            settings[kv.Key] = kv.Value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings!).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<ITextChunkingService, TextChunkingService>();

        services.AddDocumentProcessingContext(configuration);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("local")]
    [InlineData("")] // unset → the ?? "local" default path
    public void LocalStorage_DoesNotRegisterCoverUploadPipeline(string storageProvider)
    {
        var overrides = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(storageProvider))
        {
            overrides["STORAGE_PROVIDER"] = storageProvider;
        }

        using var provider = BuildProvider(overrides);

        provider.GetService<IPdfCoverUploadPipeline>().Should().BeNull(
            "in local-storage mode the R2 cover pipeline must stay unregistered so PDF ingestion skips " +
            "the cover instead of throwing 'S3_ENDPOINT is required' at resolution");
    }

    [Fact]
    public void S3Storage_RegistersCoverUploadPipeline()
    {
        using var provider = BuildProvider(new Dictionary<string, string>
        {
            ["STORAGE_PROVIDER"] = "s3",
            ["S3_ENDPOINT"] = "https://r2.example.com",
            ["S3_ACCESS_KEY"] = "ak",
            ["S3_SECRET_KEY"] = "sk",
            ["S3_BUCKET_NAME"] = "covers",
        });

        // GetService resolves the singleton factory (constructs the AmazonS3Client — no network I/O),
        // so this also proves the S3-mode factory does not throw with valid S3_* config.
        provider.GetService<IPdfCoverUploadPipeline>().Should().NotBeNull(
            "STORAGE_PROVIDER=s3 with S3_* config must register the R2 cover pipeline");
    }
}

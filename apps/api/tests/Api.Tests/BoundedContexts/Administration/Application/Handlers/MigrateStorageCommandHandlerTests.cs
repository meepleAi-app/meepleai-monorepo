using Api.BoundedContexts.Administration.Application.Commands;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class MigrateStorageCommandHandlerTests
{
    /// <summary>
    /// Defense-in-depth PII / info-leak guard for the per-file catch block.
    /// Mirrors the pattern enforced by
    /// <c>BulkImportUsersCommandHandlerTests.Handle_WhenPerLineImportFails_ShouldNotIncludeEmailInError</c>.
    /// The returned <c>MigrateStorageResult.Errors</c> payload is exposed to admin API callers
    /// and propagates into log shipping pipelines (Loki/Datadog/etc.), so it must never carry
    /// raw <c>ex.Message</c> content — S3/cloud provider exception messages routinely include
    /// bucket names, credential fragments, request IDs, and other internal infrastructure data.
    /// </summary>
    [Fact]
    public async Task Handle_WhenStorageServiceThrows_ShouldNotIncludeRawExceptionMessageInError()
    {
        // Arrange — real temp directory so Directory.GetFiles enumerates exactly one file
        // with the expected pdf_uploads/{gameId}/{fileId}_{filename} layout.
        var tempDir = Path.Combine(Path.GetTempPath(), $"meeple-migrate-test-{Guid.NewGuid()}");
        var gameId = Guid.NewGuid().ToString();
        Directory.CreateDirectory(Path.Combine(tempDir, gameId));
        var fileId = Guid.NewGuid().ToString();
        var filePath = Path.Combine(tempDir, gameId, $"{fileId}_rules.pdf");
        await File.WriteAllTextAsync(filePath, "fake pdf data");

        try
        {
            const string sensitiveProviderMessage =
                "SECRET-MARKER bucket=internal-prod-bucket credential=AKIA-leaky-fragment";

            var configValues = new Dictionary<string, string?>
            {
                ["STORAGE_PROVIDER"] = "s3",
                ["PDF_STORAGE_PATH"] = tempDir
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

            var mockStorage = new Mock<IBlobStorageService>();
            mockStorage
                .Setup(s => s.ExistsAsync(
                    It.IsAny<string>(),
                    It.IsAny<BlobCategory>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException(sensitiveProviderMessage));

            var handler = new MigrateStorageCommandHandler(
                mockStorage.Object,
                config,
                Mock.Of<ILogger<MigrateStorageCommandHandler>>());

            // Act
            var result = await handler.Handle(
                new MigrateStorageCommand(DryRun: false),
                CancellationToken.None);

            // Assert
            result.Errors.Should().HaveCount(1,
                because: "the single mock file should produce exactly one per-file error");

            result.Errors[0].Should().NotContain("SECRET-MARKER",
                because: "raw infrastructure exception messages (bucket names, credential " +
                         "fragments, internal endpoints) must not be forwarded to API callers " +
                         "via MigrateStorageResult.Errors — the full message + stack trace " +
                         "are already captured server-side via ILogger.LogWarning");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}

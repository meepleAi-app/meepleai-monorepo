using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services.Pdf;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #5082: the documents attached to a share request are copied to the shared game through the blob storage that holds
/// the PDFs, local or S3. The copy went through a local-only service that read <c>FilePath</c> as a path on disk: with
/// the PDFs on S3 it found nothing. The subclasses run these tests on each storage.
/// </summary>
public abstract class ShareRequestDocumentCopyIntegrationTestsBase : IAsyncLifetime
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal IBlobStorageService? Storage { get; set; }

    protected abstract Task StartStorageAsync();

    protected abstract ValueTask DisposeStorageAsync();

    /// <summary>Fails the test when the storage did not start (an L1 service fails, it does not skip).</summary>
    protected abstract void RequireStorage();

    public async ValueTask InitializeAsync() => await StartStorageAsync();

    public async ValueTask DisposeAsync()
    {
        await DisposeStorageAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Copy_StoresTheFileUnderTheSharedGame_AndTheCopyReadsBackTheSameBytes()
    {
        RequireStorage();
        var bytes = "%PDF-1.4 a rulebook attached to a share request"u8.ToArray();
        var source = await StoreSourceAsync(bytes);
        var sharedGameId = Guid.NewGuid();
        var (service, added) = ServiceOver(source);

        var copied = await service.CopyDocumentsToSharedGame([source.Id], sharedGameId, Guid.NewGuid(), Ct);

        copied.Should().ContainSingle();
        var copy = added.Should().ContainSingle().Which;
        copy.Id.Should().Be(copied[0]);
        PdfStorageKey.ResourceKeyFromPath(copy.FilePath).Should().Be($"shared-game-{sharedGameId}");
        (await ReadAsync(copy.FilePath)).Should().Equal(bytes);
        // A copy, not a move: the contributor's document keeps its file.
        (await ReadAsync(source.FilePath)).Should().Equal(bytes);
    }

    [Fact]
    public async Task Copy_SourceNotInStorage_CopiesNothing()
    {
        RequireStorage();
        var missing = new PdfDocumentBuilder()
            .WithFileName("gone.pdf")
            .WithFilePath($"pdfs/{Guid.NewGuid():N}/{Guid.NewGuid():N}_gone.pdf")
            .Build();
        var (service, added) = ServiceOver(missing);

        var copied = await service.CopyDocumentsToSharedGame([missing.Id], Guid.NewGuid(), Guid.NewGuid(), Ct);

        copied.Should().BeEmpty();
        added.Should().BeEmpty();
    }

    private async Task<PdfDocument> StoreSourceAsync(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        // Where a contributor's private PDF lands: the private game's id as resource key.
        var stored = await Storage!.StoreAsync(stream, "rules.pdf", BlobCategory.Pdf, Guid.NewGuid().ToString(), Ct);
        stored.Success.Should().BeTrue(stored.ErrorMessage);
        return new PdfDocumentBuilder()
            .WithFileName("rules.pdf")
            .WithFilePath(stored.FilePath!)
            .WithFileSize(bytes.Length)
            .Build();
    }

    private async Task<byte[]> ReadAsync(string filePath)
    {
        var fileId = PdfStorageKey.FileIdFromPath(filePath);
        var resourceKey = PdfStorageKey.ResourceKeyFromPath(filePath);
        fileId.Should().NotBeNull();
        resourceKey.Should().NotBeNull();
        await using var stream = await Storage!.RetrieveAsync(fileId!, BlobCategory.Pdf, resourceKey!, Ct);
        stream.Should().NotBeNull($"the file at {filePath} should be in storage");
        using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer, Ct);
        return buffer.ToArray();
    }

    private (ShareRequestDocumentService Service, List<PdfDocument> Added) ServiceOver(PdfDocument source)
    {
        var added = new List<PdfDocument>();
        var documents = new Mock<IPdfDocumentRepository>();
        documents
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([source]);
        documents
            .Setup(r => r.AddAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()))
            .Callback<PdfDocument, CancellationToken>((d, _) => added.Add(d))
            .Returns(Task.CompletedTask);
        var service = new ShareRequestDocumentService(
            documents.Object,
            Mock.Of<IShareRequestRepository>(),
            Storage!,
            NullLogger<ShareRequestDocumentService>.Instance);
        return (service, added);
    }
}

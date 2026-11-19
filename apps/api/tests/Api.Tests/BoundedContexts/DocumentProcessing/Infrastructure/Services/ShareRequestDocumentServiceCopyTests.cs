using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services.Pdf;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// #5082: the copy of the documents attached to a share request, through <see cref="IBlobStorageService"/>. Both
/// storages are exercised for real in <c>ShareRequestDocumentCopyIntegrationTestsBase</c>; these cover the keys it
/// reads and a write that fails.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class ShareRequestDocumentServiceCopyTests
{
    private readonly Mock<IBlobStorageService> _storage = new();
    private readonly Mock<IPdfDocumentRepository> _documents = new();
    private readonly List<PdfDocument> _added = [];
    private readonly ShareRequestDocumentService _service;

    public ShareRequestDocumentServiceCopyTests()
    {
        _documents
            .Setup(r => r.AddAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()))
            .Callback<PdfDocument, CancellationToken>((d, _) => _added.Add(d))
            .Returns(Task.CompletedTask);
        _service = new ShareRequestDocumentService(
            _documents.Object,
            Mock.Of<IShareRequestRepository>(),
            _storage.Object,
            NullLogger<ShareRequestDocumentService>.Instance);
    }

    [Fact]
    public async Task Copy_ReadsTheSourceByTheKeysInItsPath_AndStoresItUnderTheSharedGame()
    {
        var source = Document("pdfs/private-1/file-a_rules.pdf", "rules.pdf");
        _documents.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([source]);
        _storage.Setup(s => s.RetrieveAsync("file-a", BlobCategory.Pdf, "private-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));
        var sharedGameId = Guid.NewGuid();
        _storage.Setup(s => s.StoreAsync(
                It.IsAny<Stream>(), "rules.pdf", BlobCategory.Pdf, $"shared-game-{sharedGameId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "file-b", $"pdfs/shared-game-{sharedGameId}/file-b_rules.pdf", 3));

        var copied = await _service.CopyDocumentsToSharedGame(
            [source.Id], sharedGameId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        copied.Should().ContainSingle();
        _added.Should().ContainSingle().Which.FilePath.Should().Be($"pdfs/shared-game-{sharedGameId}/file-b_rules.pdf");
    }

    [Fact]
    public async Task Copy_AWriteThatFails_SkipsThatDocument_AndCopiesTheNext()
    {
        var failing = Document("pdfs/private-1/file-a_a.pdf", "a.pdf");
        var working = Document("pdfs/private-1/file-c_c.pdf", "c.pdf");
        _documents.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([failing, working]);
        _storage.Setup(s => s.RetrieveAsync(It.IsAny<string>(), BlobCategory.Pdf, "private-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([1]));
        _storage.Setup(s => s.StoreAsync(It.IsAny<Stream>(), "a.pdf", BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(false, null, null, 0, "bucket unavailable"));
        _storage.Setup(s => s.StoreAsync(It.IsAny<Stream>(), "c.pdf", BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "file-d", "pdfs/shared-game-x/file-d_c.pdf", 1));

        var copied = await _service.CopyDocumentsToSharedGame(
            [failing.Id, working.Id], Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        copied.Should().ContainSingle();
        _added.Should().ContainSingle().Which.FileName.Value.Should().Be("c.pdf");
    }

    private static PdfDocument Document(string filePath, string fileName) =>
        new PdfDocumentBuilder().WithFilePath(filePath).WithFileName(fileName).Build();
}

using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Regression test for the chunked-upload duplicate-content path (Task 2 of the
/// PDF dedup alignment plan). Previously, a matching SHA-256 <c>ContentHash</c>
/// caused <see cref="CompleteChunkedUploadCommandHandler"/> to REJECT the upload
/// with <c>DuplicateContentErrorMessage</c>. This pins the aligned behavior:
/// the handler now delegates to <see cref="IPdfDeduplicationService"/> and, on
/// <see cref="PdfDedupDecision.ReuseExisting"/>, transparently reuses the existing
/// document (Success: true, DocumentId: existingId) instead of rejecting.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class CompleteChunkedUploadDedupTests
{
    [Fact]
    public async Task Complete_HashKnownReady_ReusesExistingInsteadOfRejecting()
    {
        // Arrange ----------------------------------------------------------------
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var existingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Real temp directory + single chunk file, because AssembleAndStoreFileAsync
        // is private and reads real files from disk via session.GetChunkFilePath/TempDirectory.
        var tempDir = Path.Combine(Path.GetTempPath(), "meepleai_uploads", sessionId.ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var session = new ChunkedUploadSession(
                sessionId,
                gameId,
                userId,
                "rulebook.pdf",
                totalFileSize: 14,
                tempDirectory: tempDir);

            var chunkPath = session.GetChunkFilePath(0);
            await File.WriteAllTextAsync(chunkPath, "dummy pdf data");
            session.MarkChunkReceived(0);

            var sessionRepoMock = new Mock<IChunkedUploadSessionRepository>();
            sessionRepoMock
                .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

            var blobStorageMock = new Mock<IBlobStorageService>();
            blobStorageMock
                .Setup(b => b.StoreAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BlobStorageResult(true, Guid.NewGuid().ToString(), "/tmp/rulebook.pdf", 14));
            blobStorageMock
                .Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var dedupMock = new Mock<IPdfDeduplicationService>();
            dedupMock
                .Setup(d => d.EvaluateAsync(
                    It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PdfDedupResult(PdfDedupDecision.ReuseExisting, existingId, "h"));

            var mediatorMock = new Mock<IMediator>();

            var sc = new ServiceCollection();
            sc.AddSingleton(db);
            var provider = sc.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var handler = new CompleteChunkedUploadCommandHandler(
                sessionRepoMock.Object,
                db,
                blobStorageMock.Object,
                Mock.Of<IBackgroundTaskService>(),
                NullLogger<CompleteChunkedUploadCommandHandler>.Instance,
                scopeFactory,
                Mock.Of<IPdfTextExtractor>(),
                Mock.Of<IPdfTableExtractor>(),
                mediatorMock.Object,
                dedupMock.Object,
                TimeProvider.System);

            var command = new CompleteChunkedUploadCommand(sessionId, userId);

            // Act ----------------------------------------------------------------
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert ---------------------------------------------------------------
            result.Success.Should().BeTrue();
            result.DocumentId.Should().Be(existingId);
            result.ErrorMessage.Should().BeNull();

            dedupMock.Verify(d => d.EvaluateAsync(
                It.IsAny<string>(), gameId, null, userId, It.IsAny<CancellationToken>()), Times.Once);

            mediatorMock.Verify(m => m.Send(
                It.IsAny<Api.BoundedContexts.EntityRelationships.Application.Commands.CreateEntityLinkCommand>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // No new PdfDocumentEntity should have been created for the reused hash.
            db.PdfDocuments.Should().BeEmpty();
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

using Api.BoundedContexts.DocumentProcessing.Application.Commands;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Tests for CompleteChunkedUploadCommandHandler — private game upload path.
/// Issue #5207: Ensures chunked upload for private games uses PrivateGameId as
/// blob storage key and persists PrivateGameId on the created PdfDocumentEntity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class CompleteChunkedUploadCommandHandlerPrivateGameTests : IDisposable
{
    private readonly string _tempDir;

    public CompleteChunkedUploadCommandHandlerPrivateGameTests()
    {
        // Must be under meepleai_uploads to pass IsValidTempDirectory security check
        _tempDir = Path.Combine(Path.GetTempPath(), "meepleai_uploads", $"test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a single-chunk session (100 bytes) so TotalChunks == 1.
    /// </summary>
    private ChunkedUploadSession CreatePrivateGameSession(Guid sessionId, Guid userId, Guid privateGameId)
    {
        // tempDir is already under meepleai_uploads — subdirectory per session
        var sessionTempDir = Path.Combine(_tempDir, sessionId.ToString("N"));
        Directory.CreateDirectory(sessionTempDir);

        var session = new ChunkedUploadSession(
            id: sessionId,
            gameId: null,
            userId: userId,
            fileName: "test-rulebook.pdf",
            totalFileSize: 100,
            tempDirectory: sessionTempDir,
            privateGameId: privateGameId);

        session.MarkChunkReceived(0);
        return session;
    }

    private static void WriteChunkFile(ChunkedUploadSession session)
    {
        var chunkPath = session.GetChunkFilePath(0);
        File.WriteAllBytes(chunkPath, new byte[100]);
    }

    [Fact]
    public async Task Handle_PrivateGameSession_UsesPrivateGameIdAsStorageKey()
    {
        // Arrange
        var privateGameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var session = CreatePrivateGameSession(sessionId, userId, privateGameId);
        WriteChunkFile(session);

        var sessionRepoMock = new Mock<IChunkedUploadSessionRepository>();
        sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        sessionRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<ChunkedUploadSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        string? capturedStorageGameId = null;
        var blobStorageMock = new Mock<IBlobStorageService>();
        blobStorageMock
            .Setup(b => b.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, CancellationToken>((_, _, gameId, _) => capturedStorageGameId = gameId)
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: Guid.NewGuid().ToString("N"),
                FilePath: "/uploads/test-rulebook.pdf",
                FileSizeBytes: 100));

        var backgroundTaskMock = new Mock<IBackgroundTaskService>();
        backgroundTaskMock
            .Setup(b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()));

        using var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var pdfTextExtractorMock = new Mock<IPdfTextExtractor>();
        var tableExtractorMock = new Mock<IPdfTableExtractor>();
        var mediatorMock = new Mock<IMediator>();

        var loggerMock = new Mock<ILogger<CompleteChunkedUploadCommandHandler>>();
        loggerMock.Setup(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

        var handler = new CompleteChunkedUploadCommandHandler(
            sessionRepoMock.Object,
            dbContext,
            blobStorageMock.Object,
            backgroundTaskMock.Object,
            loggerMock.Object,
            scopeFactoryMock.Object,
            pdfTextExtractorMock.Object,
            tableExtractorMock.Object,
            mediatorMock.Object);

        var command = new CompleteChunkedUploadCommand(sessionId, userId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(because: "private game chunked upload should complete successfully");
        capturedStorageGameId.Should().Be(privateGameId.ToString(),
            because: "blob storage must use PrivateGameId, not GameId, for private game PDFs");
    }

    [Fact]
    public async Task Handle_PrivateGameSession_PersistsPrivateGameIdOnPdfDocument()
    {
        // Arrange
        var privateGameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var session = CreatePrivateGameSession(sessionId, userId, privateGameId);
        WriteChunkFile(session);

        var sessionRepoMock = new Mock<IChunkedUploadSessionRepository>();
        sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        sessionRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<ChunkedUploadSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pdfDocId = Guid.NewGuid().ToString("N");
        var blobStorageMock = new Mock<IBlobStorageService>();
        blobStorageMock
            .Setup(b => b.StoreAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: pdfDocId,
                FilePath: "/uploads/test-rulebook.pdf",
                FileSizeBytes: 100));

        var backgroundTaskMock = new Mock<IBackgroundTaskService>();
        backgroundTaskMock
            .Setup(b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()));

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var pdfTextExtractorMock = new Mock<IPdfTextExtractor>();
        var tableExtractorMock = new Mock<IPdfTableExtractor>();
        var mediatorMock = new Mock<IMediator>();

        var loggerMock = new Mock<ILogger<CompleteChunkedUploadCommandHandler>>();
        loggerMock.Setup(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

        var handler = new CompleteChunkedUploadCommandHandler(
            sessionRepoMock.Object,
            dbContext,
            blobStorageMock.Object,
            backgroundTaskMock.Object,
            loggerMock.Object,
            scopeFactoryMock.Object,
            pdfTextExtractorMock.Object,
            tableExtractorMock.Object,
            mediatorMock.Object);

        var command = new CompleteChunkedUploadCommand(sessionId, userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var pdfDoc = dbContext.PdfDocuments.FirstOrDefault(p => p.Id == Guid.Parse(pdfDocId));
        pdfDoc.Should().NotBeNull(because: "PDF document record must be persisted");
        pdfDoc!.PrivateGameId.Should().Be(privateGameId,
            because: "PdfDocumentEntity must carry PrivateGameId for private game chunked uploads");
        pdfDoc.GameId.Should().BeNull(
            because: "GameId must remain null for private game PDFs");
    }
}

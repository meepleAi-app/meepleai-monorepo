using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Services.Qdrant;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for private PDF upload flow.
/// Issue #3653: Private PDF Upload Endpoint Full Integration.
///
/// Tests:
/// - SSE progress streaming pub/sub pattern
/// - PrivatePdfAssociatedEventHandler processing
/// - End-to-end event flow with mocked infrastructure
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3653")]
public sealed class PrivatePdfUploadIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;

    private static readonly Guid TestUserId = new("85000000-0000-0000-0000-000000000001");
    private static readonly Guid TestGameId = new("85000000-0000-0000-0000-000000000002");
    private static readonly Guid TestEntryId = new("85000000-0000-0000-0000-000000000003");
    private static readonly Guid TestPdfId = new("85000000-0000-0000-0000-000000000004");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PrivatePdfUploadIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"meepleai_issue3653_privatepdf_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName).ConfigureAwait(false);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(TestCancellationToken).ConfigureAwait(false);

        // Seed required test data
        await SeedTestDataAsync().ConfigureAwait(false);
    }

    private async Task SeedTestDataAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test-issue3653@meepleai.dev",
            DisplayName = "Test User Issue 3653",
            Role = "user",
            Tier = "normal",
            CreatedAt = DateTime.UtcNow
        };

        var game = new Api.Infrastructure.Entities.GameEntity
        {
            Id = TestGameId,
            Name = "Test Game for Private PDF",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<UserEntity>().Add(user);
        _dbContext.Set<Api.Infrastructure.Entities.GameEntity>().Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync().ConfigureAwait(false);
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName).ConfigureAwait(false);
    }

    #region SSE Progress Streaming Tests

    [Fact]
    public async Task SubscribeToProgress_ReceivesPublishedUpdates()
    {
        // Arrange
        var progressService = new PrivatePdfProgressStreamService(
            NullLogger<PrivatePdfProgressStreamService>.Instance);

        var receivedProgress = new List<ProcessingProgressJson>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Start subscriber in background
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var progress in progressService.SubscribeToProgress(TestUserId, TestEntryId, cts.Token))
            {
                receivedProgress.Add(progress);
                if (progress.Step is ProcessingStep.Completed or ProcessingStep.Failed)
                {
                    break;
                }
            }
        }, cts.Token);

        // Give subscriber time to connect
        await Task.Delay(100, cts.Token);

        // Act - Publish progress updates
        await progressService.PublishProgressAsync(TestUserId, TestEntryId,
            ProcessingProgressJson.ForStep(ProcessingStep.Uploading, 20, "Starting..."), cts.Token);

        await progressService.PublishProgressAsync(TestUserId, TestEntryId,
            ProcessingProgressJson.ForStep(ProcessingStep.Extracting, 40, "Extracting..."), cts.Token);

        await progressService.PublishProgressAsync(TestUserId, TestEntryId,
            ProcessingProgressJson.ForStep(ProcessingStep.Chunking, 60, "Chunking..."), cts.Token);

        await progressService.PublishProgressAsync(TestUserId, TestEntryId,
            ProcessingProgressJson.Completed(), cts.Token);

        // Wait for subscriber to complete
        await subscriberTask;

        // Assert
        receivedProgress.Should().HaveCount(4);
        receivedProgress[0].Step.Should().Be(ProcessingStep.Uploading);
        receivedProgress[1].Step.Should().Be(ProcessingStep.Extracting);
        receivedProgress[2].Step.Should().Be(ProcessingStep.Chunking);
        receivedProgress[3].Step.Should().Be(ProcessingStep.Completed);
    }

    [Fact]
    public async Task SubscribeToProgress_AutoCompletesOnTerminalState()
    {
        // Arrange
        var progressService = new PrivatePdfProgressStreamService(
            NullLogger<PrivatePdfProgressStreamService>.Instance);

        var receivedProgress = new List<ProcessingProgressJson>();

        // Start subscriber
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var progress in progressService.SubscribeToProgress(TestUserId, TestEntryId, cts.Token))
            {
                receivedProgress.Add(progress);
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);

        // Act - Publish failure (terminal state)
        await progressService.PublishProgressAsync(TestUserId, TestEntryId,
            ProcessingProgressJson.Failed("Test failure"), cts.Token);

        await subscriberTask;

        // Assert
        receivedProgress.Should().HaveCount(1);
        receivedProgress[0].Step.Should().Be(ProcessingStep.Failed);
        receivedProgress[0].Error.Should().Contain("Test failure");
    }

    [Fact]
    public async Task HasActiveSubscribers_ReturnsTrueWhenSubscribed()
    {
        // Arrange
        var progressService = new PrivatePdfProgressStreamService(
            NullLogger<PrivatePdfProgressStreamService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Initially no subscribers
        progressService.HasActiveSubscribers(TestUserId, TestEntryId).Should().BeFalse();

        // Start subscriber
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var _ in progressService.SubscribeToProgress(TestUserId, TestEntryId, cts.Token))
            {
                // Just enumerate
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);

        // Act & Assert
        progressService.HasActiveSubscribers(TestUserId, TestEntryId).Should().BeTrue();

        // Cleanup
        await cts.CancelAsync();
        try { await subscriberTask; }
        catch (OperationCanceledException) { /* Expected */ }
    }

    [Fact]
    public async Task PublishProgress_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        var progressService = new PrivatePdfProgressStreamService(
            NullLogger<PrivatePdfProgressStreamService>.Instance);

        // Act - Should not throw
        var act = async () => await progressService.PublishProgressAsync(TestUserId, TestEntryId,
            ProcessingProgressJson.ForStep(ProcessingStep.Uploading, 10, "Test"), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MultipleSubscribers_AllReceiveUpdates()
    {
        // Arrange
        var progressService = new PrivatePdfProgressStreamService(
            NullLogger<PrivatePdfProgressStreamService>.Instance);

        var subscriber1Progress = new List<ProcessingProgressJson>();
        var subscriber2Progress = new List<ProcessingProgressJson>();

        // Start two subscribers
        var subscriber1Task = Task.Run(async () =>
        {
            await foreach (var progress in progressService.SubscribeToProgress(TestUserId, TestEntryId, cts.Token))
            {
                subscriber1Progress.Add(progress);
                if (progress.Step == ProcessingStep.Completed) break;
            }
        }, cts.Token);

        var subscriber2Task = Task.Run(async () =>
        {
            await foreach (var progress in progressService.SubscribeToProgress(TestUserId, TestEntryId, cts.Token))
            {
                subscriber2Progress.Add(progress);
                if (progress.Step == ProcessingStep.Completed) break;
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);

        // Act
        await progressService.PublishProgressAsync(TestUserId, TestEntryId,
            ProcessingProgressJson.ForStep(ProcessingStep.Indexing, 90, "Indexing..."), cts.Token);

        await progressService.PublishProgressAsync(TestUserId, TestEntryId,
            ProcessingProgressJson.Completed(), cts.Token);

        await Task.WhenAll(subscriber1Task, subscriber2Task);

        // Assert
        subscriber1Progress.Should().HaveCount(2);
        subscriber2Progress.Should().HaveCount(2);
        subscriber1Progress.Should().BeEquivalentTo(subscriber2Progress);
    }

    #endregion

    #region Event Handler Integration Tests

    [Fact]
    public async Task EventHandler_WithExtractedText_ProcessesSuccessfully()
    {
        // Arrange
        // Create a PDF document entity with extracted text
        var pdfEntity = new PdfDocumentEntity
        {
            Id = TestPdfId,
            GameId = TestGameId,
            FileName = "test-rules.pdf",
            FilePath = "/storage/test/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = TestUserId,
            Language = "en",
            ExtractedText = "This is a test rulebook for the game. The game has multiple phases: setup, main game, and scoring."
        };

        _dbContext!.Set<PdfDocumentEntity>().Add(pdfEntity);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Verify PDF exists in database before handler runs
        var pdfBeforeHandler = await _dbContext.Set<PdfDocumentEntity>()
            .FirstOrDefaultAsync(p => p.Id == TestPdfId, TestCancellationToken);
        pdfBeforeHandler.Should().NotBeNull("PDF should exist in database before handler runs");
        pdfBeforeHandler!.ExtractedText.Should().NotBeNullOrEmpty("PDF should have extracted text");

        // Mock services with callbacks to verify they're being invoked
        var textChunks = new List<TextChunk>
        {
            new() { Text = "Chunk 1", Page = 1, CharStart = 0, CharEnd = 50 },
            new() { Text = "Chunk 2", Page = 1, CharStart = 51, CharEnd = 100 }
        };
        var chunkingWasCalled = false;
        var mockChunkingService = new Mock<ITextChunkingService>();
        // Setup with default values (512, 50) that match the actual call
        mockChunkingService.Setup(s => s.ChunkText(It.IsAny<string>(), 512, 50))
            .Callback(() => chunkingWasCalled = true)
            .Returns(textChunks);
        // Also setup with any values for flexibility
        mockChunkingService.Setup(s => s.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback(() => chunkingWasCalled = true)
            .Returns(textChunks);

        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService.Setup(s => s.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]>
                {
                    new float[384],
                    new float[384]
                }
            });
        mockEmbeddingService.Setup(s => s.GetEmbeddingDimensions()).Returns(384);

        var mockCollectionManager = new Mock<IQdrantCollectionManager>();
        mockCollectionManager.Setup(s => s.EnsureCollectionExistsAsync(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockVectorIndexer = new Mock<IQdrantVectorIndexer>();
        mockVectorIndexer.Setup(s => s.BuildPoints(It.IsAny<List<DocumentChunk>>(), It.IsAny<Dictionary<string, Qdrant.Client.Grpc.Value>>()))
            .Returns(new List<Qdrant.Client.Grpc.PointStruct>());
        mockVectorIndexer.Setup(s => s.UpsertPointsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<Qdrant.Client.Grpc.PointStruct>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockProgressService = new Mock<IPrivatePdfProgressStreamService>();
        mockProgressService.Setup(s => s.PublishProgressAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ProcessingProgressJson>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var indexingSettings = Options.Create(new IndexingSettings { EmbeddingBatchSize = 10 });

        var handler = new PrivatePdfAssociatedEventHandler(
            _dbContext,
            mockChunkingService.Object,
            mockEmbeddingService.Object,
            mockCollectionManager.Object,
            mockVectorIndexer.Object,
            mockProgressService.Object,
            indexingSettings,
            NullLogger<PrivatePdfAssociatedEventHandler>.Instance);

        var notification = new PrivatePdfAssociatedEvent(
            libraryEntryId: TestEntryId,
            userId: TestUserId,
            gameId: TestGameId,
            pdfDocumentId: TestPdfId);

        // Act
        await handler.Handle(notification, TestCancellationToken);

        // Assert - First verify mocks were invoked
        chunkingWasCalled.Should().BeTrue("ChunkText mock should have been called - if not, the handler failed before chunking");

        var updatedPdf = await _dbContext.Set<PdfDocumentEntity>()
            .FirstAsync(p => p.Id == TestPdfId, TestCancellationToken);

        updatedPdf.ProcessingState.ToString().Should().Be("Ready");
        updatedPdf.ProcessedAt.Should().NotBeNull();

        // Verify services were called
        mockChunkingService.Verify(s => s.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        mockEmbeddingService.Verify(s => s.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        mockCollectionManager.Verify(s => s.EnsureCollectionExistsAsync("private_rules", 384, It.IsAny<CancellationToken>()), Times.Once);
        mockVectorIndexer.Verify(s => s.UpsertPointsAsync("private_rules", It.IsAny<IReadOnlyList<Qdrant.Client.Grpc.PointStruct>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EventHandler_WithoutExtractedText_PublishesFailure()
    {
        // Arrange
        var pdfIdNoText = Guid.NewGuid();

        // Create a PDF document entity WITHOUT extracted text
        var pdfEntity = new PdfDocumentEntity
        {
            Id = pdfIdNoText,
            GameId = TestGameId,
            FileName = "no-text.pdf",
            FilePath = "/storage/test/no-text.pdf",
            FileSizeBytes = 512,
            UploadedByUserId = TestUserId,
            Language = "en",
            ExtractedText = null // No text extracted
        };

        _dbContext!.Set<PdfDocumentEntity>().Add(pdfEntity);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var progressService = new PrivatePdfProgressStreamService(
            NullLogger<PrivatePdfProgressStreamService>.Instance);

        var receivedProgress = new List<ProcessingProgressJson>();

        // Subscribe to progress
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var progress in progressService.SubscribeToProgress(TestUserId, pdfIdNoText, cts.Token))
            {
                receivedProgress.Add(progress);
                if (progress.Step is ProcessingStep.Completed or ProcessingStep.Failed) break;
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);

        var handler = new PrivatePdfAssociatedEventHandler(
            _dbContext,
            Mock.Of<ITextChunkingService>(),
            Mock.Of<IEmbeddingService>(),
            Mock.Of<IQdrantCollectionManager>(),
            Mock.Of<IQdrantVectorIndexer>(),
            progressService,
            Options.Create(new IndexingSettings { EmbeddingBatchSize = 10 }),
            NullLogger<PrivatePdfAssociatedEventHandler>.Instance);

        var notification = new PrivatePdfAssociatedEvent(
            libraryEntryId: pdfIdNoText, // Use pdfId as entryId for this test
            userId: TestUserId,
            gameId: TestGameId,
            pdfDocumentId: pdfIdNoText);

        // Act
        await handler.Handle(notification, TestCancellationToken);
        await subscriberTask;

        // Assert - Handler should publish failure due to missing text
        receivedProgress.Should().Contain(p => p.Step == ProcessingStep.Failed);
    }

    [Fact]
    public async Task EventHandler_PdfNotFound_PublishesFailure()
    {
        // Arrange
        var nonExistentPdfId = Guid.NewGuid();

        var progressService = new PrivatePdfProgressStreamService(
            NullLogger<PrivatePdfProgressStreamService>.Instance);

        var receivedProgress = new List<ProcessingProgressJson>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var progress in progressService.SubscribeToProgress(TestUserId, nonExistentPdfId, cts.Token))
            {
                receivedProgress.Add(progress);
                if (progress.Step is ProcessingStep.Completed or ProcessingStep.Failed) break;
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);

        var handler = new PrivatePdfAssociatedEventHandler(
            _dbContext!,
            Mock.Of<ITextChunkingService>(),
            Mock.Of<IEmbeddingService>(),
            Mock.Of<IQdrantCollectionManager>(),
            Mock.Of<IQdrantVectorIndexer>(),
            progressService,
            Options.Create(new IndexingSettings { EmbeddingBatchSize = 10 }),
            NullLogger<PrivatePdfAssociatedEventHandler>.Instance);

        var notification = new PrivatePdfAssociatedEvent(
            libraryEntryId: nonExistentPdfId,
            userId: TestUserId,
            gameId: TestGameId,
            pdfDocumentId: nonExistentPdfId);

        // Act
        await handler.Handle(notification, TestCancellationToken);
        await subscriberTask;

        // Assert
        receivedProgress.Should().Contain(p => p.Step == ProcessingStep.Failed);
        receivedProgress.First(p => p.Step == ProcessingStep.Failed).Error.Should().Contain("not found");
    }

    #endregion
}

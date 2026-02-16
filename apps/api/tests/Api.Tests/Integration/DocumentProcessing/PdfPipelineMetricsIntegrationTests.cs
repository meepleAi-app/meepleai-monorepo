using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for PDF pipeline metrics accuracy across all states.
/// Issue #4413: Full pipeline integration test for #4219 DoD completion.
/// Verifies timing fields, progress percentage, ETA, state durations,
/// and total duration across the complete pipeline.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfPipelineMetricsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IPdfDocumentRepository? _pdfRepository;

    private static readonly Guid TestUserId = new("a4130000-0000-0000-0000-000000000001");
    private static readonly Guid TestGameId = new("a4130000-0000-0000-0000-000000000002");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PdfPipelineMetricsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"meepleai_4413_metrics_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        await SeedTestDataAsync();

        _pdfRepository = new PdfDocumentRepository(_dbContext, mockEventCollector.Object);
    }

    private async Task SeedTestDataAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test-4413-metrics@meepleai.dev",
            DisplayName = "Test User 4413 Metrics",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        var game = new GameEntity
        {
            Id = TestGameId,
            Name = "Test Game for Pipeline Metrics",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<UserEntity>().Add(user);
        _dbContext.Set<GameEntity>().Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    #region Full Pipeline Metrics Tests

    [Fact]
    public async Task FullPipeline_MetricsAccurate_AcrossAllStates()
    {
        // Arrange: Create PdfDocument
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("metrics-test.pdf"),
            "/uploads/metrics-test.pdf",
            new FileSize(1024 * 1500), // 1.5MB
            TestUserId,
            LanguageCode.English
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert initial state: Pending
        pdf.ProcessingState.Should().Be(PdfProcessingState.Pending);
        pdf.ProgressPercentage.Should().Be(0);
        pdf.UploadingStartedAt.Should().BeNull();

        // Act: Transition through all states in-memory (following existing test pattern)
        // Note: Intermediate states verified in-memory since repository MapToDomain
        // uses deprecated ProcessingStatus string which doesn't distinguish mid-pipeline states.
        _dbContext.ChangeTracker.Clear();
        var doc = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);

        // State 1: Pending → Uploading
        doc!.TransitionTo(PdfProcessingState.Uploading);
        doc.ProcessingState.Should().Be(PdfProcessingState.Uploading);
        doc.ProgressPercentage.Should().Be(10);
        doc.UploadingStartedAt.Should().NotBeNull();
        doc.UploadingStartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // State 2: Uploading → Extracting
        doc.TransitionTo(PdfProcessingState.Extracting);
        doc.ProcessingState.Should().Be(PdfProcessingState.Extracting);
        doc.ProgressPercentage.Should().Be(30);
        doc.ExtractingStartedAt.Should().NotBeNull();

        // State 3: Extracting → Chunking
        doc.TransitionTo(PdfProcessingState.Chunking);
        doc.ProcessingState.Should().Be(PdfProcessingState.Chunking);
        doc.ProgressPercentage.Should().Be(50);
        doc.ChunkingStartedAt.Should().NotBeNull();

        // State 4: Chunking → Embedding
        doc.TransitionTo(PdfProcessingState.Embedding);
        doc.ProcessingState.Should().Be(PdfProcessingState.Embedding);
        doc.ProgressPercentage.Should().Be(70);
        doc.EmbeddingStartedAt.Should().NotBeNull();

        // State 5: Embedding → Indexing
        doc.TransitionTo(PdfProcessingState.Indexing);
        doc.ProcessingState.Should().Be(PdfProcessingState.Indexing);
        doc.ProgressPercentage.Should().Be(90);
        doc.IndexingStartedAt.Should().NotBeNull();

        // State 6: Indexing → Ready (completion)
        doc.TransitionTo(PdfProcessingState.Ready);
        doc.ProcessingState.Should().Be(PdfProcessingState.Ready);
        doc.ProgressPercentage.Should().Be(100);
        doc.EstimatedTimeRemaining.Should().Be(TimeSpan.Zero);

        // Persist final state and verify DB round-trip
        await _pdfRepository.UpdateAsync(doc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        _dbContext.ChangeTracker.Clear();
        doc = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        doc!.ProcessingState.Should().Be(PdfProcessingState.Ready);
        doc.ProgressPercentage.Should().Be(100);

        // Verify all timing fields persisted correctly after DB round-trip
        doc.UploadingStartedAt.Should().NotBeNull();
        doc.ExtractingStartedAt.Should().NotBeNull();
        doc.ChunkingStartedAt.Should().NotBeNull();
        doc.EmbeddingStartedAt.Should().NotBeNull();
        doc.IndexingStartedAt.Should().NotBeNull();

        // Verify timing order: each state starts after the previous
        doc.ExtractingStartedAt.Should().BeOnOrAfter(doc.UploadingStartedAt!.Value);
        doc.ChunkingStartedAt.Should().BeOnOrAfter(doc.ExtractingStartedAt!.Value);
        doc.EmbeddingStartedAt.Should().BeOnOrAfter(doc.ChunkingStartedAt!.Value);
        doc.IndexingStartedAt.Should().BeOnOrAfter(doc.EmbeddingStartedAt!.Value);
    }

    [Fact]
    public async Task FullPipeline_MarkAsCompleted_SetsTotalDuration()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("duration-test.pdf"),
            "/uploads/duration-test.pdf",
            new FileSize(1024 * 800),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        _dbContext.ChangeTracker.Clear();
        var doc = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);

        // Act: Complete the full pipeline
        doc!.MarkAsCompleted(42); // 42 pages
        await _pdfRepository.UpdateAsync(doc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        _dbContext.ChangeTracker.Clear();
        doc = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        doc!.ProcessingState.Should().Be(PdfProcessingState.Ready);
        doc.PageCount.Should().Be(42);
        doc.ProcessedAt.Should().NotBeNull();
        doc.TotalDuration.Should().NotBeNull();
        doc.TotalDuration!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        doc.TotalDuration.Value.Should().Be(doc.ProcessedAt!.Value - doc.UploadedAt);
    }

    [Fact]
    public async Task FullPipeline_RetryIncrementsCount_AndResetsState()
    {
        // Arrange: Create and advance to Chunking
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("retry-test.pdf"),
            "/uploads/retry-test.pdf",
            new FileSize(1024 * 600),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        _dbContext.ChangeTracker.Clear();
        var doc = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);

        // Advance to Chunking in-memory
        doc!.TransitionTo(PdfProcessingState.Uploading);
        doc.TransitionTo(PdfProcessingState.Extracting);
        doc.TransitionTo(PdfProcessingState.Chunking);

        // Fail at Chunking
        doc.MarkAsFailed("Chunking failed: out of memory", ErrorCategory.Network, PdfProcessingState.Chunking);
        doc.ProcessingState.Should().Be(PdfProcessingState.Failed);
        doc.RetryCount.Should().Be(0);
        doc.FailedAtState.Should().Be(PdfProcessingState.Chunking);

        // Persist failed state
        await _pdfRepository.UpdateAsync(doc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Verify failed state persists correctly through DB round-trip
        _dbContext.ChangeTracker.Clear();
        doc = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        doc!.ProcessingState.Should().Be(PdfProcessingState.Failed);
        doc.RetryCount.Should().Be(0);
        doc.FailedAtState.Should().Be(PdfProcessingState.Chunking);

        // Act: Retry in-memory and verify state before persisting
        doc.Retry();
        doc.RetryCount.Should().Be(1);
        doc.ProcessingError.Should().BeNull();
        doc.ProcessedAt.Should().BeNull();

        // Persist and verify retry count survives DB round-trip
        await _pdfRepository.UpdateAsync(doc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        _dbContext.ChangeTracker.Clear();
        doc = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        doc!.RetryCount.Should().Be(1);
        doc.ProcessingError.Should().BeNull();
        doc.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task FullPipeline_ProgressPercentage_CorrectForAllStates()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("progress-test.pdf"),
            "/uploads/progress-test.pdf",
            new FileSize(1024 * 400),
            TestUserId
        );

        // Assert Pending = 0%
        pdf.ProgressPercentage.Should().Be(0);

        // Expected: Uploading=10, Extracting=30, Chunking=50, Embedding=70, Indexing=90, Ready=100
        var expectedProgress = new (PdfProcessingState State, int Percent)[]
        {
            (PdfProcessingState.Uploading, 10),
            (PdfProcessingState.Extracting, 30),
            (PdfProcessingState.Chunking, 50),
            (PdfProcessingState.Embedding, 70),
            (PdfProcessingState.Indexing, 90),
            (PdfProcessingState.Ready, 100),
        };

        foreach (var (state, expectedPercent) in expectedProgress)
        {
            pdf.TransitionTo(state);
            pdf.ProgressPercentage.Should().Be(expectedPercent, $"State {state} should be {expectedPercent}%");
        }
    }

    [Fact]
    public async Task FullPipeline_StateDurations_CanBeCalculatedFromTimingFields()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("durations-test.pdf"),
            "/uploads/durations-test.pdf",
            new FileSize(1024 * 1000),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act: Transition through all states with persistence
        _dbContext.ChangeTracker.Clear();
        var doc = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);

        doc!.TransitionTo(PdfProcessingState.Uploading);
        await _pdfRepository.UpdateAsync(doc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        doc.TransitionTo(PdfProcessingState.Extracting);
        await _pdfRepository.UpdateAsync(doc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        doc.TransitionTo(PdfProcessingState.Chunking);
        await _pdfRepository.UpdateAsync(doc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        doc.TransitionTo(PdfProcessingState.Embedding);
        await _pdfRepository.UpdateAsync(doc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        doc.TransitionTo(PdfProcessingState.Indexing);
        await _pdfRepository.UpdateAsync(doc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        doc.TransitionTo(PdfProcessingState.Ready);
        await _pdfRepository.UpdateAsync(doc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert: All timing fields populated and in order
        _dbContext.ChangeTracker.Clear();
        doc = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);

        doc!.UploadingStartedAt.Should().NotBeNull();
        doc.ExtractingStartedAt.Should().NotBeNull();
        doc.ChunkingStartedAt.Should().NotBeNull();
        doc.EmbeddingStartedAt.Should().NotBeNull();
        doc.IndexingStartedAt.Should().NotBeNull();

        // State durations can be calculated as intervals between timing fields
        var uploadingDuration = doc.ExtractingStartedAt!.Value - doc.UploadingStartedAt!.Value;
        var extractingDuration = doc.ChunkingStartedAt!.Value - doc.ExtractingStartedAt!.Value;
        var chunkingDuration = doc.EmbeddingStartedAt!.Value - doc.ChunkingStartedAt!.Value;
        var embeddingDuration = doc.IndexingStartedAt!.Value - doc.EmbeddingStartedAt!.Value;

        // All durations should be non-negative
        uploadingDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        extractingDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        chunkingDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        embeddingDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task FullPipeline_FailedState_SetsProgressToZero()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("failed-progress-test.pdf"),
            "/uploads/failed-progress-test.pdf",
            new FileSize(1024 * 300),
            TestUserId
        );

        // Advance to Extracting
        pdf.TransitionTo(PdfProcessingState.Uploading);
        pdf.TransitionTo(PdfProcessingState.Extracting);
        pdf.ProgressPercentage.Should().Be(30);

        // Act: Fail
        pdf.MarkAsFailed("Extraction error", ErrorCategory.Parsing, PdfProcessingState.Extracting);

        // Assert
        pdf.ProcessingState.Should().Be(PdfProcessingState.Failed);
        pdf.ProgressPercentage.Should().Be(0);
        pdf.EstimatedTimeRemaining.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task FullPipeline_ETA_DecreasesWithEachTransition()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("eta-test.pdf"),
            "/uploads/eta-test.pdf",
            new FileSize(1024 * 500),
            TestUserId
        );

        var etas = new List<TimeSpan?>();

        // Act: Record ETA at each state
        var states = new[]
        {
            PdfProcessingState.Uploading,
            PdfProcessingState.Extracting,
            PdfProcessingState.Chunking,
            PdfProcessingState.Embedding,
            PdfProcessingState.Indexing,
        };

        foreach (var state in states)
        {
            pdf.TransitionTo(state);
            etas.Add(pdf.EstimatedTimeRemaining);
        }

        // Assert: ETA strictly decreases
        for (int i = 1; i < etas.Count; i++)
        {
            etas[i].Should().NotBeNull();
            etas[i]!.Value.Should().BeLessThan(etas[i - 1]!.Value,
                $"ETA at state index {i} should be less than at {i - 1}");
        }
    }

    #endregion
}

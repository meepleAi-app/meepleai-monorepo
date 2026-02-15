using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
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
/// Integration tests for PDF pipeline metrics accuracy across all processing states.
/// Issue #4413: Validates timing fields, progress percentage, ETA calculation,
/// and state durations through the complete pipeline.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfMetricsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IPdfDocumentRepository? _pdfRepository;

    private static readonly Guid TestUserId = new("96000000-0000-0000-0000-000000000001");
    private static readonly Guid TestGameId = new("96000000-0000-0000-0000-000000000002");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PdfMetricsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"meepleai_metrics_{Guid.NewGuid():N}";
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
            Email = "test-metrics@meepleai.dev",
            DisplayName = "Test User Metrics",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        var game = new GameEntity
        {
            Id = TestGameId,
            Name = "Test Game for Metrics",
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

    // =========================================================================
    // Full Pipeline Metrics Tests
    // =========================================================================

    [Fact]
    public async Task FullPipeline_ProgressPercentage_IncreasesAcrossAllStates()
    {
        // Arrange
        var pdf = CreateTestPdfDocument();
        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert initial state
        pdf.ProcessingState.Should().Be(PdfProcessingState.Pending);
        pdf.ProgressPercentage.Should().Be(0);

        // Act & Assert: Walk through each state and verify progress increases
        var expectedProgress = new Dictionary<PdfProcessingState, int>
        {
            { PdfProcessingState.Uploading, 10 },
            { PdfProcessingState.Extracting, 30 },
            { PdfProcessingState.Chunking, 50 },
            { PdfProcessingState.Embedding, 70 },
            { PdfProcessingState.Indexing, 90 },
        };

        foreach (var (state, expectedPercent) in expectedProgress)
        {
            pdf.TransitionTo(state);
            pdf.ProgressPercentage.Should().Be(expectedPercent,
                because: $"state {state} should have {expectedPercent}% progress");
        }

        // Complete the pipeline
        pdf.MarkAsCompleted(pageCount: 10);
        pdf.ProgressPercentage.Should().Be(100);
    }

    [Fact]
    public async Task FullPipeline_TimingFields_SetOnEachStateTransition()
    {
        // Arrange
        var pdf = CreateTestPdfDocument();
        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var beforeTransition = DateTime.UtcNow;

        // Act: Transition through all states
        pdf.TransitionTo(PdfProcessingState.Uploading);
        pdf.UploadingStartedAt.Should().NotBeNull()
            .And.Subject.Should().BeOnOrAfter(beforeTransition);

        pdf.TransitionTo(PdfProcessingState.Extracting);
        pdf.ExtractingStartedAt.Should().NotBeNull()
            .And.Subject.Should().BeOnOrAfter(beforeTransition);

        pdf.TransitionTo(PdfProcessingState.Chunking);
        pdf.ChunkingStartedAt.Should().NotBeNull()
            .And.Subject.Should().BeOnOrAfter(beforeTransition);

        pdf.TransitionTo(PdfProcessingState.Embedding);
        pdf.EmbeddingStartedAt.Should().NotBeNull()
            .And.Subject.Should().BeOnOrAfter(beforeTransition);

        pdf.TransitionTo(PdfProcessingState.Indexing);
        pdf.IndexingStartedAt.Should().NotBeNull()
            .And.Subject.Should().BeOnOrAfter(beforeTransition);

        // Complete
        pdf.MarkAsCompleted(pageCount: 10);

        // Assert: All timing fields are set
        pdf.UploadingStartedAt.Should().NotBeNull();
        pdf.ExtractingStartedAt.Should().NotBeNull();
        pdf.ChunkingStartedAt.Should().NotBeNull();
        pdf.EmbeddingStartedAt.Should().NotBeNull();
        pdf.IndexingStartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FullPipeline_TotalDuration_CalculatedOnCompletion()
    {
        // Arrange
        var pdf = CreateTestPdfDocument();
        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // TotalDuration should be null before completion
        pdf.TotalDuration.Should().BeNull();

        // Act: Complete the pipeline
        pdf.TransitionTo(PdfProcessingState.Uploading);
        pdf.TransitionTo(PdfProcessingState.Extracting);
        pdf.TransitionTo(PdfProcessingState.Chunking);
        pdf.TransitionTo(PdfProcessingState.Embedding);
        pdf.TransitionTo(PdfProcessingState.Indexing);
        pdf.MarkAsCompleted(pageCount: 5);

        // Assert: TotalDuration should be positive
        pdf.TotalDuration.Should().NotBeNull();
        pdf.TotalDuration!.Value.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task FullPipeline_ETA_DecreasesAsProgressAdvances()
    {
        // Arrange
        var pdf = CreateTestPdfDocument();
        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act: Track ETA at each state
        var etas = new List<TimeSpan?>();

        pdf.TransitionTo(PdfProcessingState.Uploading);
        etas.Add(pdf.EstimatedTimeRemaining);

        pdf.TransitionTo(PdfProcessingState.Extracting);
        etas.Add(pdf.EstimatedTimeRemaining);

        pdf.TransitionTo(PdfProcessingState.Chunking);
        etas.Add(pdf.EstimatedTimeRemaining);

        pdf.TransitionTo(PdfProcessingState.Embedding);
        etas.Add(pdf.EstimatedTimeRemaining);

        pdf.TransitionTo(PdfProcessingState.Indexing);
        etas.Add(pdf.EstimatedTimeRemaining);

        // Assert: Each ETA should be less than or equal to the previous one
        for (var i = 1; i < etas.Count; i++)
        {
            etas[i].Should().NotBeNull();
            etas[i]!.Value.Should().BeLessThanOrEqualTo(etas[i - 1]!.Value,
                because: $"ETA at step {i} should be <= ETA at step {i - 1}");
        }

        // Complete: ETA should be zero
        pdf.MarkAsCompleted(pageCount: 10);
        pdf.EstimatedTimeRemaining.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task FullPipeline_MetricsPersisted_AfterSaveAndReload()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var pdf = new PdfDocument(
            pdfId,
            TestGameId,
            new FileName("metrics-persist-test.pdf"),
            "/uploads/metrics-persist-test.pdf",
            new FileSize(1024 * 100),
            TestUserId,
            LanguageCode.English
        );

        // Act: Progress through pipeline and save
        pdf.TransitionTo(PdfProcessingState.Uploading);
        pdf.TransitionTo(PdfProcessingState.Extracting);
        pdf.TransitionTo(PdfProcessingState.Chunking);

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Reload from database
        var reloaded = await _pdfRepository.GetByIdAsync(pdfId, TestCancellationToken);

        // Assert: Metrics persisted correctly
        reloaded.Should().NotBeNull();
        reloaded!.ProcessingState.Should().Be(PdfProcessingState.Chunking);
        reloaded.ProgressPercentage.Should().Be(50);
        reloaded.UploadingStartedAt.Should().NotBeNull();
        reloaded.ExtractingStartedAt.Should().NotBeNull();
        reloaded.ChunkingStartedAt.Should().NotBeNull();
        reloaded.EmbeddingStartedAt.Should().BeNull();
        reloaded.IndexingStartedAt.Should().BeNull();
    }

    [Fact]
    public async Task FullPipeline_RetryOverhead_IncludedInMetrics()
    {
        // Arrange
        var pdf = CreateTestPdfDocument();
        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act: Progress, fail, retry
        pdf.TransitionTo(PdfProcessingState.Uploading);
        pdf.TransitionTo(PdfProcessingState.Extracting);

        // Fail at Extracting
        pdf.MarkAsFailed("Extraction timeout", "timeout", PdfProcessingState.Extracting);
        pdf.RetryCount.Should().Be(0);

        // Retry
        pdf.RetryProcessing();
        pdf.RetryCount.Should().Be(1);

        // Complete successfully
        pdf.TransitionTo(PdfProcessingState.Chunking);
        pdf.TransitionTo(PdfProcessingState.Embedding);
        pdf.TransitionTo(PdfProcessingState.Indexing);
        pdf.MarkAsCompleted(pageCount: 8);

        // Assert
        pdf.RetryCount.Should().Be(1);
        pdf.ProgressPercentage.Should().Be(100);
        pdf.TotalDuration.Should().NotBeNull();
        pdf.ProcessingState.Should().Be(PdfProcessingState.Ready);
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================

    private static PdfDocument CreateTestPdfDocument()
    {
        return new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("test-metrics-pipeline.pdf"),
            "/uploads/test-metrics-pipeline.pdf",
            new FileSize(1024 * 200), // 200KB
            TestUserId,
            LanguageCode.English
        );
    }
}

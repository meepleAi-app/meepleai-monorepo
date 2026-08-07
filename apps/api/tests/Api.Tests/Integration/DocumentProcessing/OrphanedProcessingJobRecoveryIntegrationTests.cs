using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Regression guard for issue #3588 (second defect): a <c>processing_jobs</c> row left
/// <c>Processing</c> by a container restart is orphaned forever. The queue worker skips its entire
/// cycle while <c>Processing &gt;= MaxConcurrentWorkers</c>, so a handful of orphans permanently
/// wedge ingest — on staging one of them blocked it for 96 minutes.
///
/// Recovery must requeue (not fail) the job AND rewind its PDF, because the pipeline's atomic claim
/// only accepts a <c>Pending</c> document: requeuing the job alone would have the worker pick it up,
/// silently skip on the refused claim, and then mark the job <c>Completed</c> without doing any work.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3588")]
public sealed class OrphanedProcessingJobRecoveryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000003588001");
    private static readonly Guid TestGameId = new("B0000000-0000-0000-0000-000003588001");

    public OrphanedProcessingJobRecoveryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_orphan_jobs_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.MigrateAsync(TestCancellationToken);
        await SeedBaseDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Best-effort cleanup — test isolation already achieved by isolated DB.
            }
        }
    }

    private async Task SeedBaseDataAsync()
    {
        _dbContext!.Users.Add(new UserEntity
        {
            Id = TestUserId,
            Email = "orphan-job-test@meepleai.test",
            DisplayName = "Orphan Job Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
        });
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = TestGameId,
            Title = "Orphan Job Test Game",
            BggId = Guid.NewGuid().GetHashCode() & 0x7FFFFFFF,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private Guid SeedJob(string jobStatus, string pdfState, DateTimeOffset? startedAt)
    {
        var pdfId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        _dbContext!.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = $"orphan-{jobId:N}.pdf",
            FilePath = $"/test/orphan-{jobId:N}.pdf",
            FileSizeBytes = 1_024,
            ContentType = "application/pdf",
            UploadedByUserId = TestUserId,
            SharedGameId = TestGameId,
            ProcessingState = pdfState,
            ProcessingError = "worker died mid-pipeline",
            UploadedAt = DateTime.UtcNow.AddHours(-1),
        });

        _dbContext.ProcessingJobs.Add(new ProcessingJobEntity
        {
            Id = jobId,
            PdfDocumentId = pdfId,
            UserId = TestUserId,
            Status = jobStatus,
            Priority = 0,
            CurrentStep = startedAt is null ? null : nameof(ProcessingStepType.Embed),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            StartedAt = startedAt,
            LastProgressAt = startedAt,
            MaxRetries = 3,
        });

        return jobId;
    }

    private OrphanedProcessingJobRecoveryService CreateService() =>
        new(_serviceProvider!.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrphanedProcessingJobRecoveryService>.Instance);

    [Fact]
    public async Task Recover_OrphanedProcessingJob_RequeuesJobAndRewindsPdf()
    {
        // ── Arrange ──────────────────────────────────────────────────────────────
        var orphanId = SeedJob("Processing", nameof(PdfProcessingState.Embedding),
            DateTimeOffset.UtcNow.AddMinutes(-90));
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // ── Act ───────────────────────────────────────────────────────────────────
        var recovered = await CreateService().RecoverOrphanedJobsAsync(TestCancellationToken);

        // ── Assert ────────────────────────────────────────────────────────────────
        recovered.Should().Be(1);

        var job = await _dbContext.ProcessingJobs.AsNoTracking()
            .FirstAsync(j => j.Id == orphanId, TestCancellationToken);

        job.Status.Should().Be(nameof(JobStatus.Queued),
            "an orphan did not fail — it lost its worker, so it goes back in the queue");
        job.StartedAt.Should().BeNull("the requeued job has not started yet");
        job.LastProgressAt.Should().BeNull("the idle clock must restart with the new attempt");
        job.CurrentStep.Should().BeNull("the pipeline restarts from the first step");

        var pdf = await _dbContext.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == job.PdfDocumentId, TestCancellationToken);

        pdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending),
            "the pipeline's atomic claim only accepts a Pending document; leaving it mid-pipeline "
            + "would make the worker skip and then falsely mark the job Completed");
        pdf.ProcessingError.Should().BeNull("the stale error from the killed worker must be cleared");
    }

    [Fact]
    public async Task Recover_LeavesQueuedAndTerminalJobsUntouched()
    {
        // ── Arrange ──────────────────────────────────────────────────────────────
        var queuedId = SeedJob("Queued", nameof(PdfProcessingState.Pending), startedAt: null);
        var completedId = SeedJob("Completed", nameof(PdfProcessingState.Ready),
            DateTimeOffset.UtcNow.AddHours(-1));
        var failedId = SeedJob("Failed", nameof(PdfProcessingState.Failed),
            DateTimeOffset.UtcNow.AddHours(-1));
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // ── Act ───────────────────────────────────────────────────────────────────
        var recovered = await CreateService().RecoverOrphanedJobsAsync(TestCancellationToken);

        // ── Assert ────────────────────────────────────────────────────────────────
        recovered.Should().Be(0, "only Processing rows can be orphaned by a restart");

        var jobs = await _dbContext.ProcessingJobs.AsNoTracking()
            .Where(j => j.Id == queuedId || j.Id == completedId || j.Id == failedId)
            .ToDictionaryAsync(j => j.Id, TestCancellationToken);

        jobs[queuedId].Status.Should().Be("Queued");
        jobs[completedId].Status.Should().Be("Completed");
        jobs[failedId].Status.Should().Be("Failed");

        var readyPdf = await _dbContext.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == jobs[completedId].PdfDocumentId, TestCancellationToken);
        readyPdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Ready),
            "a document that reached a terminal state must keep its outcome");
    }

    [Fact]
    public async Task Recover_OrphanWhosePdfAlreadyReady_RequeuesJobButKeepsPdfReady()
    {
        // The worker died after the pipeline had persisted Ready but before it wrote the job row.
        // Rewinding such a document would discard a completed, indexed ingest.
        var orphanId = SeedJob("Processing", nameof(PdfProcessingState.Ready),
            DateTimeOffset.UtcNow.AddMinutes(-45));
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var recovered = await CreateService().RecoverOrphanedJobsAsync(TestCancellationToken);

        recovered.Should().Be(1);

        var job = await _dbContext.ProcessingJobs.AsNoTracking()
            .FirstAsync(j => j.Id == orphanId, TestCancellationToken);
        job.Status.Should().Be(nameof(JobStatus.Queued), "the job row must stop consuming a slot");

        var pdf = await _dbContext.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == job.PdfDocumentId, TestCancellationToken);
        pdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Ready),
            "a Ready document must not be rewound — its ingest already succeeded");
    }
}

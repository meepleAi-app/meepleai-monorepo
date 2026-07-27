using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration test proving <see cref="BulkReindexFailedCommandHandler"/> resets the underlying
/// PdfDocument to <c>Pending</c> (not just re-queues the ProcessingJob) so the reprocessing loop
/// actually closes. Issue #3269 recovery-hardening (bug-hunt B12).
///
/// <para><b>The bug this guards against:</b> the handler re-queues each Failed ProcessingJob
/// (Failed → Queued, Low priority) but never touches the <c>pdf_documents</c> row, which stays
/// <c>Failed</c>. The Quartz worker then dequeues the Queued job and calls
/// <see cref="RelationalPdfClaimService.TryClaimPendingAsync"/>, whose atomic
/// <c>UPDATE ... WHERE processing_state = 'Pending'</c> matches 0 rows for a Failed PDF — the
/// pipeline logs "not in Pending state, skipping" and returns WITHOUT reprocessing, so the job is
/// marked Completed while the PDF is silently never re-indexed (a phantom "reindex succeeded").
/// Every sibling recovery path (<see cref="Api.BoundedContexts.DocumentProcessing.Application.Commands.ReindexDocumentCommandHandler"/>,
/// and the <c>BulkReindexReadyCommandHandler</c> fan-out) resets the PDF to Pending; this handler
/// was the outlier.</para>
///
/// <para>The pure-mock unit suite (<c>BulkReindexFailedCommandHandlerTests</c>) never constructs a
/// PdfDocument and asserts only on the job, so it cannot catch this — a Testcontainers test that
/// exercises the real claim SQL is the only faithful reproduction. It also runs under the
/// production <c>QueryTrackingBehavior.NoTracking</c> default so a missing <c>.AsTracking()</c>
/// on the reset query would still be caught.</para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3269")]
public sealed class BulkReindexFailedResetsPdfIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-0000000B1200");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-0000000B1200");

    public BulkReindexFailedResetsPdfIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_bulkfailed_reset_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // useNoTrackingDefault: true reproduces the production QueryTrackingBehavior.NoTracking
        // default, so a missing `.AsTracking()` on the PDF reset query would drop the mutation.
        var services = IntegrationServiceCollectionBuilder.CreateBase(
            _isolatedDbConnectionString, useNoTrackingDefault: true);

        services.AddSingleton<IHttpContextAccessor>(new Mock<IHttpContextAccessor>().Object);
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        await TestMigrationHelper.MigrateWithRetryAsync(_dbContext, TestCancellationToken);
        await SeedBaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }
        if (_serviceProvider is IAsyncDisposable d)
        {
            await d.DisposeAsync();
        }
        else
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors — test isolation already achieved
            }
        }
    }

    private async Task SeedBaseAsync()
    {
        _dbContext!.Set<UserEntity>().Add(new UserEntity
        {
            Id = TestUserId,
            Email = "bulkfailed-reset-test@meepleai.test",
            PasswordHash = "x",
            DisplayName = "BulkReindexFailed Reset Test",
        });
        _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = TestSharedGameId,
            Title = "BulkReindexFailed Reset Test Game",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    /// <summary>
    /// Seeds a Failed PdfDocument (with non-default reset-target fields so a dropped mutation is
    /// observable) plus a matching Failed ProcessingJob (retryable: RetryCount 0 &lt; MaxRetries 3).
    /// Seeds via its own fresh scope, deliberately separate from the one the mediator will use,
    /// mirroring the isolation of a real prior request's DbContext (see
    /// ReindexDocumentPersistsResetIntegrationTests for the rationale).
    /// </summary>
    private async Task<Guid> SeedFailedPdfWithFailedJobAsync()
    {
        var pdfId = Guid.NewGuid();

        using var seedScope = _serviceProvider!.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        seedDb.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = TestSharedGameId,
            UploadedByUserId = TestUserId,
            FileName = $"bulkfailed-{pdfId:N}.pdf",
            FilePath = $"/tmp/{pdfId:N}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            ProcessingState = "Failed",
            UploadedAt = DateTime.UtcNow,
            IndexerVersion = "v1.0",
            ProcessedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessingError = "boom — extractor timed out",
            RetryCount = 2,
            ErrorCategory = "Network",
            FailedAtState = "Chunking",
        });

        seedDb.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            UserId = TestUserId,
            Status = "Failed",
            Priority = (int)ProcessingPriority.Normal,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ErrorMessage = "boom — extractor timed out",
            RetryCount = 0,
            MaxRetries = 3,
        });

        await seedDb.SaveChangesAsync(TestCancellationToken);
        return pdfId;
    }

    [Fact]
    public async Task BulkReindexFailed_ResetsPdfToPending_SoTheWorkerCanClaimAndReprocess()
    {
        var pdfId = await SeedFailedPdfWithFailedJobAsync();

        var result = await _mediator!.Send(
            new BulkReindexFailedCommand(TestUserId), TestCancellationToken);

        result.EnqueuedCount.Should().Be(1, "the single retryable Failed job must be re-queued");

        // Verify from a FRESH, independent scope + AsNoTracking read — simulates what the Quartz
        // worker observes on the next tick.
        using var verifyScope = _serviceProvider!.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // (a) The job is re-queued at Low priority.
        var job = await verifyDb.Set<ProcessingJobEntity>()
            .AsNoTracking()
            .FirstAsync(j => j.PdfDocumentId == pdfId, TestCancellationToken);
        job.Status.Should().Be(nameof(JobStatus.Queued), "Retry() moves the job back to Queued");
        job.Priority.Should().Be((int)ProcessingPriority.Low, "bulk reindex re-queues at Low priority");

        // (b) The PDF row is reset to Pending — the crux. Without this the worker's Pending-only
        // atomic claim never matches and the reprocess silently no-ops.
        var pdf = await verifyDb.PdfDocuments
            .AsNoTracking()
            .FirstAsync(p => p.Id == pdfId, TestCancellationToken);
        pdf.ProcessingState.Should().Be(
            "Pending",
            "BulkReindexFailedCommandHandler must reset the PDF to Pending (like every sibling "
            + "recovery path) — re-queuing the job alone is a phantom success because the pipeline "
            + "only claims Pending PDFs.");
        pdf.ProcessingError.Should().BeNull("the stale error must be cleared on re-queue");
        pdf.ProcessedAt.Should().BeNull("ProcessedAt must be cleared on re-queue");
        pdf.RetryCount.Should().Be(0, "the PDF retry counter must reset on re-queue");
        pdf.ErrorCategory.Should().BeNull("ErrorCategory must be cleared on re-queue");
        pdf.FailedAtState.Should().BeNull("FailedAtState must be cleared on re-queue");
        pdf.IndexerVersion.Should().Be(
            "v1.0", "IndexerVersion is a provenance label and must NOT be touched by the re-queue");

        // (c) Prove the loop actually closes: the worker's atomic Pending-only claim now succeeds.
        using var claimScope = _serviceProvider!.CreateScope();
        var claimDb = claimScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var claimService = new RelationalPdfClaimService(claimDb);
        var claimed = await claimService.TryClaimPendingAsync(pdfId, TestCancellationToken);
        claimed.Should().BeTrue(
            "after the reset the PDF is Pending, so the worker's atomic claim (Pending → Extracting) "
            + "wins and reprocessing proceeds; before the fix it would return false and the PDF "
            + "would stay Failed forever.");
    }
}

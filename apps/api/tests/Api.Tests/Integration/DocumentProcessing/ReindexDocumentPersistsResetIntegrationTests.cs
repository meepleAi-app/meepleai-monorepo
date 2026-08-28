using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
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
/// Integration test proving <see cref="ReindexDocumentCommandHandler"/>'s field mutations
/// (ProcessingState reset to Pending, ProcessedAt/ProcessingError clear, RetryCount reset,
/// IndexerVersion stamp) are actually persisted when the fetch query runs under the SAME
/// <c>QueryTrackingBehavior.NoTracking</c> default configured for the production
/// <see cref="MeepleAiDbContext"/> (<c>InfrastructureServiceExtensions.cs</c> ~:178, PERF-06).
/// Issue #3269 follow-up (SP3 bulk re-index safety hardening).
///
/// <para><b>The bug this guards against:</b> under <c>QueryTrackingBehavior.NoTracking</c>, a plain
/// <c>_dbContext.PdfDocuments.FirstOrDefaultAsync(...)</c> (no <c>.AsTracking()</c>) returns a
/// detached POCO the change tracker knows nothing about. Setting properties on it (ProcessingState,
/// ProcessedAt, ProcessingError, RetryCount, ErrorCategory, FailedAtState, IndexerVersion) has zero
/// effect on <c>SaveChangesAsync</c> — no UPDATE is ever generated for that row. Meanwhile
/// <c>_dbContext.TextChunks.RemoveRange(chunks)</c> DOES persist, because <c>DbSet.RemoveRange</c>
/// implicitly attaches untracked entities before marking them Deleted — a different code path than
/// plain property mutation on a query result. Net effect: chunks are deleted but the document's
/// ProcessingState never resets — a phantom success that leaves the document stuck (broken),
/// exactly the scenario the SP3 <c>BulkReindexReadyCommandHandler</c> fan-out was built to fix.</para>
///
/// <para><b>Why a NEW test file instead of extending <see cref="ReindexDocumentVersionIntegrationTests"/>:</b>
/// that sibling suite's DbContext (built via <see cref="IntegrationServiceCollectionBuilder.CreateBase"/>
/// without <c>useNoTrackingDefault</c>) leaves EF Core's tracking-BY-DEFAULT behavior in place, so its
/// <c>Reindex_ExplicitVersion_PersistsOnEntity</c> test would keep passing even if <c>.AsTracking()</c>
/// were removed from the handler — it does not reproduce the production configuration and therefore
/// cannot catch this regression. This suite explicitly opts into NoTracking to match production.</para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3269")]
public sealed class ReindexDocumentPersistsResetIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000003269");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-000000003269");

    public ReindexDocumentPersistsResetIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_reindex_notrk_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // CRUX OF THE TEST: useNoTrackingDefault: true reproduces the production
        // QueryTrackingBehavior.NoTracking default. Without this flag the test DbContext would
        // track-by-default and mask the bug (see class doc comment).
        var services = IntegrationServiceCollectionBuilder.CreateBase(
            _isolatedDbConnectionString, useNoTrackingDefault: true);

        // Best-effort enqueue fan-out (EnqueuePdfCommand) needs these registered so the inner
        // mediator.Send resolves cleanly instead of being swallowed by the handler's CA1031 catch.
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
            Email = "reindex-notrk-test@meepleai.test",
            PasswordHash = "x",
            DisplayName = "Reindex NoTracking Test",
        });
        _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = TestSharedGameId,
            Title = "Reindex NoTracking Test Game",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    /// <summary>
    /// Seeds a Ready PdfDocument with non-default reset-target field values (so a bug that drops
    /// the mutations is observable, not masked by fields that already happen to equal their
    /// post-reset value) plus 2 associated TextChunks.
    ///
    /// IMPORTANT: seeds via its OWN fresh scope/DbContext instance, deliberately separate from
    /// the one the mediator call will use later. Reusing the same <see cref="MeepleAiDbContext"/>
    /// instance for both seeding (via <c>Add()</c>, which always tracks regardless of the
    /// NoTracking default) and the handler's later query would leave the seeded entities tracked
    /// (Unchanged) in that same context's change tracker. The handler's own NoTracking query then
    /// materializes SEPARATE, untracked instances for the same rows — and
    /// <c>TextChunks.RemoveRange(...)</c> on those detached duplicates throws
    /// <see cref="InvalidOperationException"/> ("already being tracked") because EF Core refuses
    /// to attach a second instance under the same key. That collision is a test-harness artifact,
    /// not the production bug: in production the row was written in a wholly separate prior
    /// request with its own scoped DbContext, long disposed by the time reindex runs. Using an
    /// isolated seeding scope here reproduces that same isolation.
    /// </summary>
    private async Task<Guid> SeedReadyPdfWithChunksAsync()
    {
        var pdfId = Guid.NewGuid();

        using var seedScope = _serviceProvider!.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var pdf = new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = TestSharedGameId,
            UploadedByUserId = TestUserId,
            FileName = $"reindex-notrk-{pdfId:N}.pdf",
            FilePath = $"/tmp/{pdfId:N}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            IndexerVersion = "v1.0",
            ProcessedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessingError = "stale-error-from-previous-run",
            RetryCount = 3,
            ErrorCategory = "Network",
            FailedAtState = "Chunking",
        };
        seedDb.PdfDocuments.Add(pdf);

        for (var i = 0; i < 2; i++)
        {
            seedDb.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                SharedGameId = TestSharedGameId,
                Content = $"Chunk {i} content",
                ChunkIndex = i,
                PageNumber = i + 1,
                CharacterCount = 20,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await seedDb.SaveChangesAsync(TestCancellationToken);
        return pdfId;
    }

    [Fact]
    public async Task Reindex_UnderProductionNoTrackingDefault_PersistsStateResetAndDeletesChunks()
    {
        var pdfId = await SeedReadyPdfWithChunksAsync();

        await _mediator!.Send(
            new ReindexDocumentCommand(pdfId, IndexerVersionRegistry.Current.Version),
            TestCancellationToken);

        // Verify from a FRESH, independent scope + AsNoTracking read — simulates what any other
        // request/pipeline worker would observe after the command completes.
        using var verifyScope = _serviceProvider!.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var reloaded = await verifyDb.PdfDocuments
            .AsNoTracking()
            .FirstAsync(p => p.Id == pdfId, TestCancellationToken);

        reloaded.ProcessingState.Should().Be(
            "Pending",
            "ReindexDocumentCommandHandler must reset ProcessingState even when the fetch query "
            + "runs under QueryTrackingBehavior.NoTracking (the production default) — a missing "
            + "`.AsTracking()` silently drops this mutation, leaving the document stuck at its "
            + "pre-reindex state forever.");
        reloaded.ProcessedAt.Should().BeNull("ProcessedAt must be cleared on reindex");
        reloaded.ProcessingError.Should().BeNull("ProcessingError must be cleared on reindex");
        reloaded.RetryCount.Should().Be(0, "RetryCount must reset to 0 on reindex");
        reloaded.ErrorCategory.Should().BeNull("ErrorCategory must be cleared on reindex");
        reloaded.FailedAtState.Should().BeNull("FailedAtState must be cleared on reindex");
        reloaded.IndexerVersion.Should().Be(
            IndexerVersionRegistry.Current.Version,
            "IndexerVersion must be stamped with the resolved version on reindex");

        var remainingChunks = await verifyDb.TextChunks
            .AsNoTracking()
            .Where(tc => tc.PdfDocumentId == pdfId)
            .ToListAsync(TestCancellationToken);
        remainingChunks.Should().BeEmpty("all TextChunks for the document must be deleted on reindex");
    }

    /// <summary>
    /// Fills the processing queue to <see cref="ProcessingJob.MaxQueueSize"/> with Queued jobs (all
    /// pointing at one throwaway PDF) so the next <c>EnqueuePdfCommand → ProcessingJob.Create</c>
    /// hits the capacity guard and throws <see cref="ConflictException"/>. Seeded via an isolated
    /// scope so the rows are committed before the reindex runs.
    /// </summary>
    private async Task SaturateQueueAsync()
    {
        using var seedScope = _serviceProvider!.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var fillerPdfId = Guid.NewGuid();
        seedDb.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = fillerPdfId,
            SharedGameId = TestSharedGameId,
            UploadedByUserId = TestUserId,
            FileName = $"filler-{fillerPdfId:N}.pdf",
            FilePath = $"/tmp/{fillerPdfId:N}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            ProcessingState = "Pending",
            UploadedAt = DateTime.UtcNow,
        });

        for (var i = 0; i < ProcessingJob.MaxQueueSize; i++)
        {
            seedDb.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = fillerPdfId,
                UserId = TestUserId,
                Status = nameof(JobStatus.Queued),
                Priority = (int)ProcessingPriority.Normal,
                CreatedAt = DateTimeOffset.UtcNow,
                RetryCount = 0,
                MaxRetries = 3,
            });
        }

        await seedDb.SaveChangesAsync(TestCancellationToken);
    }

    [Fact]
    public async Task Reindex_WhenQueueFull_RollsBackReset_NoPhantomStrand()
    {
        var pdfId = await SeedReadyPdfWithChunksAsync(); // Ready, IndexerVersion "v1.0", 2 chunks
        await SaturateQueueAsync();                      // queue at MaxQueueSize → enqueue will fail

        var act = () => _mediator!.Send(
            new ReindexDocumentCommand(pdfId, IndexerVersionRegistry.Current.Version),
            TestCancellationToken);

        // (a) The failed enqueue surfaces as a retryable conflict — never a phantom success.
        await act.Should().ThrowAsync<ConflictException>();

        // Verify from a fresh scope: the destructive reset was ROLLED BACK.
        using var verifyScope = _serviceProvider!.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var reloaded = await verifyDb.PdfDocuments
            .AsNoTracking()
            .FirstAsync(p => p.Id == pdfId, TestCancellationToken);
        // (b) PDF untouched: still Ready, still v1.0 (B4 — NOT stamped to the target, so recovery
        // selectors still see it), chunks intact.
        reloaded.ProcessingState.Should().Be(
            "Ready", "the destructive reset must roll back when the enqueue fails (no phantom strand)");
        reloaded.IndexerVersion.Should().Be(
            "v1.0", "IndexerVersion must NOT be stamped to the target on a rolled-back reindex (B4)");

        var chunks = await verifyDb.TextChunks
            .AsNoTracking()
            .Where(tc => tc.PdfDocumentId == pdfId)
            .ToListAsync(TestCancellationToken);
        chunks.Should().HaveCount(2, "the chunk delete must roll back with the reset — no data loss");

        // (c) No ProcessingJob was created for the target PDF (the fan-out left no strand).
        var jobForPdf = await verifyDb.Set<ProcessingJobEntity>()
            .AsNoTracking()
            .AnyAsync(j => j.PdfDocumentId == pdfId, TestCancellationToken);
        jobForPdf.Should().BeFalse("a rolled-back reindex must not leave an orphan job for the PDF");
    }
}

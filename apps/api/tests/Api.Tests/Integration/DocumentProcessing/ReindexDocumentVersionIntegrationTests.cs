using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for ReindexDocumentCommandHandler version selector. Issue #1673.
/// Verifies version persistence + audit row (success + conflict) + 409 on in-flight reindex
/// using a real PostgreSQL instance via Testcontainers.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1673")]
public sealed class ReindexDocumentVersionIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000001673");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-000000001673");

    public ReindexDocumentVersionIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_reindex_v_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Required for audit tests: replicate Program.cs MediatR pipeline behavior wiring.
        // CreateBase does not register IPipelineBehavior — that is a Program.cs concern.
        // Without these, tests 4 and 5 assert against an empty outbox and fail.
        services.AddSingleton<IHttpContextAccessor>(
            new Mock<IHttpContextAccessor>().Object); // returns null HttpContext — behavior handles this
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));

        // Issue #2023: tests verifying the Pending → Extracting handoff need the production
        // RelationalPdfClaimService (raw SQL atomic UPDATE) — IPdfClaimService is NOT in the
        // IntegrationServiceCollectionBuilder base because most reindex tests don't exercise
        // the claim step. Registering it here keeps the build minimal for all other tests.
        services.AddScoped<IPdfClaimService, RelationalPdfClaimService>();

        // Issue #2023 (code review CRITICAL): ReindexDocumentCommandHandler fans out to
        // EnqueuePdfCommand, whose handler depends on IPdfDocumentRepository +
        // IProcessingJobRepository. Without these the inner _mediator.Send(EnqueuePdfCommand)
        // throws InvalidOperationException which the outer reindex handler swallows in its
        // best-effort catch (CA1031). The SaveChanges-side assertion would still pass — but the
        // test would not be exercising the EnqueuePdf path it claims to. Registering the
        // production implementations here matches the production DI exactly (see
        // DocumentProcessingServiceExtensions.cs lines 47, 49). Other reindex tests in this
        // class can rely on the same enqueue path going through cleanly.
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        await MigrateWithRetryAsync(_dbContext);
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
            Email = "reindex-v-test@meepleai.test",
            PasswordHash = "x",
            DisplayName = "Reindex V Test",
        });
        _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = TestSharedGameId,
            Title = "Reindex V Test Game",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<PdfDocumentEntity> SeedPdfAsync(string state = "Ready", string? indexerVersion = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "reindex-v.pdf",
            FilePath = "/tmp/reindex-v.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = TestUserId,
            SharedGameId = TestSharedGameId,
            ProcessingState = state,
            IndexerVersion = indexerVersion,
        };
        _dbContext!.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return pdf;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reindex_ExplicitVersion_PersistsOnEntity()
    {
        var pdf = await SeedPdfAsync();

        await _mediator!.Send(
            new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
            TestCancellationToken);

        var reloaded = await _dbContext!.PdfDocuments
            .AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        reloaded.IndexerVersion.Should().Be(IndexerVersionRegistry.Current.Version);
        reloaded.ProcessingState.Should().Be("Pending");
    }

    [Fact]
    public async Task Reindex_NullVersionWithStoredV0_KeepsStoredValue()
    {
        // v0 backfill marker stored → reindex without override keeps stored value, NOT Current.
        var legacy = IndexerVersionRegistry.Legacy.Version;
        var pdf = await SeedPdfAsync(indexerVersion: legacy);

        await _mediator!.Send(
            new ReindexDocumentCommand(pdf.Id),
            TestCancellationToken);

        var reloaded = await _dbContext!.PdfDocuments
            .AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        reloaded.IndexerVersion.Should().Be(legacy);
    }

    [Fact]
    public async Task Reindex_InFlight_ThrowsConflictException()
    {
        var pdf = await SeedPdfAsync(state: "Chunking");

        var act = () => _mediator!.Send(
            new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
            TestCancellationToken);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Reindex_Success_WritesAuditOutboxRow()
    {
        var pdf = await SeedPdfAsync();

        await _mediator!.Send(
            new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
            TestCancellationToken);

        // AuditOutboxEntity.PayloadJson is a JSON blob — fetch all rows and filter client-side
        // to avoid jsonb operator dependencies in tests.
        var rows = await _dbContext!.AuditOutbox.AsNoTracking().ToListAsync(TestCancellationToken);
        rows.Should().Contain(
            r => HasMatchingAuditPayload(r.PayloadJson, "DocumentReindex", "Document"),
            "a successful reindex must write an audit outbox row with Action=DocumentReindex Resource=Document");
    }

    [Fact]
    [Trait("Issue", "2023")]
    public async Task Reindex_FromFailedState_PersistsPendingAndIsClaimableByPipeline()
    {
        // Issue #2023: ensure the documented Failed → Pending transition is committed to the
        // database before the Quartz pipeline picks it up. The original bug report observed the
        // pipeline log "PDF not in Pending state (already claimed or terminal), skipping" after a
        // successful reindex; this test reproduces the exact end-to-end handoff (ReindexDocumentCommand
        // → DB state Pending + ProcessingJob queued → RelationalPdfClaimService.TryClaimPendingAsync
        // → state Extracting) against a real PostgreSQL instance, so any future regression on the
        // SaveChanges or enqueue boundary of the reindex handler fails here.
        var pdf = await SeedPdfAsync(state: "Failed");

        await _mediator!.Send(
            new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
            TestCancellationToken);

        // Read with AsNoTracking + fresh scope to simulate what the Quartz pipeline sees
        // when it runs in a different scope ~seconds later.
        using (var verifyScope = _serviceProvider!.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var afterReindex = await verifyDb.PdfDocuments
                .AsNoTracking()
                .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);

            afterReindex.ProcessingState.Should().Be(
                "Pending",
                "ReindexDocumentCommandHandler must commit ProcessingState = Pending in the same "
                + "SaveChanges call as the rest of the reset fields (issue #2023). A fresh scope "
                + "must observe the committed state without any L1-cache tricks.");
            afterReindex.ProcessedAt.Should().BeNull();
            afterReindex.ProcessingError.Should().BeNull();
            afterReindex.RetryCount.Should().Be(0);

            // Issue #2023 (code review CRITICAL follow-up): also assert that EnqueuePdfCommand
            // actually created a queued ProcessingJob. The reindex handler catches every
            // exception from the enqueue mediator.Send (lines 113-128 in
            // ReindexDocumentCommandHandler.cs, CA1031), so a missing IProcessingJobRepository
            // / IPdfDocumentRepository registration would otherwise silently swallow the
            // failure and this test would still see ProcessingState=Pending. The job-row check
            // is what proves the enqueue path is also exercised.
            var queuedJob = await verifyDb.ProcessingJobs
                .AsNoTracking()
                .Where(j => j.PdfDocumentId == pdf.Id)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefaultAsync(TestCancellationToken);

            queuedJob.Should().NotBeNull(
                "ReindexDocumentCommandHandler must enqueue a ProcessingJob via "
                + "EnqueuePdfCommand. If this is null, the inner mediator.Send was likely "
                + "swallowed by the handler's CA1031 catch — verify IProcessingJobRepository "
                + "and IPdfDocumentRepository are both registered in InitializeAsync.");
        }

        // Verify the production RelationalPdfClaimService (raw SQL UPDATE ... WHERE
        // processing_state = 'Pending') can successfully claim the document — this is the
        // operation that was logged as "skipping" in the bug report.
        //
        // The claim service uses ExecuteSqlInterpolatedAsync. Because the test DbContext is
        // configured with EnableRetryOnFailure (NpgsqlRetryingExecutionStrategy), raw SQL
        // outside the strategy's ExecuteAsync wrapper is not safely retryable — wrap it.
        using (var claimScope = _serviceProvider!.CreateScope())
        {
            var claimDb = claimScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var claimService = claimScope.ServiceProvider.GetRequiredService<IPdfClaimService>();
            var strategy = claimDb.Database.CreateExecutionStrategy();
            var claimed = await strategy.ExecuteAsync(
                () => claimService.TryClaimPendingAsync(pdf.Id, TestCancellationToken));

            claimed.Should().BeTrue(
                "the pipeline claim service must observe Pending and atomically transition to "
                + "Extracting; if this fails the bug from #2023 has regressed.");
        }

        // Final state check: after the claim, the PDF is in Extracting.
        using (var postClaimScope = _serviceProvider!.CreateScope())
        {
            var postClaimDb = postClaimScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var afterClaim = await postClaimDb.PdfDocuments
                .AsNoTracking()
                .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);

            afterClaim.ProcessingState.Should().Be("Extracting");
        }
    }

    [Fact]
    public async Task Reindex_ConflictOnInFlight_StillWritesAuditOutboxRow()
    {
        // T4 code review carry-forward: failed admin actions must be forensically auditable.
        // [AuditableAction] without [AtomicAudit] uses the best-effort path, which writes an
        // Error audit row AFTER the handler throws. Verify the row exists despite the exception.
        var pdf = await SeedPdfAsync(state: "Chunking");

        var act = () => _mediator!.Send(
            new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
            TestCancellationToken);
        await act.Should().ThrowAsync<ConflictException>();

        var rows = await _dbContext!.AuditOutbox.AsNoTracking().ToListAsync(TestCancellationToken);
        rows.Should().Contain(
            r => HasMatchingAuditPayload(r.PayloadJson, "DocumentReindex", "Document", "Error"),
            "a failed (conflict) reindex must write an Error audit row — not a Success row — for forensic traceability");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Overload without Result check (Action + Resource only). Required as a separate method
    /// (not a default argument) because <see cref="FluentAssertions.GenericCollectionAssertions{T}.Contain(System.Linq.Expressions.Expression{System.Func{T,bool}}, string, object[])"/>
    /// takes an expression tree, and C# forbids calls with optional arguments inside expression trees (CS0854).
    /// </summary>
    private static bool HasMatchingAuditPayload(
        string payloadJson,
        string expectedAction,
        string expectedResource)
        => HasMatchingAuditPayload(payloadJson, expectedAction, expectedResource, null);

    /// <summary>
    /// Deserializes <paramref name="payloadJson"/> and checks whether it contains the expected
    /// Action + Resource + optional Result values. Client-side filter to avoid jsonb SQL operator
    /// dependencies. When <paramref name="expectedResult"/> is non-null, the Result property in the
    /// payload (e.g. "Success" or "Error") must also match.
    /// </summary>
    private static bool HasMatchingAuditPayload(
        string payloadJson,
        string expectedAction,
        string expectedResource,
        string? expectedResult)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Action", out var actionEl)
                || !string.Equals(actionEl.GetString(), expectedAction, StringComparison.Ordinal))
            {
                return false;
            }

            if (!root.TryGetProperty("Resource", out var resourceEl)
                || !string.Equals(resourceEl.GetString(), expectedResource, StringComparison.Ordinal))
            {
                return false;
            }

            if (expectedResult is not null)
            {
                if (!root.TryGetProperty("Result", out var resultEl)
                    || !string.Equals(resultEl.GetString(), expectedResult, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task MigrateWithRetryAsync(MeepleAiDbContext context)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await context.Database.MigrateAsync(TestCancellationToken);
                return;
            }
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }
}

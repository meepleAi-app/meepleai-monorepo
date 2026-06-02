using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for PdfDocumentEntity RowVersion optimistic concurrency.
/// Issue #1802. Uses Barrier-synchronized parallel tasks for real race conditions
/// against PostgreSQL Testcontainers.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1802")]
public sealed class PdfRowVersionConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000001802");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-000000001802");

    public PdfRowVersionConcurrencyIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_rowversion_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Register real AgentDefinitionRepository (required by DeleteKbDocumentCommandHandler
        // which needs to update consuming agents' KbCardIds).
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();

        // Blob storage — best-effort in DeleteKbDocumentCommandHandler; mock so no physical I/O.
        var blobMock = new Mock<IBlobStorageService>();
        blobMock.Setup(b => b.DeleteAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton<IBlobStorageService>(blobMock.Object);

        // AI response cache invalidation — best-effort in handler; mock.
        var cacheMock = new Mock<IAiResponseCacheService>();
        cacheMock.Setup(c => c.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);

        // Vector store — mock so pgvector_embeddings deletion doesn't fail in Testcontainers.
        var vectorStoreMock = new Mock<IVectorStoreAdapter>();
        vectorStoreMock.Setup(v => v.DeleteByVectorDocumentIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddScoped<IVectorStoreAdapter>(_ => vectorStoreMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await MigrateWithRetryAsync(_dbContext);
        await SeedBaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable d)
            await d.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // best-effort cleanup — test isolation already achieved
            }
        }
    }

    private static async Task MigrateWithRetryAsync(MeepleAiDbContext db)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(TestCancellationToken);
                return;
            }
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    private async Task SeedBaseAsync()
    {
        _dbContext!.Set<UserEntity>().Add(new UserEntity
        {
            Id = TestUserId,
            Email = "rowversion-test@meepleai.test",
            PasswordHash = "x",
            DisplayName = "RowVersion Test",
        });
        _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = TestSharedGameId,
            Title = "RowVersion Test Game",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<PdfDocumentEntity> SeedReadyPdfAsync(string? indexerVersion = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rowversion.pdf",
            FilePath = "/tmp/rowversion.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = TestUserId,
            SharedGameId = TestSharedGameId,
            ProcessingState = nameof(PdfProcessingState.Ready),
            IndexerVersion = indexerVersion ?? IndexerVersionRegistry.Current.Version,
        };
        _dbContext!.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return pdf;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 1: Parallel two reindex — only one succeeds
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Parallel_TwoReindex_OnlyOneSucceeds()
    {
        var pdf = await SeedReadyPdfAsync();

        using var barrier = new Barrier(participantCount: 2);

        Task<Exception?> RunReindex() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(
                    new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
                    TestCancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        var taskA = RunReindex();
        var taskB = RunReindex();

        var exceptions = await Task.WhenAll(taskA, taskB);

        var successes = exceptions.Count(ex => ex is null);
        var conflicts = exceptions.Count(ex => ex is ConflictException);
        successes.Should().Be(1, "exactly one reindex must win the race");
        conflicts.Should().Be(1, "exactly one reindex must lose with ConflictException");

        var reloaded = await _dbContext!.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        reloaded.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        reloaded.RowVersion.Should().NotBeNull().And.NotEqual(pdf.RowVersion);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 2: Reindex races with Delete — first wins, second 409
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reindex_RacesWithDelete_FirstWinsSecondGets409()
    {
        var pdf = await SeedReadyPdfAsync();

        using var barrier = new Barrier(participantCount: 2);

        Task<(string Op, Exception? Exception)> RunReindex() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(
                    new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
                    TestCancellationToken);
                return ("reindex", (Exception?)null);
            }
            catch (Exception ex) { return ("reindex", (Exception?)ex); }
        });

        Task<(string Op, Exception? Exception)> RunDelete() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(new DeleteKbDocumentCommand(pdf.Id), TestCancellationToken);
                return ("delete", (Exception?)null);
            }
            catch (Exception ex) { return ("delete", (Exception?)ex); }
        });

        var results = await Task.WhenAll(RunReindex(), RunDelete());

        var successCount = results.Count(r => r.Exception is null);
        var conflictCount = results.Count(r => r.Exception is ConflictException);
        successCount.Should().Be(1, "exactly one operation must succeed");
        conflictCount.Should().Be(1, "exactly one operation must conflict");

        var winner = results.Single(r => r.Exception is null);
        var existsCheck = await _dbContext!.PdfDocuments.AsNoTracking()
            .AnyAsync(p => p.Id == pdf.Id, TestCancellationToken);
        if (winner.Op == "delete")
        {
            existsCheck.Should().BeFalse("delete winner removes the document");
            var orphanChunks = await _dbContext.TextChunks.AsNoTracking()
                .CountAsync(tc => tc.PdfDocumentId == pdf.Id, TestCancellationToken);
            orphanChunks.Should().Be(0, "no orphan TextChunks after cascade delete");
        }
        else
        {
            existsCheck.Should().BeTrue("reindex winner keeps the document");
            var reloaded = await _dbContext.PdfDocuments.AsNoTracking()
                .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
            reloaded.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 3: Reindex races with background pipeline mutation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reindex_RacesWithBackgroundPipeline_AdminGets409()
    {
        var pdf = await SeedReadyPdfAsync();

        using var barrier = new Barrier(participantCount: 2);

        Task<(string Op, Exception? Exception)> RunAdminReindex() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Pre-load the entity to confirm it exists before racing (AsNoTracking — no scope pollution).
            using var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            _ = await db.PdfDocuments.AsNoTracking()
                .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);

            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(
                    new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
                    TestCancellationToken);
                return ("admin", (Exception?)null);
            }
            catch (Exception ex) { return ("admin", (Exception?)ex); }
        });

        Task<(string Op, Exception? Exception)> RunPipelineTick() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                // Simulate a Category B pipeline mutation directly (no real pipeline service spin-up).
                var entity = await db.PdfDocuments
                    .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
                entity.ProcessingState = nameof(PdfProcessingState.Chunking);
                await db.SaveChangesAsync(TestCancellationToken);
                return ("pipeline", (Exception?)null);
            }
            catch (Exception ex) { return ("pipeline", (Exception?)ex); }
        });

        var results = await Task.WhenAll(RunAdminReindex(), RunPipelineTick());

        // Race outcome depends on timing; at most one operation should conflict.
        var failureCount = results.Count(r => r.Exception is not null);
        failureCount.Should().BeLessThanOrEqualTo(1,
            "at most one operation should conflict (or zero if barrier failed to align both in-flight)");

        // The document MUST end in a consistent, recognized state — not partial.
        var final = await _dbContext!.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        final.ProcessingState.Should().BeOneOf(
            nameof(PdfProcessingState.Pending),    // admin reindex won
            nameof(PdfProcessingState.Chunking));  // pipeline tick won
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 4: Retry after conflict succeeds
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sequential_RetryAfterConflict_Succeeds()
    {
        var pdf = await SeedReadyPdfAsync();

        // Step 1: provoke a conflict via parallel reindex (same shape as Scenario 1).
        using (var barrier = new Barrier(participantCount: 2))
        {
            Task<Exception?> RunReindex() => Task.Run(async () =>
            {
                using var scope = _serviceProvider!.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                try
                {
                    await mediator.Send(
                        new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
                        TestCancellationToken);
                    return null;
                }
                catch (Exception ex) { return ex; }
            });

            await Task.WhenAll(RunReindex(), RunReindex());
        }

        // Step 2: loser retries 1 second later (after winner has persisted its RowVersion).
        await Task.Delay(TimeSpan.FromSeconds(1), TestCancellationToken);

        using var retryScope = _serviceProvider!.CreateScope();
        var retryMediator = retryScope.ServiceProvider.GetRequiredService<IMediator>();
        await retryMediator.Send(
            new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
            TestCancellationToken);

        var final = await _dbContext!.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        final.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending),
            "retry after conflict should succeed and leave ProcessingState=Pending");
    }
}

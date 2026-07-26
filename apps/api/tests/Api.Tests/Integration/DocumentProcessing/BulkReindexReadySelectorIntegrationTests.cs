using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration test for the <see cref="BulkReindexReadyCommandHandler"/> PDF selector against a
/// real PostgreSQL instance (Testcontainers). Issue #3269 (SP3 RAG bulk re-index).
/// <para>
/// Proves the mandatory Postgres null-comparison guard on the selector: a plain
/// <c>indexer_version &lt;&gt; 'v1.1'</c> predicate silently drops rows where
/// <c>indexer_version IS NULL</c> (three-valued logic), so the handler MUST use
/// <c>IndexerVersion == null || IndexerVersion != target</c>. This test seeds a NULL-version
/// Ready row and asserts it IS selected — it fails against Npgsql SQL semantics if the
/// <c>== null</c> clause is dropped.
/// </para>
/// The fan-out mediator is mocked so it only records the dispatched <see cref="ReindexDocumentCommand"/>
/// ids without running the real reindex handler.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3269")]
public sealed class BulkReindexReadySelectorIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000003269");

    public BulkReindexReadySelectorIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_bulk_reindex_ready_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

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
            Email = "bulk-reindex-ready@meepleai.test",
            PasswordHash = "x",
            DisplayName = "Bulk Reindex Ready Test",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<PdfDocumentEntity> SeedPdfAsync(string state, string? indexerVersion)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "bulk-reindex.pdf",
            FilePath = "/tmp/bulk-reindex.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = TestUserId,
            ProcessingState = state,
            IndexerVersion = indexerVersion,
        };
        _dbContext!.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return pdf;
    }

    [Fact]
    public async Task Handle_SelectsNullVersionReadyRow_ExcludesTargetVersionAndNonReady()
    {
        // PDF-A: Ready + IndexerVersion = NULL  → MUST be selected (null-comparison guard).
        var pdfA = await SeedPdfAsync("Ready", indexerVersion: null);
        // PDF-B: Ready + already at target v1.1 → excluded (idempotent).
        await SeedPdfAsync("Ready", indexerVersion: IndexerVersionRegistry.Current.Version);
        // PDF-C: Failed + NULL                 → excluded (non-Ready).
        await SeedPdfAsync("Failed", indexerVersion: null);

        var sentIds = new List<Guid>();
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<ReindexDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ReindexDocumentCommand, CancellationToken>((c, _) => sentIds.Add(c.PdfId))
            .Returns(Task.CompletedTask);

        var jobRepoMock = new Mock<IProcessingJobRepository>();
        jobRepoMock
            .Setup(r => r.CountByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new BulkReindexReadyCommandHandler(
            _dbContext!,
            mediatorMock.Object,
            jobRepoMock.Object,
            NullLogger<BulkReindexReadyCommandHandler>.Instance);

        var result = await handler.Handle(
            new BulkReindexReadyCommand(TestUserId), TestCancellationToken);

        // Exactly one reindex, for the NULL-version Ready row. This is the assertion that fails
        // against real Npgsql if the selector drops the `IndexerVersion == null ||` clause.
        sentIds.Should().ContainSingle().Which.Should().Be(pdfA.Id);
        result.EnqueuedCount.Should().Be(1);
        result.SkippedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }
}

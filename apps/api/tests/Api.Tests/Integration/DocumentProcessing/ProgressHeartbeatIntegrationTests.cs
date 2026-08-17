using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Issue #3585 — proves the progress heartbeat actually WRITES on real Postgres.
/// <para>
/// The heartbeat is deliberately best-effort (wrapped in a catch so a failed beat never aborts an
/// ingest), which means a broken write is INVISIBLE at runtime: on staging the column stayed NULL
/// while jobs were embedding, with nothing in the logs above Debug. A unit test cannot catch that —
/// the InMemory provider does not even support <c>ExecuteUpdate</c>. This test runs the exact
/// statement the pipeline runs, against a real database, so a translation or type failure surfaces
/// in CI instead of silently disabling the stuck-job protection.
/// </para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3585")]
public sealed class ProgressHeartbeatIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000003585001");

    public ProgressHeartbeatIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_heartbeat_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

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

    private async Task<(Guid PdfId, Guid JobId)> SeedProcessingJobAsync()
    {
        var db = _dbContext!;

        db.Users.Add(new UserEntity
        {
            Id = TestUserId,
            Email = $"heartbeat-{Guid.NewGuid():N}@test.local",
            DisplayName = "Heartbeat Test",
            PasswordHash = "x",
            Role = "user",
        });

        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "big_rulebook.pdf",
            FilePath = "pdfs/big_rulebook.pdf",
            UploadedByUserId = TestUserId,
            ProcessingState = "Embedding",
        });

        var jobId = Guid.NewGuid();
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = jobId,
            PdfDocumentId = pdfId,
            UserId = TestUserId,
            Status = "Processing",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-40),
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-40),
            LastProgressAt = null,
        });

        await db.SaveChangesAsync(TestCancellationToken);
        db.ChangeTracker.Clear();
        return (pdfId, jobId);
    }

    [Fact]
    public async Task TheHeartbeatStatement_WritesLastProgressAt_OnRealPostgres()
    {
        var (pdfId, jobId) = await SeedProcessingJobAsync();
        var db = _dbContext!;
        var now = DateTimeOffset.UtcNow;

        // EXACTLY the statement PdfProcessingPipelineService.ReportProgressAsync runs.
        var affected = await db.Set<ProcessingJobEntity>()
            .Where(j => j.PdfDocumentId == pdfId && j.Status == "Processing")
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.LastProgressAt, now), TestCancellationToken);

        affected.Should().Be(1, "the heartbeat must update the active job for this PDF");

        db.ChangeTracker.Clear();
        var stored = await db.Set<ProcessingJobEntity>()
            .AsNoTracking()
            .FirstAsync(j => j.Id == jobId, TestCancellationToken);

        stored.LastProgressAt.Should().NotBeNull("a NULL here means the stuck-job guard is disabled");
        stored.LastProgressAt!.Value.Should().BeCloseTo(now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TheHeartbeat_DoesNotTouchJobsInOtherStates()
    {
        var (pdfId, _) = await SeedProcessingJobAsync();
        var db = _dbContext!;

        var queuedJobId = Guid.NewGuid();
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = queuedJobId,
            PdfDocumentId = pdfId,
            UserId = TestUserId,
            Status = "Queued",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(TestCancellationToken);
        db.ChangeTracker.Clear();

        var now = DateTimeOffset.UtcNow;
        await db.Set<ProcessingJobEntity>()
            .Where(j => j.PdfDocumentId == pdfId && j.Status == "Processing")
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.LastProgressAt, now), TestCancellationToken);

        db.ChangeTracker.Clear();
        var queued = await db.Set<ProcessingJobEntity>()
            .AsNoTracking()
            .FirstAsync(j => j.Id == queuedJobId, TestCancellationToken);

        queued.LastProgressAt.Should().BeNull("only the in-flight job reports progress");
    }
}

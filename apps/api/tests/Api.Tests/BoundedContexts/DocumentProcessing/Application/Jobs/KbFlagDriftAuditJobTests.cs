using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Issue #2248 (epic #2242, Sub #6 Block B): unit tests for the periodic reconcile
/// that repairs the "Ready ⇒ HasKnowledgeBase" invariant when the inline projection
/// was missed (e.g. a VectorDocumentIndexedEvent carrying a null SharedGameId).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "2248")]
public sealed class KbFlagDriftAuditJobTests
{
    private readonly Mock<ILogger<KbFlagDriftAuditJob>> _logger = new();
    private readonly Mock<IHybridCacheService> _cache = new();
    private readonly Mock<ICacheInvalidationRetryPolicy> _retryPolicy = new();

    public KbFlagDriftAuditJobTests()
    {
        _cache
            .Setup(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        // Execute the wrapped invalidation so the cache mock is genuinely exercised.
        _retryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, ValueTask>, string, CancellationToken>((op, _, ct) => op(ct).AsTask());
    }

    private KbFlagDriftAuditJob CreateJob(MeepleAiDbContext db) =>
        new(db, _cache.Object, _retryPolicy.Object, _logger.Object);

    private static PdfDocumentEntity ReadyPdf(Guid id, Guid sharedGameId) =>
        new()
        {
            Id = id,
            SharedGameId = sharedGameId,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow.AddMinutes(-30),
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow.AddMinutes(-25),
            Language = "en",
            Tags = new List<string>(),
        };

    private static SharedGameEntity Game(Guid id, bool hasKb) =>
        new()
        {
            Id = id,
            Title = "Test Game",
            YearPublished = 2020,
            Description = "Desc",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 30,
            MinAge = 8,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Status = 1,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            HasKnowledgeBase = hasKb,
        };

    private static IJobExecutionContext FakeContext(CancellationToken token = default)
    {
        var ctx = new Mock<IJobExecutionContext>();
        ctx.SetupGet(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        ctx.SetupGet(c => c.CancellationToken).Returns(token);
        ctx.SetupProperty(c => c.Result);
        return ctx.Object;
    }

    [Fact]
    public async Task Execute_NoDriftedGames_ReturnsZeroAndDoesNotTouchCache()
    {
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Healthy state: PDF Ready and SharedGame.HasKnowledgeBase=true (invariant holds)
        db.SharedGames.Add(Game(sharedGameId, hasKb: true));
        db.PdfDocuments.Add(ReadyPdf(Guid.NewGuid(), sharedGameId));
        await db.SaveChangesAsync();

        var ctx = FakeContext();
        await CreateJob(db).Execute(ctx);

        ctx.Result.Should().BeEquivalentTo(new { RepairedGames = 0 });
        _cache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_DriftDetected_RepairsFlagAndInvalidatesCache()
    {
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Drifted state: PDF Ready but SharedGame.HasKnowledgeBase=false.
        db.SharedGames.Add(Game(sharedGameId, hasKb: false));
        db.PdfDocuments.Add(ReadyPdf(Guid.NewGuid(), sharedGameId));
        await db.SaveChangesAsync();

        var ctx = FakeContext();
        await CreateJob(db).Execute(ctx);

        ctx.Result.Should().BeEquivalentTo(new { RepairedGames = 1 });

        // The flag is now actually repaired in the DB (the key new behaviour).
        var reloaded = await db.SharedGames.AsNoTracking().SingleAsync(g => g.Id == sharedGameId);
        reloaded.HasKnowledgeBase.Should().BeTrue();

        // Cache invalidated: the "search-games" list tag + the per-game detail tag.
        _cache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync("search-games", It.IsAny<CancellationToken>()),
            Times.Once);
        _cache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync($"shared-game:{sharedGameId}", It.IsAny<CancellationToken>()),
            Times.Once);

        // #4659: and the catalog rails, whose trending entry carries the same flag.
        _cache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync(CatalogRailsCache.Tag, It.IsAny<CancellationToken>()),
            Times.Once);

        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("KbFlagDrift reconciled", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// #4659: le rail sono l'ultimo passo. Se si arrendono, la lista e il dettaglio sono già evinti e la riga di
    /// errore dello SLO è già stata scritta: non è un'evizione che ne salta altre.
    /// </summary>
    [Fact]
    public async Task Execute_WhenTheRailsEvictionGivesUp_TheDetailsAreAlreadyEvicted_AndTheSloIsLogged()
    {
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(Game(sharedGameId, hasKb: false));
        db.PdfDocuments.Add(ReadyPdf(Guid.NewGuid(), sharedGameId));
        await db.SaveChangesAsync();

        _retryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(),
                "kb-reconcile.rails",
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new InvalidOperationException("redis down")));

        var ctx = FakeContext();
        await CreateJob(db).Execute(ctx);

        _cache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync($"shared-game:{sharedGameId}", It.IsAny<CancellationToken>()),
            Times.Once,
            "the detail was evicted before the rails step ran");
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("SLO=0 violated", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "the SLO line is logged before the rails step");

        // #4659: the step that gave up is logged and does not turn the reconciliation into an error.
        ctx.Result.Should().BeEquivalentTo(new { RepairedGames = 1 });
    }

    /// <summary>
    /// #4659: ogni passo di evizione è a sé: se si arrendono tutti (lista, dettaglio e rail) il job li prova
    /// comunque tutti, scrive la riga dello SLO e la riconciliazione, che è già salvata, resta riuscita. I passi
    /// girano sotto il token di Quartz, a differenza degli handler.
    /// </summary>
    [Fact]
    public async Task Execute_WhenEveryEvictionGivesUp_EveryStepIsTried_TheSloIsLogged_AndTheResultIsTheReconciliation()
    {
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(Game(sharedGameId, hasKb: false));
        db.PdfDocuments.Add(ReadyPdf(Guid.NewGuid(), sharedGameId));
        await db.SaveChangesAsync();
        var policy = GivingUpRetryPolicy.Always();
        var job = new KbFlagDriftAuditJob(db, _cache.Object, policy, _logger.Object);
        using var quartz = new CancellationTokenSource();

        var ctx = FakeContext(quartz.Token);
        await job.Execute(ctx);

        policy.Operations.Should().Equal("kb-reconcile.list", "kb-reconcile.detail", "kb-reconcile.rails");
        policy.Tokens.Should().OnlyContain(t => t == quartz.Token, "the job runs under the scheduler's token");
        ctx.Result.Should().BeEquivalentTo(new { RepairedGames = 1 });
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Cache eviction failed", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("SLO=0 violated", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// #4659: un passo di evizione che si arrende (Redis che non risponde dopo i tentativi) si registra e non
    /// salta gli altri: la lista persa non toglie il dettaglio né le rail, e la riconciliazione resta riuscita.
    /// </summary>
    [Fact]
    public async Task Execute_WhenTheListEvictionGivesUp_TheDetailsAndTheRailsAreStillEvicted()
    {
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(Game(sharedGameId, hasKb: false));
        db.PdfDocuments.Add(ReadyPdf(Guid.NewGuid(), sharedGameId));
        await db.SaveChangesAsync();
        var policy = GivingUpRetryPolicy.ForOperation("kb-reconcile.list");
        var job = new KbFlagDriftAuditJob(db, _cache.Object, policy, _logger.Object);

        var ctx = FakeContext();
        await job.Execute(ctx);

        policy.Operations.Should().Equal("kb-reconcile.list", "kb-reconcile.detail", "kb-reconcile.rails");
        ctx.Result.Should().BeEquivalentTo(new { RepairedGames = 1 });
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Cache eviction failed", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_PdfWithoutSharedGameId_DoesNotDrift()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var orphan = ReadyPdf(Guid.NewGuid(), sharedGameId: Guid.NewGuid());
        orphan.SharedGameId = null;
        db.PdfDocuments.Add(orphan);
        await db.SaveChangesAsync();

        var ctx = FakeContext();
        await CreateJob(db).Execute(ctx);

        ctx.Result.Should().BeEquivalentTo(new { RepairedGames = 0 });
    }

    [Fact]
    public async Task Execute_PdfNotReady_DoesNotRepairFlag()
    {
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        db.SharedGames.Add(Game(sharedGameId, hasKb: false));
        var indexing = ReadyPdf(Guid.NewGuid(), sharedGameId);
        indexing.ProcessingState = "Indexing"; // in progress, not yet Ready
        db.PdfDocuments.Add(indexing);
        await db.SaveChangesAsync();

        var ctx = FakeContext();
        await CreateJob(db).Execute(ctx);

        ctx.Result.Should().BeEquivalentTo(new { RepairedGames = 0 });
        var reloaded = await db.SharedGames.AsNoTracking().SingleAsync(g => g.Id == sharedGameId);
        reloaded.HasKnowledgeBase.Should().BeFalse();
    }
}

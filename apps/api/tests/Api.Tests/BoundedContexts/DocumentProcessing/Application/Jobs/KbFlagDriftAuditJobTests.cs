using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Issue #2248 (epic #2242, Sub #6 Block B): unit tests for the periodic audit
/// that guards the "Ready ⇒ HasKnowledgeBase" invariant.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "2248")]
public sealed class KbFlagDriftAuditJobTests
{
    private readonly Mock<ILogger<KbFlagDriftAuditJob>> _logger = new();

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

    private static IJobExecutionContext FakeContext()
    {
        var ctx = new Mock<IJobExecutionContext>();
        ctx.SetupGet(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        ctx.SetupProperty(c => c.Result);
        return ctx.Object;
    }

    [Fact]
    public async Task Execute_NoDriftedRows_LogsZeroAndReturns()
    {
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Healthy state: PDF Ready and SharedGame.HasKnowledgeBase=true (the invariant holds)
        db.SharedGames.Add(Game(sharedGameId, hasKb: true));
        db.PdfDocuments.Add(ReadyPdf(Guid.NewGuid(), sharedGameId));
        await db.SaveChangesAsync();

        var job = new KbFlagDriftAuditJob(db, _logger.Object);
        var ctx = FakeContext();

        await job.Execute(ctx);

        ctx.Result.Should().BeEquivalentTo(new { DriftedRows = 0 });
    }

    [Fact]
    public async Task Execute_DriftDetected_LogsWarningAndIncrementsCounter()
    {
        var sharedGameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Drifted state: PDF Ready but SharedGame.HasKnowledgeBase=false (the bug
        // #2243 Block A patches and Sub #2 architecture refactor structurally fixes).
        db.SharedGames.Add(Game(sharedGameId, hasKb: false));
        db.PdfDocuments.Add(ReadyPdf(pdfId, sharedGameId));
        await db.SaveChangesAsync();

        var job = new KbFlagDriftAuditJob(db, _logger.Object);
        var ctx = FakeContext();

        await job.Execute(ctx);

        ctx.Result.Should().BeEquivalentTo(new { DriftedRows = 1 });

        // Warning per drifted row + 1 Error summary line.
        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("KbFlagDrift detected", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("SLO=0 violated", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_PdfWithoutSharedGameId_DoesNotDrift()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // A PDF with null SharedGameId (private game / orphan) must not appear in the audit.
        var orphan = ReadyPdf(Guid.NewGuid(), sharedGameId: Guid.NewGuid());
        orphan.SharedGameId = null;
        db.PdfDocuments.Add(orphan);
        await db.SaveChangesAsync();

        var job = new KbFlagDriftAuditJob(db, _logger.Object);
        var ctx = FakeContext();

        await job.Execute(ctx);

        ctx.Result.Should().BeEquivalentTo(new { DriftedRows = 0 });
    }

    [Fact]
    public async Task Execute_PdfNotReady_DoesNotDrift()
    {
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        db.SharedGames.Add(Game(sharedGameId, hasKb: false));
        var indexing = ReadyPdf(Guid.NewGuid(), sharedGameId);
        indexing.ProcessingState = "Indexing"; // in progress, not yet Ready
        db.PdfDocuments.Add(indexing);
        await db.SaveChangesAsync();

        var job = new KbFlagDriftAuditJob(db, _logger.Object);
        var ctx = FakeContext();

        await job.Execute(ctx);

        ctx.Result.Should().BeEquivalentTo(new { DriftedRows = 0 });
    }
}

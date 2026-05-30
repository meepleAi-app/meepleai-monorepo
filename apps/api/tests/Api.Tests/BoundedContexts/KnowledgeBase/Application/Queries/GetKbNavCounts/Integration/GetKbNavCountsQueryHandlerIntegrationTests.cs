using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts.Integration;

/// <summary>
/// Integration tests for <see cref="GetKbNavCountsQueryHandler"/> against a real PostgreSQL
/// database via Testcontainers. Validates that the EF Core SQL translation of
/// <c>CountByStatusesAsync</c> (IN clause) and <c>CountSinceAsync</c> (WHERE &gt;= clause)
/// produces the correct aggregate counts end-to-end.
/// Issue #1655 (F3-FU-6 KbSubNav count badges).
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1655")]
public sealed class GetKbNavCountsQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _dbName = null!;
    private MeepleAiDbContext _dbContext = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public GetKbNavCountsQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_kbnav_counts_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);

        var mockMediator = TestDbContextFactory.CreateMockMediator();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547: UseVector() required by MeepleAiDbContext model
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_dbName);
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();
    }

    [Fact(Timeout = 30_000)]
    public async Task Handle_RealPostgres_CountsActiveJobsAndRecentFeedback()
    {
        // Arrange ----------------------------------------------------------------
        var now = DateTime.UtcNow;
        var clock = new FakeTimeProvider(new DateTimeOffset(now, TimeSpan.Zero));

        // Seed a user and a PDF document required by ProcessingJobEntity FK constraints.
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(CreateUser(userId));

        // One PDF document shared across all seeded jobs.
        var pdfId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(CreatePdfDocument(pdfId, userId));

        await _dbContext.SaveChangesAsync(Token);

        // Seed jobs:
        //   Active (counted):   Queued ×2 + Processing ×1 + Failed ×1 → 4
        //   Excluded:           Completed ×3 + Cancelled ×1             → 4
        _dbContext.ProcessingJobs.AddRange(
            MakeJob(pdfId, userId, JobStatus.Queued, now),
            MakeJob(pdfId, userId, JobStatus.Queued, now),
            MakeJob(pdfId, userId, JobStatus.Processing, now),
            MakeJob(pdfId, userId, JobStatus.Failed, now),
            MakeJob(pdfId, userId, JobStatus.Completed, now),
            MakeJob(pdfId, userId, JobStatus.Completed, now),
            MakeJob(pdfId, userId, JobStatus.Completed, now),
            MakeJob(pdfId, userId, JobStatus.Cancelled, now));

        // Seed feedback:
        //   Within 7 days (counted):   offsets 0,1,3,5,6 → 5
        //   Older (excluded):          offsets 10,30     → 2
        // Day-offset 7 omitted to avoid boundary ambiguity in `WHERE CreatedAt >= since`
        var withinWindow = new[] { 0, 1, 3, 5, 6 };
        var outsideWindow = new[] { 10, 30 };
        foreach (var dayOffset in withinWindow)
            _dbContext.KbUserFeedbacks.Add(MakeFeedback(now.AddDays(-dayOffset)));
        foreach (var dayOffset in outsideWindow)
            _dbContext.KbUserFeedbacks.Add(MakeFeedback(now.AddDays(-dayOffset)));

        await _dbContext.SaveChangesAsync(Token);

        // Build repositories from the same DbContext used to seed.
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();
        var jobsRepo = new ProcessingJobRepository(_dbContext, mockEventCollector.Object);
        var fbRepo = new KbUserFeedbackRepository(_dbContext);
        var sut = new GetKbNavCountsQueryHandler(jobsRepo, fbRepo, clock);

        // Act --------------------------------------------------------------------
        var result = await sut.Handle(new GetKbNavCountsQuery(), Token);

        // Assert -----------------------------------------------------------------
        result.ProcessingQueue.Should().Be(4,
            "Queued ×2 + Processing ×1 + Failed ×1 are the three active statuses");
        result.Feedback7d.Should().Be(5,
            "five feedback records fall within the 7-day window");
        result.AsOf.Should().Be(clock.GetUtcNow(),
            "AsOf must be the clock's current UTC time");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static UserEntity CreateUser(Guid userId) => new()
    {
        Id = userId,
        Email = $"kbnav-test-{userId:N}@test.local",
        DisplayName = "KbNavCounts Test User",
        PasswordHash = "hashed",
        Role = "User",
        Tier = "free",
        CreatedAt = DateTime.UtcNow,
        IsTwoFactorEnabled = false,
    };

    private static PdfDocumentEntity CreatePdfDocument(Guid pdfId, Guid userId) => new()
    {
        Id = pdfId,
        FileName = "test-rulebook.pdf",
        FilePath = "/test/test-rulebook.pdf",
        FileSizeBytes = 1024,
        UploadedByUserId = userId,
        UploadedAt = DateTime.UtcNow,
        ProcessingState = "Ready",
    };

    private static ProcessingJobEntity MakeJob(
        Guid pdfDocumentId, Guid userId, JobStatus status, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        PdfDocumentId = pdfDocumentId,
        UserId = userId,
        Status = status.ToString(),
        Priority = 0,
        CreatedAt = new DateTimeOffset(createdAt, TimeSpan.Zero),
    };

    private static KbUserFeedback MakeFeedback(DateTime createdAt)
    {
        var feedback = KbUserFeedback.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            chatSessionId: Guid.NewGuid(),
            messageId: Guid.NewGuid(),
            outcome: "helpful",
            comment: null);

        // Override the private CreatedAt set by KbUserFeedback.Create() to a
        // deterministic test timestamp, enabling precise window-boundary assertions.
        typeof(KbUserFeedback)
            .GetProperty(nameof(KbUserFeedback.CreatedAt))!
            .SetValue(feedback, createdAt);

        return feedback;
    }
}

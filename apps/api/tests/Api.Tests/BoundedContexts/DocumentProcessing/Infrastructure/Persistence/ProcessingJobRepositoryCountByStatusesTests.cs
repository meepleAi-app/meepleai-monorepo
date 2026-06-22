using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1655")]
public sealed class ProcessingJobRepositoryCountByStatusesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProcessingJobRepository CreateRepo(out Api.Infrastructure.MeepleAiDbContext db)
    {
        // Read-only tests: use the factory's shared Moq collector so the same instance is wired
        // into both MeepleAiDbContext and the repository — see PhotoBatchUploadRepositoryIntegrationTests
        // for the write-path (Testcontainers) pattern.
        var collector = TestDbContextFactory.CreateMockEventCollector();
        db = TestDbContextFactory.CreateInMemoryDbContextWithCollector(collector);
        return new ProcessingJobRepository(db, collector.Object);
    }

    private static ProcessingJobEntity NewJob(JobStatus status) => new()
    {
        Id = Guid.NewGuid(),
        PdfDocumentId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Status = status.ToString(),
        Priority = 0,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task CountByStatusesAsync_NoMatches_ReturnsZero()
    {
        var repo = CreateRepo(out var db);
        db.ProcessingJobs.AddRange(NewJob(JobStatus.Completed), NewJob(JobStatus.Cancelled));
        await db.SaveChangesAsync(Ct);

        var count = await repo.CountByStatusesAsync(
            new[] { JobStatus.Queued, JobStatus.Processing, JobStatus.Failed }, Ct);

        count.Should().Be(0);
    }

    [Fact]
    public async Task CountByStatusesAsync_MultipleStatuses_CountsAcrossAll()
    {
        var repo = CreateRepo(out var db);
        db.ProcessingJobs.AddRange(
            NewJob(JobStatus.Queued),
            NewJob(JobStatus.Queued),
            NewJob(JobStatus.Processing),
            NewJob(JobStatus.Failed),
            NewJob(JobStatus.Completed),     // not counted
            NewJob(JobStatus.Cancelled));    // not counted
        await db.SaveChangesAsync(Ct);

        var count = await repo.CountByStatusesAsync(
            new[] { JobStatus.Queued, JobStatus.Processing, JobStatus.Failed }, Ct);

        count.Should().Be(4);
    }

    [Fact]
    public async Task CountByStatusesAsync_EmptyStatusList_ReturnsZero()
    {
        var repo = CreateRepo(out var db);
        db.ProcessingJobs.Add(NewJob(JobStatus.Queued));
        await db.SaveChangesAsync(Ct);

        var count = await repo.CountByStatusesAsync(Array.Empty<JobStatus>(), Ct);

        count.Should().Be(0);
    }

    [Fact]
    public async Task CountByStatusesAsync_SingleStatus_CountsOnlyThatStatus()
    {
        var repo = CreateRepo(out var db);
        db.ProcessingJobs.AddRange(
            NewJob(JobStatus.Failed),
            NewJob(JobStatus.Failed),
            NewJob(JobStatus.Queued));
        await db.SaveChangesAsync(Ct);

        var count = await repo.CountByStatusesAsync(new[] { JobStatus.Failed }, Ct);

        count.Should().Be(2);
    }

    [Fact]
    public async Task CountByStatusesAsync_NullStatuses_ThrowsArgumentNullException()
    {
        var repo = CreateRepo(out _);

        Func<Task> act = () => repo.CountByStatusesAsync(null!, Ct);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("statuses");
    }
}

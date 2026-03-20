using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// Integration tests for BatchJobRepository (Issue #3693)
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class BatchJobRepositoryTests : IClassFixture<SharedTestcontainersFixture>, IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Mock<IDomainEventCollector> _eventCollectorMock = new();
    private readonly string _databaseName;
    private string? _connectionString;
    private static readonly Guid TestUserId = Guid.NewGuid();

    public BatchJobRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _databaseName = $"test_batch_job_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Apply migrations to create schema
        using var dbContext = _fixture.CreateDbContext(_connectionString);
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidJob_ShouldPersistToDatabase()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);
        var job = BatchJob.Create(JobType.ResourceForecast, "{\"days\":30}", TestUserId);

        // Act
        await repository.AddAsync(job);
        await dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await repository.GetByIdAsync(job.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Type.Should().Be(JobType.ResourceForecast);
        retrieved.Status.Should().Be(JobStatus.Queued);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingJob_ShouldReturnJob()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        await repository.AddAsync(job);
        await dbContext.SaveChangesAsync();

        // Act
        var retrieved = await repository.GetByIdAsync(job.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(job.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentJob_ShouldReturnNull()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);
        var nonExistentId = Guid.NewGuid();

        // Act
        var retrieved = await repository.GetByIdAsync(nonExistentId);

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WithMultipleJobs_ShouldReturnAll()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);
        await repository.AddAsync(BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId));
        await repository.AddAsync(BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId));
        await repository.AddAsync(BatchJob.Create(JobType.DataCleanup, "{}", TestUserId));
        await dbContext.SaveChangesAsync();

        // Act
        var jobs = await repository.GetAllAsync();

        // Assert
        jobs.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPagedAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);

        // Create 5 jobs
        for (int i = 0; i < 5; i++)
        {
            await repository.AddAsync(BatchJob.Create(JobType.BggSync, "{}", TestUserId));
        }
        await dbContext.SaveChangesAsync();

        // Act
        var firstPage = await repository.GetPagedAsync(0, 2);
        var secondPage = await repository.GetPagedAsync(2, 2);

        // Assert
        firstPage.Should().HaveCount(2);
        secondPage.Should().HaveCount(2);
        firstPage.Select(j => j.Id).Should().NotIntersectWith(secondPage.Select(j => j.Id));
    }

    #endregion

    #region GetByStatusAsync Tests

    [Fact]
    public async Task GetByStatusAsync_WithQueuedStatus_ShouldReturnOnlyQueuedJobs()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);

        var queuedJob = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        var runningJob = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        runningJob.Start();
        var completedJob = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        completedJob.Start();
        completedJob.Complete(null, "Done", null);

        await repository.AddAsync(queuedJob);
        await repository.AddAsync(runningJob);
        await repository.AddAsync(completedJob);
        await dbContext.SaveChangesAsync();

        // Act
        var queuedJobs = await repository.GetByStatusAsync(JobStatus.Queued);

        // Assert
        queuedJobs.Should().HaveCount(1);
        queuedJobs.Should().Contain(j => j.Id == queuedJob.Id);
    }

    [Fact]
    public async Task GetByStatusAsync_WithRunningStatus_ShouldReturnOnlyRunningJobs()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);

        var job1 = BatchJob.Create(JobType.BggSync, "{}", TestUserId);
        var job2 = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        job2.Start();

        await repository.AddAsync(job1);
        await repository.AddAsync(job2);
        await dbContext.SaveChangesAsync();

        // Act
        var runningJobs = await repository.GetByStatusAsync(JobStatus.Running);

        // Assert
        runningJobs.Should().HaveCount(1);
        runningJobs.Should().Contain(j => j.Id == job2.Id);
    }

    [Fact]
    public async Task GetByStatusAsync_WithFailedStatus_ShouldReturnOnlyFailedJobs()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);

        var failedJob = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        failedJob.Start();
        failedJob.Fail("Test error");

        await repository.AddAsync(failedJob);
        await dbContext.SaveChangesAsync();

        // Act
        var failedJobs = await repository.GetByStatusAsync(JobStatus.Failed);

        // Assert
        failedJobs.Should().HaveCount(1);
        failedJobs.First().ErrorMessage.Should().Be("Test error");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithProgressUpdate_ShouldPersistChanges()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        await repository.AddAsync(job);
        await dbContext.SaveChangesAsync();

        // Act
        job.Start();
        job.UpdateProgress(50);
        await repository.UpdateAsync(job);
        await dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await repository.GetByIdAsync(job.Id);
        retrieved!.Status.Should().Be(JobStatus.Running);
        retrieved.Progress.Should().Be(50);
    }

    [Fact]
    public async Task UpdateAsync_WithStatusChange_ShouldPersistChanges()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);
        var job = BatchJob.Create(JobType.BggSync, "{}", TestUserId);
        await repository.AddAsync(job);
        await dbContext.SaveChangesAsync();

        // Act
        job.Start();
        await repository.UpdateAsync(job);
        await dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await repository.GetByIdAsync(job.Id);
        retrieved!.Status.Should().Be(JobStatus.Running);
        retrieved.StartedAt.Should().NotBeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithExistingJob_ShouldRemoveFromDatabase()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        await repository.AddAsync(job);
        await dbContext.SaveChangesAsync();

        // Act
        await repository.DeleteAsync(job.Id);
        await dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await repository.GetByIdAsync(job.Id);
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetOldestQueuedJobAsync Tests

    [Fact]
    public async Task GetOldestQueuedJobAsync_WithMultipleQueuedJobs_ShouldReturnOldest()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);

        var job1 = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        await repository.AddAsync(job1);
        await dbContext.SaveChangesAsync();

        await Task.Delay(100); // Ensure different timestamps

        var job2 = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        await repository.AddAsync(job2);
        await dbContext.SaveChangesAsync();

        // Act
        var oldest = await repository.GetByStatusAsync(JobStatus.Queued);

        // Assert
        var orderedJobs = oldest.OrderBy(j => j.CreatedAt).ToList();
        orderedJobs.First().Id.Should().Be(job1.Id);
    }

    #endregion

    #region Count Tests

    [Fact]
    public async Task GetAllAsync_Count_ShouldReturnCorrectTotal()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);

        await repository.AddAsync(BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId));
        await repository.AddAsync(BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId));
        await dbContext.SaveChangesAsync();

        // Act
        var count = (await repository.GetAllAsync()).Count;

        // Assert
        count.Should().Be(2);
    }

    #endregion

    #region JobType Filtering Tests

    [Fact]
    public async Task GetAllAsync_WithDifferentJobTypes_ShouldReturnAll()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new BatchJobRepository(dbContext, _eventCollectorMock.Object);

        await repository.AddAsync(BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId));
        await repository.AddAsync(BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId));
        await repository.AddAsync(BatchJob.Create(JobType.DataCleanup, "{}", TestUserId));
        await repository.AddAsync(BatchJob.Create(JobType.BggSync, "{}", TestUserId));
        await dbContext.SaveChangesAsync();

        // Act
        var jobs = await repository.GetAllAsync();

        // Assert
        jobs.Should().HaveCount(4);
        jobs.Select(j => j.Type).Should().Contain(new[] {
            JobType.ResourceForecast,
            JobType.CostAnalysis,
            JobType.DataCleanup,
            JobType.BggSync
        });
    }

    #endregion
}
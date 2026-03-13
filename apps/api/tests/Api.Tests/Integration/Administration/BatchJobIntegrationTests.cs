using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for Batch Job System with real database (Issue #3693)
/// Tests job execution, cancellation, retry workflows with PostgreSQL
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3693")]
public sealed class BatchJobIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IBatchJobRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public BatchJobIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_batchjob_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IBatchJobRepository, BatchJobRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // MediatR (required by MeepleAiDbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IBatchJobRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database schema
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    #region Job Execution Workflow Tests

    [Fact]
    public async Task JobExecutionWorkflow_FromCreationToCompletion_ShouldSucceed()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{\"days\":30}", TestUserId);
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Start job
        job.Start();
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Update progress
        job.UpdateProgress(50);
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Complete job
        job.Complete("{\"result\":\"success\"}", "Forecast completed", null);
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(job.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(JobStatus.Completed);
        retrieved.Progress.Should().Be(100);
        retrieved.ResultSummary.Should().Be("Forecast completed");
    }

    [Fact]
    public async Task JobExecutionWorkflow_WithFailure_ShouldMarkAsFailed()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Start and fail job
        job.Start();
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        job.Fail("Database connection timeout");
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(job.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(JobStatus.Failed);
        retrieved.ErrorMessage.Should().Be("Database connection timeout");
        retrieved.RetryCount.Should().Be(1);
    }

    #endregion

    #region Job Cancellation Tests

    [Fact]
    public async Task CancelJob_WhenQueued_ShouldMarkAsCancelled()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        job.Cancel();
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(job.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public async Task CancelJob_WhenRunning_ShouldMarkAsCancelled()
    {
        // Arrange
        var job = BatchJob.Create(JobType.BggSync, "{\"limit\":1000}", TestUserId);
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        job.Start();
        job.UpdateProgress(25);
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act
        job.Cancel();
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(job.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(JobStatus.Cancelled);
        retrieved.Progress.Should().Be(25); // Progress preserved
    }

    #endregion

    #region Job Retry Tests

    [Fact]
    public async Task RetryJob_AfterFailure_ShouldRequeueJob()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        job.Start();
        job.Fail("Network error");
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        var originalRetryCount = job.RetryCount;

        // Act
        job.Retry();
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(job.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(JobStatus.Queued);
        retrieved.Progress.Should().Be(0);
        retrieved.ErrorMessage.Should().BeNull();
        retrieved.RetryCount.Should().Be(originalRetryCount); // Preserved from failure
    }

    [Fact]
    public async Task RetryJob_MultipleTimes_ShouldIncrementRetryCount()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - First attempt fails
        job.Start();
        job.Fail("Error 1");
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Retry and fail again
        job.Retry();
        job.Start();
        job.Fail("Error 2");
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(job.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.RetryCount.Should().Be(2);
    }

    #endregion

    #region Processor Integration Tests

    [Fact]
    public async Task ProcessorJob_WithQueuedJob_ShouldExecuteAndComplete()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var logger = _serviceProvider!.GetRequiredService<ILogger<BatchJobProcessorJob>>();
        var processor = new BatchJobProcessorJob(_repository, _serviceProvider, logger);

        var contextMock = new Mock<IJobExecutionContext>();
        contextMock.Setup(x => x.CancellationToken).Returns(TestCancellationToken);

        // Act
        await processor.Execute(contextMock.Object);

        // Assert
        var retrieved = await _repository.GetByIdAsync(job.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(JobStatus.Completed);
        retrieved.Progress.Should().Be(100);
        retrieved.StartedAt.Should().NotBeNull();
        retrieved.CompletedAt.Should().NotBeNull();
        retrieved.DurationSeconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ProcessorJob_WithMultipleQueuedJobs_ShouldProcessOldestFirst()
    {
        // Arrange
        var job1 = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        await _repository!.AddAsync(job1, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        await Task.Delay(100); // Ensure different timestamps

        var job2 = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        await _repository.AddAsync(job2, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        var logger = _serviceProvider!.GetRequiredService<ILogger<BatchJobProcessorJob>>();
        var processor = new BatchJobProcessorJob(_repository, _serviceProvider, logger);

        var contextMock = new Mock<IJobExecutionContext>();
        contextMock.Setup(x => x.CancellationToken).Returns(TestCancellationToken);

        // Act
        await processor.Execute(contextMock.Object);

        // Assert
        var retrieved1 = await _repository.GetByIdAsync(job1.Id, TestCancellationToken);
        var retrieved2 = await _repository.GetByIdAsync(job2.Id, TestCancellationToken);

        retrieved1!.Status.Should().Be(JobStatus.Completed); // Oldest processed
        retrieved2!.Status.Should().Be(JobStatus.Queued);     // Still queued
    }

    #endregion

    #region Status Filtering Tests

    [Fact]
    public async Task GetByStatusAsync_WithMixedStatuses_ShouldReturnCorrectJobs()
    {
        // Arrange
        var queuedJob = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        var runningJob = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        runningJob.Start();
        var completedJob = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        completedJob.Start();
        completedJob.Complete(null, "Done", null);

        await _repository!.AddAsync(queuedJob, TestCancellationToken);
        await _repository.AddAsync(runningJob, TestCancellationToken);
        await _repository.AddAsync(completedJob, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var queued = await _repository.GetByStatusAsync(JobStatus.Queued, TestCancellationToken);
        var running = await _repository.GetByStatusAsync(JobStatus.Running, TestCancellationToken);
        var completed = await _repository.GetByStatusAsync(JobStatus.Completed, TestCancellationToken);

        // Assert
        queued.Should().HaveCount(1);
        queued.Should().Contain(j => j.Id == queuedJob.Id);

        running.Should().HaveCount(1);
        running.Should().Contain(j => j.Id == runningJob.Id);

        completed.Should().HaveCount(1);
        completed.Should().Contain(j => j.Id == completedJob.Id);
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task GetPagedAsync_WithPagination_ShouldReturnCorrectPages()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            var job = BatchJob.Create(JobType.BggSync, "{}", TestUserId);
            await _repository!.AddAsync(job, TestCancellationToken);
        }
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var page1 = await _repository!.GetPagedAsync(0, 2, null, TestCancellationToken);
        var page2 = await _repository.GetPagedAsync(2, 2, null, TestCancellationToken);
        var page3 = await _repository.GetPagedAsync(4, 2, null, TestCancellationToken);

        // Assert
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page3.Should().HaveCount(1);

        var allJobIds = page1.Select(j => j.Id)
            .Concat(page2.Select(j => j.Id))
            .Concat(page3.Select(j => j.Id))
            .ToList();

        allJobIds.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_WithExistingJob_ShouldRemoveFromDatabase()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        await _repository.DeleteAsync(job.Id, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(job.Id, TestCancellationToken);
        retrieved.Should().BeNull();
    }

    #endregion

    #region Job Type Tests

    [Theory]
    [InlineData(JobType.ResourceForecast)]
    [InlineData(JobType.CostAnalysis)]
    [InlineData(JobType.DataCleanup)]
    [InlineData(JobType.BggSync)]
    [InlineData(JobType.AgentBenchmark)]
    public async Task CreateAndRetrieve_AllJobTypes_ShouldSucceed(JobType jobType)
    {
        // Arrange
        var job = BatchJob.Create(jobType, "{}", TestUserId);
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await _repository.GetByIdAsync(job.Id, TestCancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Type.Should().Be(jobType);
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ConcurrentProcessing_WithMultipleProcessors_ShouldProcessDifferentJobs()
    {
        // Arrange
        var job1 = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        var job2 = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        await _repository!.AddAsync(job1, TestCancellationToken);
        await _repository.AddAsync(job2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var logger = _serviceProvider!.GetRequiredService<ILogger<BatchJobProcessorJob>>();
        var processor1 = new BatchJobProcessorJob(_repository, _serviceProvider, logger);
        var processor2 = new BatchJobProcessorJob(_repository, _serviceProvider, logger);

        var contextMock = new Mock<IJobExecutionContext>();
        contextMock.Setup(x => x.CancellationToken).Returns(TestCancellationToken);

        // Act - Process in parallel (though processor is DisallowConcurrentExecution)
        var task1 = processor1.Execute(contextMock.Object);
        await Task.Delay(50); // Small delay to ensure job1 starts first
        var task2 = processor2.Execute(contextMock.Object);

        await Task.WhenAll(task1, task2);

        // Assert
        var retrieved1 = await _repository.GetByIdAsync(job1.Id, TestCancellationToken);
        var retrieved2 = await _repository.GetByIdAsync(job2.Id, TestCancellationToken);

        // At least one should be completed
        var completedCount = new[] { retrieved1!.Status, retrieved2!.Status }
            .Count(s => s == JobStatus.Completed);

        completedCount.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Progress Tracking Tests

    [Fact]
    public async Task ProgressTracking_ShouldPersistAllUpdates()
    {
        // Arrange
        var job = BatchJob.Create(JobType.BggSync, "{}", TestUserId);
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        job.Start();
        await _repository.UpdateAsync(job, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        for (int progress = 10; progress <= 100; progress += 10)
        {
            job.UpdateProgress(progress);
            await _repository.UpdateAsync(job, TestCancellationToken);
            await _unitOfWork.SaveChangesAsync(TestCancellationToken);
        }

        // Assert
        var retrieved = await _repository.GetByIdAsync(job.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Progress.Should().Be(100);
    }

    #endregion
}

using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for BatchJobProcessor lifecycle management.
/// Tests job creation, execution, completion, retry, and cancellation flows.
/// Issue #3697: Epic 1 - Testing & Integration (Phase 2)
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3697")]
public sealed class BatchJobProcessorIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IBatchJobRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestCreatedBy = new("99000000-0000-0000-0000-000000000001");

    public BatchJobProcessorIntegrationTests(SharedTestcontainersFixture fixture)
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
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
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
            await ((ServiceProvider)_serviceProvider).DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task JobLifecycle_CreateStartComplete_ShouldPersistAllStates()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{\"test\": true}", TestCreatedBy);
        var jobId = job.Id; // Capture generated ID

        // Act - Create
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Verify Created state (Queued)
        var createdJob = await _repository.GetByIdAsync(jobId, TestCancellationToken);
        createdJob.Should().NotBeNull();
        createdJob!.Status.Should().Be(JobStatus.Queued);
        createdJob.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Act - Start
        createdJob.Start();
        await _repository.UpdateAsync(createdJob, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Verify Running state
        var runningJob = await _repository.GetByIdAsync(jobId, TestCancellationToken);
        runningJob.Should().NotBeNull();
        runningJob!.Status.Should().Be(JobStatus.Running);
        runningJob.StartedAt.Should().NotBeNull();
        runningJob.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Act - Complete
        runningJob.Complete(resultData: null, resultSummary: "Job finished successfully", outputFileUrl: null);
        await _repository.UpdateAsync(runningJob, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Verify Completed state
        var completedJob = await _repository.GetByIdAsync(jobId, TestCancellationToken);
        completedJob.Should().NotBeNull();
        completedJob!.Status.Should().Be(JobStatus.Completed);
        completedJob.CompletedAt.Should().NotBeNull();
        completedJob.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        completedJob.ResultSummary.Should().Be("Job finished successfully");
    }

    [Fact]
    public async Task JobRetry_WhenJobFails_ShouldIncrementRetryCountAndResetToQueued()
    {
        // Arrange
        var job = BatchJob.Create(JobType.AgentBenchmark, "{\"benchmark\": \"test\"}", TestCreatedBy);
        var jobId = job.Id;
        job.Start();

        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Fail job (triggers retry)
        var runningJob = await _repository.GetByIdAsync(jobId, TestCancellationToken);
        runningJob.Should().NotBeNull();
        runningJob!.Fail("Simulated failure");
        await _repository.UpdateAsync(runningJob, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Verify Failed state
        var failedJob = await _repository.GetByIdAsync(jobId, TestCancellationToken);
        failedJob.Should().NotBeNull();
        failedJob!.Status.Should().Be(JobStatus.Failed);
        failedJob.RetryCount.Should().Be(0);
        failedJob.ErrorMessage.Should().Be("Simulated failure");

        // Act - Retry job
        failedJob.Retry();
        await _repository.UpdateAsync(failedJob, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Verify Queued state after retry
        var retriedJob = await _repository.GetByIdAsync(jobId, TestCancellationToken);
        retriedJob.Should().NotBeNull();
        retriedJob!.Status.Should().Be(JobStatus.Queued);
        retriedJob.RetryCount.Should().Be(1);
        retriedJob.ErrorMessage.Should().BeNull(); // Error cleared on retry
    }

    [Fact]
    public async Task JobCancellation_WhenRequested_ShouldSetStatusToCancelled()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{\"period\": \"monthly\"}", TestCreatedBy);
        var jobId = job.Id;
        await _repository!.AddAsync(job, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Cancel job
        var pendingJob = await _repository.GetByIdAsync(jobId, TestCancellationToken);
        pendingJob.Should().NotBeNull();
        pendingJob!.Cancel();
        await _repository.UpdateAsync(pendingJob, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Verify Cancelled state
        var cancelledJob = await _repository.GetByIdAsync(jobId, TestCancellationToken);
        cancelledJob.Should().NotBeNull();
        cancelledJob!.Status.Should().Be(JobStatus.Cancelled);
        cancelledJob.CompletedAt.Should().NotBeNull();
        cancelledJob.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetPendingJobs_WhenMultipleJobsExist_ShouldReturnOnlyPendingJobs()
    {
        // Arrange - Create jobs in different states
        var pendingJob1 = BatchJob.Create(JobType.BggSync, "{}", TestCreatedBy);
        var pendingJob2 = BatchJob.Create(JobType.ResourceForecast, "{}", TestCreatedBy);
        var runningJob = BatchJob.Create(JobType.DataCleanup, "{}", TestCreatedBy);
        runningJob.Start();
        var completedJob = BatchJob.Create(JobType.CostAnalysis, "{}", TestCreatedBy);
        completedJob.Start();
        completedJob.Complete(resultData: null, resultSummary: "Done", outputFileUrl: null);

        await _repository!.AddAsync(pendingJob1, TestCancellationToken);
        await _repository.AddAsync(pendingJob2, TestCancellationToken);
        await _repository.AddAsync(runningJob, TestCancellationToken);
        await _repository.AddAsync(completedJob, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var pendingJobs = await _repository.GetByStatusAsync(JobStatus.Queued, TestCancellationToken);

        // Assert
        pendingJobs.Should().NotBeNull();
        pendingJobs.Should().HaveCount(2);
        pendingJobs.Should().OnlyContain(j => j.Status == JobStatus.Queued);
        pendingJobs.Should().Contain(j => j.Type == JobType.BggSync);
        pendingJobs.Should().Contain(j => j.Type == JobType.ResourceForecast);
    }

    [Fact]
    public async Task GetRunningJobs_WhenMultipleJobsExist_ShouldReturnOnlyRunningJobs()
    {
        // Arrange
        var pendingJob = BatchJob.Create(JobType.BggSync, "{}", TestCreatedBy);
        var runningJob1 = BatchJob.Create(JobType.ResourceForecast, "{}", TestCreatedBy);
        runningJob1.Start();
        var runningJob2 = BatchJob.Create(JobType.AgentBenchmark, "{}", TestCreatedBy);
        runningJob2.Start();

        await _repository!.AddAsync(pendingJob, TestCancellationToken);
        await _repository.AddAsync(runningJob1, TestCancellationToken);
        await _repository.AddAsync(runningJob2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var runningJobs = await _repository.GetByStatusAsync(JobStatus.Running, TestCancellationToken);

        // Assert
        runningJobs.Should().NotBeNull();
        runningJobs.Should().HaveCount(2);
        runningJobs.Should().OnlyContain(j => j.Status == JobStatus.Running);
        runningJobs.Should().Contain(j => j.Type == JobType.ResourceForecast);
        runningJobs.Should().Contain(j => j.Type == JobType.AgentBenchmark);
    }
}

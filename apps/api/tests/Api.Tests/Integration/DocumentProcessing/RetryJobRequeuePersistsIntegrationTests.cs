using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Regression guard for the bug-hunt B12 root cause (issue #3269 recovery-hardening): re-queuing a
/// Failed <see cref="Api.BoundedContexts.DocumentProcessing.Domain.Entities.ProcessingJob"/> via
/// <c>job.Retry()</c> + <see cref="ProcessingJobRepository.UpdateAsync"/> +
/// <c>UnitOfWork.SaveChangesAsync</c> threw <see cref="DbUpdateConcurrencyException"/> for any job
/// that has steps (every real job — <c>ProcessingJob.Create()</c> always seeds 5).
///
/// <para><b>Root cause:</b> <c>Retry()</c> replaces the owned step collection with fresh-Id
/// <c>ProcessingStep</c> instances. <c>ProcessingJobRepository.UpdateAsync</c> then called a blind
/// <c>DbSet.Update(entity)</c>, which marks every incoming step Modified — so the fresh-Id steps
/// became <c>UPDATE processing_steps WHERE Id = @newId</c>, matching 0 rows and throwing. The
/// pure-mock unit suites (<c>RetryJobCommandHandler</c> / <c>BulkReindexFailedCommandHandler</c>)
/// stub <c>UpdateAsync</c> and never exercise EF's disconnected-graph semantics, so nothing caught
/// this. Both the user-facing single-job retry (this test) and the bulk reindex-failed path share
/// the broken code, so the fix repairs both.</para>
///
/// <para>Runs under the production <c>QueryTrackingBehavior.NoTracking</c> default to match how the
/// repository queries execute in production.</para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3269")]
public sealed class RetryJobRequeuePersistsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-0000000B12A0");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-0000000B12A0");

    private static readonly string[] StepNames =
        { "Upload", "Extract", "Chunk", "Embed", "Index" };

    public RetryJobRequeuePersistsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_retryjob_requeue_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(
            _isolatedDbConnectionString, useNoTrackingDefault: true);

        services.AddSingleton<IHttpContextAccessor>(new Mock<IHttpContextAccessor>().Object);
        services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

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
            Email = "retryjob-requeue-test@meepleai.test",
            PasswordHash = "x",
            DisplayName = "RetryJob Requeue Test",
        });
        _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = TestSharedGameId,
            Title = "RetryJob Requeue Test Game",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    /// <summary>
    /// Seeds a Failed PDF + a Failed ProcessingJob with a full set of 5 persisted step rows (as a
    /// real job created via <c>ProcessingJob.Create()</c> would have). Returns (jobId, oldStepIds).
    /// Uses a fresh isolated scope so the seeded rows are not left tracked in the handler's context.
    /// </summary>
    private async Task<(Guid JobId, Guid[] OldStepIds)> SeedFailedJobWithStepsAsync()
    {
        var pdfId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var oldStepIds = Enumerable.Range(0, StepNames.Length).Select(_ => Guid.NewGuid()).ToArray();

        using var seedScope = _serviceProvider!.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        seedDb.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = TestSharedGameId,
            UploadedByUserId = TestUserId,
            FileName = $"retryjob-{pdfId:N}.pdf",
            FilePath = $"/tmp/{pdfId:N}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            ProcessingState = "Failed",
            UploadedAt = DateTime.UtcNow,
        });

        var steps = oldStepIds.Select((id, i) => new ProcessingStepEntity
        {
            Id = id,
            StepName = StepNames[i],
            Status = i == 0 ? "Failed" : "Pending",
            StartedAt = i == 0 ? DateTimeOffset.UtcNow.AddMinutes(-6) : null,
            CompletedAt = i == 0 ? DateTimeOffset.UtcNow.AddMinutes(-5) : null,
        }).ToList();

        seedDb.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = jobId,
            PdfDocumentId = pdfId,
            UserId = TestUserId,
            Status = "Failed",
            Priority = (int)ProcessingPriority.Normal,
            CurrentStep = null,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ErrorMessage = "boom — extractor timed out",
            RetryCount = 0,
            MaxRetries = 3,
            Steps = steps,
        });

        await seedDb.SaveChangesAsync(TestCancellationToken);
        return (jobId, oldStepIds);
    }

    [Fact]
    public async Task RetryJob_OnFailedJobWithSteps_RequeuesWithoutConcurrencyException()
    {
        var (jobId, oldStepIds) = await SeedFailedJobWithStepsAsync();

        // Act: the whole point — this MUST NOT throw. Pre-fix it threw
        // DbUpdateConcurrencyException ("expected to affect 1 row(s), but actually affected 0")
        // when SaveChangesAsync tried to UPDATE the fresh-Id steps.
        await _mediator!.Send(new RetryJobCommand(jobId), TestCancellationToken);

        using var verifyScope = _serviceProvider!.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var job = await verifyDb.Set<ProcessingJobEntity>()
            .AsNoTracking()
            .FirstAsync(j => j.Id == jobId, TestCancellationToken);
        job.Status.Should().Be(nameof(JobStatus.Queued), "Retry() moves a Failed job back to Queued");
        job.RetryCount.Should().Be(1, "Retry() increments the retry counter");
        job.CurrentStep.Should().BeNull("Retry() clears the current step");

        // The step collection was fully replaced: exactly 5 fresh Pending steps, and none of the
        // old step rows survive (the reconcile must DELETE them, not orphan them).
        var steps = await verifyDb.Set<ProcessingStepEntity>()
            .AsNoTracking()
            .Where(s => s.ProcessingJobId == jobId)
            .ToListAsync(TestCancellationToken);
        steps.Should().HaveCount(5, "Retry() replaces the 5 steps — old ones deleted, new ones inserted");
        steps.Should().OnlyContain(s => s.Status == "Pending", "fresh retry steps all start Pending");
        steps.Select(s => s.Id).Should().NotIntersectWith(
            oldStepIds, "the pre-retry step rows must be deleted, not left orphaned");
    }
}

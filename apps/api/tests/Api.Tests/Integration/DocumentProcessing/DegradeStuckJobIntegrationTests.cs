using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Regression guard for issue #2689: proves that <see cref="DegradeStuckJobCommandHandler"/>
/// persists Failed state on real Postgres WITHOUT throwing <see cref="ArgumentException"/>
/// during the xmin optimistic-concurrency UPDATE path.
///
/// Root cause of the reverted PR #2684: a <see cref="DateTime"/> with
/// <c>Kind=Unspecified</c> was written through the <c>UPDATE … WHERE xmin=Y RETURNING xmin</c>
/// SQL path, causing Npgsql to throw <see cref="ArgumentException"/>. The current handler
/// uses <see cref="DateTime.UtcNow"/> (<c>Kind=Utc</c>) inside
/// <c>PdfDocument.MarkAsFailed</c>, which is the correct kind for a <c>timestamptz</c> column.
///
/// This test inserts real rows, loads them via real repositories so Postgres assigns xmin,
/// and verifies that the full degrade–save round-trip completes without exception and that
/// both rows are persisted in <c>Failed</c> state.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "2689")]
public sealed class DegradeStuckJobIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Fixed GUIDs avoid collisions with other tests in the shared container.
    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000002689001");
    private static readonly Guid TestGameId = new("B0000000-0000-0000-0000-000002689001");

    public DegradeStuckJobIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_degrade_stuck_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Real repositories required directly by DegradeStuckJobCommandHandler.
        services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();

        // IUserRepository: required by ProcessingJobNotificationEventHandler (handles JobFailedEvent).
        services.AddScoped<IUserRepository, UserRepository>();

        // Mock: IProcessingMetricsService — required by PdfStateChangedMetricsEventHandler
        // which is picked up by MediatR's assembly scan and handles PdfStateChangedEvent
        // (raised via TransitionTo(Failed) inside MarkAsFailed).
        var mockMetrics = new Mock<Api.BoundedContexts.DocumentProcessing.Application.Services.IProcessingMetricsService>();
        services.AddScoped(_ => mockMetrics.Object);

        // Mock: IQueueStreamService — required by JobFailedStreamHandler which handles JobFailedEvent
        // raised by ProcessingJob.Fail(). Never called inline (OutboxOnly mode — events are stored
        // in domain_event_outbox, not dispatched immediately). Registered defensively so the DI
        // container can resolve JobFailedStreamHandler if the dispatch mode ever changes.
        // Moq loose mode returns Task.CompletedTask for all Task-returning methods by default.
        var mockQueueStream = new Mock<IQueueStreamService>();
        services.AddScoped<IQueueStreamService>(_ => mockQueueStream.Object);

        // Mock: UserNotifications dependencies — required by PdfNotificationEventHandler
        // (handles PdfStateChangedEvent / PdfFailedEvent) and ProcessingJobNotificationEventHandler
        // (handles JobFailedEvent). Picked up by MediatR's assembly scan.
        var mockNotifPrefsRepo = new Mock<Api.BoundedContexts.UserNotifications.Domain.Repositories.INotificationPreferencesRepository>();
        services.AddScoped(_ => mockNotifPrefsRepo.Object);
        var mockNotifRepo = new Mock<Api.BoundedContexts.UserNotifications.Domain.Repositories.INotificationRepository>();
        services.AddScoped(_ => mockNotifRepo.Object);
        var mockPushService = new Mock<Api.Services.IPushNotificationService>();
        services.AddScoped(_ => mockPushService.Object);
        var mockEmailQueueRepo = new Mock<Api.BoundedContexts.UserNotifications.Domain.Repositories.IEmailQueueRepository>();
        services.AddScoped(_ => mockEmailQueueRepo.Object);
        var mockEmailTemplateService = new Mock<Api.BoundedContexts.UserNotifications.Application.Services.IEmailTemplateService>();
        services.AddSingleton(_ => mockEmailTemplateService.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.MigrateAsync(TestCancellationToken);
        await SeedBaseDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable d)
            await d.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

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

    private async Task SeedBaseDataAsync()
    {
        _dbContext!.Users.Add(new UserEntity
        {
            Id = TestUserId,
            Email = "degrade-stuck-test@meepleai.test",
            DisplayName = "Degrade Stuck Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
        });
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = TestGameId,
            Title = "Degrade Stuck Test Game",
            BggId = Guid.NewGuid().GetHashCode() & 0x7FFFFFFF, // Positive int
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    /// <summary>
    /// Proves end-to-end that degrading a stuck job + its PDF to Failed state:
    /// (a) does NOT throw <see cref="ArgumentException"/> on the
    ///     <c>UPDATE … WHERE xmin=Y RETURNING xmin</c> path (the regression from #2684), and
    /// (b) persists <c>ProcessingState=Failed</c>, <c>ErrorCategory=Service</c>,
    ///     and <c>ProcessedAt ≠ null</c> on the PDF row, and
    ///     <c>Status=Failed</c> + <c>CompletedAt ≠ null</c> on the job row.
    /// </summary>
    [Fact]
    public async Task Degrade_StuckJobAndPdf_PersistsFailedState_NoDateTimeKindError()
    {
        // ── Arrange ──────────────────────────────────────────────────────────────
        // Insert a PdfDocument (Embedding) and a ProcessingJob (Processing) into
        // real Postgres BEFORE running the handler. The handler's repositories will
        // then LOAD these rows so Postgres assigns xmin to both entities. The
        // subsequent SaveChangesAsync issues UPDATE … WHERE xmin=Y RETURNING xmin
        // for PdfDocumentEntity (configured with .IsRowVersion()). This is the exact
        // path that threw ArgumentException when Kind=Unspecified in #2684.

        var pdfId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        // PdfDocument in Embedding state — not Ready / not Failed → handler will call MarkAsFailed.
        _dbContext!.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "stuck-test.pdf",
            FilePath = $"/test/stuck-{pdfId:N}.pdf",
            FileSizeBytes = 1_024,
            ContentType = "application/pdf",
            UploadedByUserId = TestUserId,
            SharedGameId = TestGameId,
            ProcessingState = nameof(PdfProcessingState.Embedding),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
        });

        // ProcessingJob in Processing state with an old StartedAt (simulates stuck job).
        // CurrentStep = null so job.Fail() skips the step-lookup branch (no steps seeded).
        _dbContext.ProcessingJobs.Add(new ProcessingJobEntity
        {
            Id = jobId,
            PdfDocumentId = pdfId,
            UserId = TestUserId,
            Status = "Processing",
            Priority = 0,
            CurrentStep = null,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-50), // stuck for 50 min
            MaxRetries = 3,
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // ── Act ───────────────────────────────────────────────────────────────────
        // Resolve a FRESH scope so the handler gets its own DbContext. The handler
        // loads both entities via real repositories (EF populates RowVersion/xmin),
        // mutates them via domain methods, and saves via the real UnitOfWork.
        // PdfDocument.MarkAsFailed internally sets ProcessedAt = DateTime.UtcNow
        // (Kind=Utc). This MUST NOT throw ArgumentException on the xmin UPDATE path.
        using var scope = _serviceProvider!.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // No exception must escape — that IS the regression proof.
        var result = await mediator.Send(
            new DegradeStuckJobCommand(jobId, 45.0),
            TestCancellationToken);

        // ── Assert ────────────────────────────────────────────────────────────────
        result.Degraded.Should().BeTrue(
            "handler must report successful degrade — no ArgumentException on xmin UPDATE path");

        // Reload PDF with AsNoTracking to bypass any cached tracker state.
        var reloadedPdf = await _dbContext.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdfId, TestCancellationToken);

        reloadedPdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Failed),
            "PdfDocument must be persisted in Failed state after degrade");
        reloadedPdf.ErrorCategory.Should().Be(nameof(ErrorCategory.Service),
            "ErrorCategory=Service keeps the PDF eligible for RetryFailedPdfsJob (RetryCount < 3)");
        reloadedPdf.ProcessedAt.Should().NotBeNull(
            "ProcessedAt written by MarkAsFailed (DateTime.UtcNow, Kind=Utc) must persist without ArgumentException");

        // Reload job with AsNoTracking.
        var reloadedJob = await _dbContext.ProcessingJobs.AsNoTracking()
            .FirstAsync(j => j.Id == jobId, TestCancellationToken);

        reloadedJob.Status.Should().Be("Failed",
            "ProcessingJob must be persisted in Failed status after degrade");
        reloadedJob.CompletedAt.Should().NotBeNull(
            "CompletedAt written by job.Fail() via TimeProvider (DateTimeOffset.UtcNow) must persist without ArgumentException");
    }
}

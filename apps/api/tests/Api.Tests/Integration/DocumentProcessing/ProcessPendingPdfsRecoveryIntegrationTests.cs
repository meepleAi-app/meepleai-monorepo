using Api.BoundedContexts.DocumentProcessing.Application.Commands.ProcessPendingPdfs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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
/// Integration test proving the B14 (#3269) recovery selector + reset run correctly against real
/// Postgres under the production <c>QueryTrackingBehavior.NoTracking</c> default. Uses a mock
/// <see cref="IPdfProcessingPipelineService"/> (recording which PDFs it is asked to process) rather
/// than the heavy real pipeline — the atomic Pending-only claim that makes concurrent sweeps
/// race-safe lives in RelationalPdfClaimService and is covered by its own suite.
///
/// <para>Verifies that <see cref="ProcessPendingPdfsCommandHandler"/> processes Pending docs and
/// STALE in-flight docs (resetting the latter to Pending first) but NOT recently-in-flight docs
/// (which the live pipeline is still working on — the old handler raced them) nor demo mock
/// placeholders.</para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3269")]
public sealed class ProcessPendingPdfsRecoveryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;
    private readonly Mock<IPdfProcessingPipelineService> _pipeline = new();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-0000000B1400");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-0000000B1400");

    public ProcessPendingPdfsRecoveryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_processpending_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(
            _isolatedDbConnectionString, useNoTrackingDefault: true);

        services.AddSingleton<IHttpContextAccessor>(new Mock<IHttpContextAccessor>().Object);

        _pipeline
            .Setup(p => p.ProcessAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // Registered last so it wins over any real pipeline registration from CreateBase.
        services.AddScoped(_ => _pipeline.Object);

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
            Email = "processpending-test@meepleai.test",
            PasswordHash = "x",
            DisplayName = "ProcessPending Recovery Test",
        });
        _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = TestSharedGameId,
            Title = "ProcessPending Recovery Test Game",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<Guid> SeedPdfAsync(string state, DateTime uploadedAt, bool demoMock = false)
    {
        var id = Guid.NewGuid();
        using var seedScope = _serviceProvider!.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        seedDb.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            SharedGameId = TestSharedGameId,
            UploadedByUserId = TestUserId,
            FileName = $"{id:N}.pdf",
            FilePath = demoMock
                ? $"{PdfDocumentEntity.DemoMockFilePathPrefix}badsworm/{id:N}/rulebook.pdf"
                : $"/tmp/{id:N}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            ProcessingState = state,
            UploadedAt = uploadedAt,
        });
        await seedDb.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    [Fact]
    public async Task ProcessPending_RecoversPendingAndStale_SkipsRecentInFlightAndDemoMocks()
    {
        var pendingId = await SeedPdfAsync(nameof(PdfProcessingState.Pending), DateTime.UtcNow);
        var recentInFlightId = await SeedPdfAsync(nameof(PdfProcessingState.Embedding), DateTime.UtcNow);
        var staleInFlightId = await SeedPdfAsync(nameof(PdfProcessingState.Indexing), DateTime.UtcNow.AddMinutes(-31));
        var demoMockId = await SeedPdfAsync(nameof(PdfProcessingState.Pending), DateTime.UtcNow, demoMock: true);

        var result = await _mediator!.Send(new ProcessPendingPdfsCommand(), TestCancellationToken);

        result.TotalPending.Should().Be(2, "only the Pending doc and the STALE in-flight doc are recoverable");

        // Recovered: Pending (as-is) + stale Indexing (after reset to Pending).
        _pipeline.Verify(p => p.ProcessAsync(pendingId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _pipeline.Verify(p => p.ProcessAsync(staleInFlightId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);

        // Skipped: recently-in-flight (live pipeline still working) + demo mock placeholder.
        _pipeline.Verify(p => p.ProcessAsync(recentInFlightId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _pipeline.Verify(p => p.ProcessAsync(demoMockId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        // The stale in-flight doc was reset to Pending in the DB (verified from a fresh scope under
        // NoTracking) so the pipeline's atomic claim can pick it up; the recent in-flight doc is
        // untouched.
        using var verifyScope = _serviceProvider!.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var stale = await verifyDb.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == staleInFlightId, TestCancellationToken);
        stale.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending), "stale in-flight doc must be reset to Pending");

        var recent = await verifyDb.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == recentInFlightId, TestCancellationToken);
        recent.ProcessingState.Should().Be(nameof(PdfProcessingState.Embedding), "a recently-active doc must NOT be reset — the live pipeline owns it");
    }
}

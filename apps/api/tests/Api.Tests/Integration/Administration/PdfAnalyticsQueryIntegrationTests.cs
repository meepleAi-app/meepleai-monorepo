using Api.BoundedContexts.Administration.Application.Queries.Analytics;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// <c>GetPdfAnalyticsQueryHandler</c> (<c>GET /api/v1/admin/analytics/pdf</c>) averages the processing time of the
/// ready documents. The average used to be <c>Select(...).DefaultIfEmpty(0).AverageAsync()</c> on a root query, the
/// shape EF Core cannot translate (the one #4059 fixed in <c>GetStatsAsync</c> and #4635 in
/// <c>GetStatsByAgentAsync</c>). Nothing tested it against a database, and the route has no caller in the web.
/// </summary>
/// <remarks>
/// On Postgres, because the failure is in the translation of the query. The instants are relative to now, never fixed,
/// because a fixed date ages.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Dependency", "PostgreSQL")]
public sealed class PdfAnalyticsQueryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private Guid _uploaderId;

    public PdfAnalyticsQueryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_pdf_analytics_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _serviceProvider = IntegrationServiceCollectionBuilder.CreateBase(connectionString).BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync(Ct);

        _uploaderId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _uploaderId, Email = "pdf-analytics@meepleai.dev", DisplayName = "PDF analytics",
            Role = "User", Tier = "Free", CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Best-effort cleanup: isolation is already guaranteed by the dedicated database.
            }
        }
    }

    /// <summary>The empty table: the translation failure did not depend on there being rows.</summary>
    [Fact]
    public async Task WithNoDocuments_EverythingIsZero()
    {
        var analytics = await HandleAsync();

        analytics.TotalDocuments.Should().Be(0);
        analytics.TotalProcessed.Should().Be(0);
        analytics.SuccessRate.Should().Be(0);
        analytics.AvgProcessingTimeMs.Should().Be(0);
    }

    /// <summary>
    /// The average is over the documents that are <c>Ready</c> and have a <c>ProcessedAt</c>: a failed one, a
    /// ready one with no instant and a still-pending one do not enter it.
    /// </summary>
    [Fact]
    public async Task TheAverageProcessingTime_IsOverTheReadyDocumentsWithAnInstant_Only()
    {
        var uploaded = DateTime.UtcNow.AddHours(-1);
        await SeedAsync("Ready", uploaded, processedAt: uploaded.AddSeconds(2));
        await SeedAsync("Ready", uploaded, processedAt: uploaded.AddSeconds(4));
        await SeedAsync("Failed", uploaded, processedAt: uploaded.AddSeconds(60));
        await SeedAsync("Ready", uploaded, processedAt: null);
        await SeedAsync("Pending", uploaded, processedAt: null);

        var analytics = await HandleAsync();

        analytics.AvgProcessingTimeMs.Should().BeApproximately(3000, 1,
            "(2 s + 4 s) / 2: the failed one (60 s), the ready one with no instant and the pending one are out");
        analytics.TotalDocuments.Should().Be(5);
        analytics.TotalProcessed.Should().Be(4, "Ready or Failed");
        analytics.SuccessRate.Should().Be(75, "three ready out of the four processed");
    }

    /// <summary>The table has rows but none has finished: the average of nothing is zero, not an error.</summary>
    [Fact]
    public async Task WhenNoDocumentHasFinished_TheAverageIsZero()
    {
        var uploaded = DateTime.UtcNow.AddMinutes(-5);
        await SeedAsync("Pending", uploaded, processedAt: null);
        await SeedAsync("Extracting", uploaded, processedAt: null);

        var analytics = await HandleAsync();

        analytics.TotalDocuments.Should().Be(2);
        analytics.AvgProcessingTimeMs.Should().Be(0);
    }

    // ─── Support ────────────────────────────────────────────────

    private async Task<Api.BoundedContexts.Administration.Application.DTOs.PdfAnalyticsDto> HandleAsync() =>
        await new GetPdfAnalyticsQueryHandler(_dbContext!).Handle(new GetPdfAnalyticsQuery(), Ct);

    private async Task SeedAsync(string state, DateTime uploadedAt, DateTime? processedAt)
    {
        var id = Guid.NewGuid();
        _dbContext!.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = $"analytics-{id:N}.pdf",
            FilePath = $"/tmp/analytics-{id:N}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = _uploaderId,
            UploadedAt = uploadedAt,
            ProcessingState = state,
            ProcessedAt = processedAt,
        });
        await _dbContext.SaveChangesAsync(Ct);
    }
}

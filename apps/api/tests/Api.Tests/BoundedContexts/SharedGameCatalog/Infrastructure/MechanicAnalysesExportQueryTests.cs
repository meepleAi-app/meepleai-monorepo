using System.Text;

using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>#532: CSV export emits a header + one row per analysis, with comma-containing values quoted.</summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysesExportQueryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;

    public MechanicAnalysesExportQueryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me532_export_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
        (_userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, Guid.NewGuid());
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task Export_EmitsHeaderAndRows_WithCsvEscaping()
    {
        var now = DateTime.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            var game = await MechanicMetricsSeed.GameAsync(scope, _userId, "Ticket, Ride"); // comma → must be quoted
            await MechanicMetricsSeed.AnalysisAsync(scope, game, _userId, status: 2, costUsd: 1.25m,
                createdAt: now.AddMinutes(-2), reviewedAt: now, reviewedBy: _userId);
            await MechanicMetricsSeed.AnalysisAsync(scope, game, _userId, status: 1, costUsd: 0.30m, createdAt: now.AddMinutes(-1));
        }

        Api.BoundedContexts.SharedGameCatalog.Application.DTOs.ExportMechanicAnalysesResult result;
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            result = await mediator.Send(new ExportMechanicAnalysesQuery());
        }

        result.ContentType.Should().Be("text/csv");
        result.FileName.Should().StartWith("mechanic-analyses-").And.EndWith(".csv");

        var csv = Encoding.UTF8.GetString(result.Content);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("Id,GameName,Status,ReviewerId,ReviewerName,CreatedAt,ReviewedAt,EstimatedCostUsd");
        lines.Should().HaveCount(3); // header + 2 rows
        csv.Should().Contain("\"Ticket, Ride\""); // comma-containing game name quoted
    }
}

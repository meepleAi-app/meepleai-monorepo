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

/// <summary>#532: the daily cost time-series buckets cost/count per day and gap-fills empty days.</summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCostByDayQueryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;

    public MechanicCostByDayQueryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me532_costday_{Guid.NewGuid():N}";
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
    public async Task CostByDay_SumsPerDay_AndGapFills()
    {
        var now = DateTime.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            var game = await MechanicMetricsSeed.GameAsync(scope, _userId);
            // Two InReview today (1.00 + 2.00), one InReview 3 days ago (5.00). InReview avoids the
            // one-Published-per-game constraint.
            await MechanicMetricsSeed.AnalysisAsync(scope, game, _userId, status: 1, costUsd: 1.00m, createdAt: now);
            await MechanicMetricsSeed.AnalysisAsync(scope, game, _userId, status: 1, costUsd: 2.00m, createdAt: now);
            await MechanicMetricsSeed.AnalysisAsync(scope, game, _userId, status: 1, costUsd: 5.00m, createdAt: now.AddDays(-3));
        }

        IReadOnlyList<Api.BoundedContexts.SharedGameCatalog.Application.DTOs.MechanicCostByDayDto> series;
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            series = await mediator.Send(new GetMechanicCostByDayQuery(Days: 7));
        }

        series.Should().HaveCount(7);
        var today = DateOnly.FromDateTime(now.Date);
        series.Single(d => d.Date == today).Should().BeEquivalentTo(new { CostUsd = 3.00m, AnalysisCount = 2 },
            o => o.ExcludingMissingMembers());
        series.Single(d => d.Date == today.AddDays(-3)).CostUsd.Should().Be(5.00m);
        series.Where(d => d.Date != today && d.Date != today.AddDays(-3)).Should().OnlyContain(d => d.CostUsd == 0m);
    }
}

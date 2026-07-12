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

/// <summary>#2837: DISTINCT game + reviewer filter options, deduped across all analyses (no recency cap).</summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicMetricsFilterOptionsQueryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;
    private Guid _reviewerId;

    public MechanicMetricsFilterOptionsQueryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me2837_options_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
        (_userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, Guid.NewGuid());
        (_reviewerId, _) = await TestSessionHelper.CreateAdminSessionAsync(db, Guid.NewGuid());
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
    public async Task FilterOptions_ReturnsDistinctGamesAndReviewers()
    {
        var now = DateTime.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            var g1 = await MechanicMetricsSeed.GameAsync(scope, _userId, "Catan");
            var g2 = await MechanicMetricsSeed.GameAsync(scope, _userId, "Carcassonne");
            // Same reviewer on two analyses (distinct dedup) + one unreviewed (no reviewer contribution).
            await MechanicMetricsSeed.AnalysisAsync(scope, g1, _userId, status: 2, costUsd: 1m,
                createdAt: now.AddMinutes(-3), reviewedAt: now, reviewedBy: _reviewerId);
            await MechanicMetricsSeed.AnalysisAsync(scope, g2, _userId, status: 3, costUsd: 1m,
                createdAt: now.AddMinutes(-2), reviewedAt: now, reviewedBy: _reviewerId, rejectionReason: "factual");
            await MechanicMetricsSeed.AnalysisAsync(scope, g2, _userId, status: 1, costUsd: 1m, createdAt: now.AddMinutes(-1));
        }

        Api.BoundedContexts.SharedGameCatalog.Application.DTOs.MechanicMetricsFilterOptionsDto result;
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            result = await mediator.Send(new GetMechanicMetricsFilterOptionsQuery());
        }

        result.Games.Select(g => g.Name).Should().BeEquivalentTo(new[] { "Carcassonne", "Catan" });
        result.Reviewers.Should().ContainSingle(r => r.Id == _reviewerId); // deduped despite 2 reviewed analyses
    }
}

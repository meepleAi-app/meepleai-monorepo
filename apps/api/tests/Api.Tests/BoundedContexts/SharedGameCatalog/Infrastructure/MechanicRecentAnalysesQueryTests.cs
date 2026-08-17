using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
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

/// <summary>#532: the recent-analyses query filters (game/reviewer/status), paginates, and resolves names.</summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicRecentAnalysesQueryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;
    private Guid _game1;
    private Guid _game2;

    public MechanicRecentAnalysesQueryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me532_recent_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
        (_userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, Guid.NewGuid());

        var now = DateTime.UtcNow;
        _game1 = await MechanicMetricsSeed.GameAsync(scope, _userId, "Catan");
        _game2 = await MechanicMetricsSeed.GameAsync(scope, _userId, "Carcassonne");
        // game1: Published (reviewed by _userId). game2: Rejected + InReview.
        await MechanicMetricsSeed.AnalysisAsync(scope, _game1, _userId, status: 2, costUsd: 1.00m,
            createdAt: now.AddMinutes(-3), reviewedAt: now, reviewedBy: _userId);
        await MechanicMetricsSeed.AnalysisAsync(scope, _game2, _userId, status: 3, costUsd: 0.50m,
            createdAt: now.AddMinutes(-2), reviewedAt: now, reviewedBy: _userId, rejectionReason: "factual");
        await MechanicMetricsSeed.AnalysisAsync(scope, _game2, _userId, status: 1, costUsd: 0.20m,
            createdAt: now.AddMinutes(-1));
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private async Task<MechanicRecentAnalysesResult> RunAsync(GetMechanicRecentAnalysesQuery q)
    {
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(q);
    }

    [Fact]
    public async Task Recent_ReturnsAll_WithNamesResolved()
    {
        var result = await RunAsync(new GetMechanicRecentAnalysesQuery());
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items.Should().Contain(i => i.GameName == "Catan" && i.Status == 2 && i.ReviewerName != null);
    }

    [Fact]
    public async Task Recent_FiltersByGame()
    {
        var result = await RunAsync(new GetMechanicRecentAnalysesQuery(GameId: _game1));
        result.TotalCount.Should().Be(1);
        result.Items.Should().OnlyContain(i => i.SharedGameId == _game1);
    }

    [Fact]
    public async Task Recent_FiltersByStatus()
    {
        var result = await RunAsync(new GetMechanicRecentAnalysesQuery(Status: 3));
        result.TotalCount.Should().Be(1);
        result.Items.Should().OnlyContain(i => i.Status == 3);
    }

    [Fact]
    public async Task Recent_Paginates()
    {
        var result = await RunAsync(new GetMechanicRecentAnalysesQuery(Limit: 1, Offset: 0));
        result.TotalCount.Should().Be(3); // total ignores paging
        result.Items.Should().HaveCount(1);
    }
}

using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// #2633 C2 — regression guard for the diary route collision. The GameNight diary GET was
/// registered twice on the v1 group (GameManagement + SessionFlow), same route template →
/// <c>AmbiguousMatchException</c> (HTTP 500) on every request. This test boots the real host and
/// asserts the endpoint is registered exactly once, so a future duplicate fails CI, not prod.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightDiaryRouteUniquenessTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;

    public GameNightDiaryRouteUniquenessTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"gn_diary_route_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);
        _ = _factory.Services; // force host build so the EndpointDataSource is populated
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public void GameNightDiary_IsRegisteredExactlyOnce()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        var diaryEndpoints = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } raw
                && raw.Contains("game-nights", StringComparison.OrdinalIgnoreCase)
                && raw.EndsWith("/diary", StringComparison.OrdinalIgnoreCase))
            .ToList();

        diaryEndpoints.Should().ContainSingle(
            "the GameNight diary GET must map to exactly one endpoint — a duplicate route template " +
            "throws AmbiguousMatchException (HTTP 500) at request time (#2633 C2)");
    }
}

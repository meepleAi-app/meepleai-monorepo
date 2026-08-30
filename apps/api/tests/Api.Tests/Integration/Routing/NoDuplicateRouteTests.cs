using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Routing;

/// <summary>
/// Regression guard for duplicate route registrations.
///
/// Two endpoints registered on the same (method, template) do not fail at startup:
/// ASP.NET throws <c>AmbiguousMatchException</c> at REQUEST time, so the endpoint
/// looks healthy until someone calls it and gets a 500. It happened to
/// <c>POST /admin/impersonation/end</c>, registered both by the dedicated
/// impersonation file and by the user-activity file composed into
/// MapAdminUserEndpoints — leaving admins unable to end an impersonation session.
///
/// <see cref="GameNightDiaryRouteUniquenessTests"/> guards one specific route;
/// this one covers the whole table, so the next duplicate fails in CI instead of
/// in production.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed class NoDuplicateRouteTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;

    public NoDuplicateRouteTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"dup_routes_{Guid.NewGuid():N}";
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
    public void NoRouteTemplate_IsRegisteredTwiceForTheSameHttpMethod()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        var duplicates = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e =>
                (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new[] { "*" })
                    .Select(method => new
                    {
                        Key = $"{method} {e.RoutePattern.RawText}",
                        Endpoint = e.DisplayName ?? "(senza nome)"
                    }))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} → {g.Count()} registrazioni: {string.Join(" | ", g.Select(x => x.Endpoint))}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        // The duplicates are listed in the failure reason on purpose: the assertion
        // message truncates the collection, and a guard that says "there is a
        // duplicate" without naming it sends the reader back to square one.
        duplicates.Should().BeEmpty(
            "a route registered twice for the same method throws AmbiguousMatchException (HTTP 500) " +
            "on every request, while the endpoint looks correctly registered. Duplicates found: " +
            string.Join(" ;; ", duplicates));
    }

    /// <summary>
    /// Pins the #3840 decision: the two live-scoring confirmations are separate routes.
    ///
    /// <c>/scores/confirm</c> confirms a reading produced by the assistant — any session
    /// participant may call it. <c>/scores/proposals/confirm</c> is the host ratifying
    /// another player's proposal, and it broadcasts over SignalR. They were registered on
    /// the same template, so both answered 500; merging them back would restore that.
    ///
    /// The generic guard above only catches the duplicate. Without this test, deleting
    /// either registration outright would also be "no duplicates" — and silently drop one
    /// of the two flows.
    /// </summary>
    [Fact]
    public void BothLiveScoringConfirmations_AreRegisteredExactlyOnce()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        // RawText may or may not carry the leading slash depending on how the group was
        // composed; normalising here keeps the assertion about the route, not its spelling.
        var postTemplates = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST") == true)
            .Select(e => (e.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .ToList();

        postTemplates.Should().ContainSingle(
            t => string.Equals(t, "api/v1/live-sessions/{sessionId}/scores/confirm", StringComparison.OrdinalIgnoreCase),
            "the participant-facing confirmation (ConfirmScoreCommand) must stay reachable exactly once");

        postTemplates.Should().ContainSingle(
            t => string.Equals(t, "api/v1/live-sessions/{sessionId}/scores/proposals/confirm", StringComparison.OrdinalIgnoreCase),
            "the host-facing proposal ratification (ConfirmScoreProposalCommand) must stay reachable exactly once");
    }
}

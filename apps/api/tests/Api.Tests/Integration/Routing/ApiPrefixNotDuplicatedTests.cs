using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Routing;

/// <summary>
/// #3831 — regression guard for the doubled `/api/v1` prefix.
///
/// `Program.cs` registers most endpoint files on `v1Api`, which is already
/// `app.MapGroup("/api/v1")`. A file that ALSO declares its own group with the full
/// `/api/v1/...` prefix gets the two nested by ASP.NET, and lands on
/// `/api/v1/api/v1/...` — a path no client calls, so every request 404s while the
/// endpoint looks perfectly registered. Four families shipped that way
/// (reports, alert configuration, LLM analytics, permissions) and no test covered them.
///
/// Files registered on `app` instead of `v1Api` legitimately declare the full prefix:
/// this test targets the duplication, not the hardcoded prefix.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed class ApiPrefixNotDuplicatedTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;

    public ApiPrefixNotDuplicatedTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"api_prefix_{Guid.NewGuid():N}";
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
    public void NoEndpoint_IsRegisteredUnderADoubledApiV1Prefix()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        var doubled = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText)
            .Where(raw => raw is not null
                && raw.Contains("api/v1/api/v1", StringComparison.OrdinalIgnoreCase))
            .OrderBy(raw => raw, StringComparer.Ordinal)
            .ToList();

        doubled.Should().BeEmpty(
            "an endpoint mounted under a doubled /api/v1 prefix is unreachable: clients call the " +
            "single-prefix path and get 404, while the endpoint looks registered (#3831)");
    }
}

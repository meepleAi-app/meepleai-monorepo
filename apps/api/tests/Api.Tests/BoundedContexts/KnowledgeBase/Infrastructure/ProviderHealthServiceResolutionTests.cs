using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Regression guard for #2865: <see cref="IProviderHealthCheckService"/> must resolve from the
/// integration DI container without depending on <c>ProviderHealthCheckService</c> being present
/// in the <c>IHostedService</c> collection.
///
/// <para>
/// The integration factory strips ALL <c>IHostedService</c> registrations
/// (<see cref="IntegrationWebApplicationFactory"/>, "prevents background service startup failures").
/// The previous registration resolved the singleton via
/// <c>GetServices&lt;IHostedService&gt;().OfType&lt;ProviderHealthCheckService&gt;().ToList()[0]</c>,
/// so under the stripped container that list was empty and <c>[0]</c> threw
/// <see cref="ArgumentOutOfRangeException"/> — which the API exception middleware mapped to HTTP 400.
/// Any integration test that resolves <c>ILlmService</c> (→ HybridLlmService → provider health)
/// tripped this, e.g. <c>ExtractMetadata_WithValidFilePath_Returns200Ok</c>.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "2865")]
public sealed class ProviderHealthServiceResolutionTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;

    public ProviderHealthServiceResolutionTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"provider_health_di_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // A valid connection string is enough — pure DI resolution does not touch the schema.
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public void IProviderHealthCheckService_ResolvesUnderStrippedHostedServices_WithoutThrowing()
    {
        using var scope = _factory.Services.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IProviderHealthCheckService>();

        act.Should().NotThrow(
            "the injectable IProviderHealthCheckService must not depend on the IHostedService " +
            "collection being populated (the integration factory strips hosted services) — #2865");
    }
}

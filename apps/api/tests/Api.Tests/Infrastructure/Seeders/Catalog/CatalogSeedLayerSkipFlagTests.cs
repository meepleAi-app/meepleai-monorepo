using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Tests.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

[Trait("Category", TestCategories.Unit)]
public sealed class CatalogSeedLayerSkipFlagTests
{
    [Fact]
    public async Task SeedAsync_WhenSkipFlagTrue_ReturnsImmediately()
    {
        // Arrange: configuration with SKIP_CATALOG_SEED=true
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SKIP_CATALOG_SEED"] = "true"
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        // DbContext is intentionally null — the skip path must NOT dereference it.
        // If the skip logic is broken we expect a NullReferenceException or a
        // ServiceNotFound exception when downstream services are resolved.
        var context = new SeedContext(
            Profile: SeedProfile.Dev,
            DbContext: null!,
            Services: services,
            Logger: NullLogger.Instance,
            SystemUserId: Guid.NewGuid());

        var layer = new CatalogSeedLayer();

        // Act + Assert: must complete without throwing
        await layer.SeedAsync(context, default);
    }

    [Fact]
    public async Task SeedAsync_WhenSkipFlagFalse_AttemptsToResolveDependencies()
    {
        // Arrange: SKIP_CATALOG_SEED not set — layer should try to resolve
        // IBggApiService and fail because it's not registered.
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var context = new SeedContext(
            Profile: SeedProfile.Dev,
            DbContext: null!,
            Services: services,
            Logger: NullLogger.Instance,
            SystemUserId: Guid.NewGuid());

        var layer = new CatalogSeedLayer();

        // Act + Assert: must throw because IBggApiService is not registered.
        // This proves the skip-path is not being taken when the flag is absent.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await layer.SeedAsync(context, default));
    }
}

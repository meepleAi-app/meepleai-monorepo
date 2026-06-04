using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Integration tests for <see cref="CatalogSeedDraftRepository"/>.
/// Uses Testcontainers PostgreSQL through <see cref="SharedTestcontainersFixture"/>
/// with an isolated database per test class.
/// Issue #1903 — M1.5.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Collection("Integration-GroupC")]
public sealed class CatalogSeedDraftRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fx;
    private string _dbName = string.Empty;
    private string _connStr = string.Empty;

    public CatalogSeedDraftRepositoryTests(SharedTestcontainersFixture fx) => _fx = fx;

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_catseedrepo_{Guid.NewGuid():N}";
        _connStr = await _fx.CreateIsolatedDatabaseAsync(_dbName);
        await using var ctx = _fx.CreateDbContext(_connStr);
        await ctx.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _fx.DropIsolatedDatabaseAsync(_dbName);

    [Fact]
    public async Task AddAsync_PersistsAndReturnsViaGetByIdAsync()
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        var repo = new CatalogSeedDraftRepository(ctx);

        var entity = new CatalogSeedDraftEntity
        {
            BggId = 13,
            Status = "Pending",
            CreatedByUserId = Guid.NewGuid(),
        };
        await repo.AddAsync(entity, default);
        await ctx.SaveChangesAsync();

        var fetched = await repo.GetByIdAsync(entity.Id, default);
        fetched.Should().NotBeNull();
        fetched!.BggId.Should().Be(13);
    }

    [Fact]
    public async Task GetByStatusAsync_FiltersAndLimits()
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        var repo = new CatalogSeedDraftRepository(ctx);
        var userId = Guid.NewGuid();

        for (var i = 0; i < 7; i++)
        {
            await repo.AddAsync(new CatalogSeedDraftEntity
            {
                BggId = i,
                Status = i < 4 ? "Pending" : "Fetched",
                CreatedByUserId = userId,
            }, default);
        }
        await ctx.SaveChangesAsync();

        var pending = await repo.GetByStatusAsync("Pending", take: 10, default);
        pending.Should().HaveCount(4);

        var top2 = await repo.GetByStatusAsync("Pending", take: 2, default);
        top2.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExistsAsync_ChecksBggId()
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        var repo = new CatalogSeedDraftRepository(ctx);

        await repo.AddAsync(new CatalogSeedDraftEntity
        {
            BggId = 99,
            Status = "Pending",
            CreatedByUserId = Guid.NewGuid(),
        }, default);
        await ctx.SaveChangesAsync();

        (await repo.ExistsAsync(99, default)).Should().BeTrue();
        (await repo.ExistsAsync(100, default)).Should().BeFalse();
    }
}

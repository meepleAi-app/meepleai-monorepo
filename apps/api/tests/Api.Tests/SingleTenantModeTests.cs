using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public class SingleTenantModeTests
{
    [Fact]
    public async Task DbContext_UsesDefaultTenant_WhenNoneProvided()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new MeepleAiDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();

            setup.Tenants.Add(new TenantEntity
            {
                Id = "meepleai",
                Name = "MeepleAI"
            });

            setup.Games.AddRange(
                new GameEntity { Id = "game-default", TenantId = "meepleai", Name = "Default Game" },
                new GameEntity { Id = "game-legacy", TenantId = "legacy", Name = "Legacy Game" });

            await setup.SaveChangesAsync();
        }

        await using var context = new MeepleAiDbContext(options, Options.Create(new SingleTenantOptions { TenantId = "meepleai" }));

        var games = await context.Games.AsNoTracking().ToListAsync();

        Assert.Single(games);
        Assert.Equal("game-default", games[0].Id);
        Assert.Equal("meepleai", games[0].TenantId);
    }

    [Fact]
    public async Task DbContext_HonorsExplicitTenant_WhenProvided()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new MeepleAiDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();

            setup.Tenants.AddRange(
                new TenantEntity { Id = "meepleai", Name = "MeepleAI" },
                new TenantEntity { Id = "legacy", Name = "Legacy" });

            setup.Games.AddRange(
                new GameEntity { Id = "game-default", TenantId = "meepleai", Name = "Default Game" },
                new GameEntity { Id = "game-legacy", TenantId = "legacy", Name = "Legacy Game" });

            await setup.SaveChangesAsync();
        }

        await using var context = new MeepleAiDbContext(options, Options.Create(new SingleTenantOptions { TenantId = "legacy" }));

        var games = await context.Games.AsNoTracking().ToListAsync();

        Assert.Single(games);
        Assert.Equal("game-legacy", games[0].Id);
    }

}

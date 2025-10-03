using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

public class RuleSpecServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecService _service;

    public RuleSpecServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        _service = new RuleSpecService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetOrCreateDemoAsync_CreatesNewSpec_WhenNotExists()
    {
        // Arrange
        var tenantId = "test-tenant";
        var gameId = "test-game";

        // Act
        var result = await _service.GetOrCreateDemoAsync(tenantId, gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result.gameId);
        Assert.Equal(2, result.rules.Count); // Demo creates 2 atoms
        Assert.Equal("r1", result.rules[0].id);
        Assert.Equal("Two players.", result.rules[0].text);
    }

    [Fact]
    public async Task GetOrCreateDemoAsync_ReturnExistingSpec_WhenExists()
    {
        // Arrange
        var tenantId = "test-tenant";
        var gameId = "test-game";

        // Create tenant and game
        var tenant = new TenantEntity
        {
            Id = tenantId,
            Name = "Test Tenant",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);

        var game = new GameEntity
        {
            Id = gameId,
            TenantId = tenantId,
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        var spec = new RuleSpecEntity
        {
            TenantId = tenantId,
            GameId = gameId,
            Version = "v1-test",
            CreatedAt = DateTime.UtcNow
        };
        spec.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = spec,
            Key = "custom-r1",
            Text = "Custom rule text",
            Section = "Custom",
            PageNumber = 1,
            LineNumber = 1,
            SortOrder = 1
        });
        _dbContext.RuleSpecs.Add(spec);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetOrCreateDemoAsync(tenantId, gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("v1-test", result.version);
        Assert.Single(result.rules);
        Assert.Equal("custom-r1", result.rules[0].id);
    }

    [Fact]
    public async Task GetRuleSpecAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var tenantId = "nonexistent-tenant";
        var gameId = "nonexistent-game";

        // Act
        var result = await _service.GetRuleSpecAsync(tenantId, gameId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRuleSpecAsync_ReturnsSpec_WhenExists()
    {
        // Arrange
        var tenantId = "test-tenant";
        var gameId = "test-game";

        // Create tenant and game
        var tenant = new TenantEntity
        {
            Id = tenantId,
            Name = "Test Tenant",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);

        var game = new GameEntity
        {
            Id = gameId,
            TenantId = tenantId,
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        var spec = new RuleSpecEntity
        {
            TenantId = tenantId,
            GameId = gameId,
            Version = "v1-test",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RuleSpecs.Add(spec);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRuleSpecAsync(tenantId, gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result.gameId);
        Assert.Equal("v1-test", result.version);
    }

    [Fact]
    public async Task GetOrCreateDemoAsync_CreatesTenant_WhenNotExists()
    {
        // Arrange
        var tenantId = "new-tenant";
        var gameId = "new-game";

        // Act
        await _service.GetOrCreateDemoAsync(tenantId, gameId);

        // Assert
        var tenant = await _dbContext.Tenants.FindAsync(tenantId);
        Assert.NotNull(tenant);
        Assert.Equal(tenantId, tenant.Id);
        Assert.Equal(tenantId, tenant.Name); // Default name is same as ID
    }

    [Fact]
    public async Task GetOrCreateDemoAsync_CreatesGame_WhenNotExists()
    {
        // Arrange
        var tenantId = "new-tenant";
        var gameId = "new-game";

        // Act
        await _service.GetOrCreateDemoAsync(tenantId, gameId);

        // Assert
        var game = await _dbContext.Games
            .FirstOrDefaultAsync(g => g.Id == gameId && g.TenantId == tenantId);
        Assert.NotNull(game);
        Assert.Equal(gameId, game.Id);
        Assert.Equal(tenantId, game.TenantId);
    }

    [Fact]
    public async Task GetRuleSpecAsync_ReturnsLatestSpec_WhenMultipleExist()
    {
        // Arrange
        var tenantId = "test-tenant";
        var gameId = "test-game";

        var tenant = new TenantEntity
        {
            Id = tenantId,
            Name = "Test Tenant",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);

        var game = new GameEntity
        {
            Id = gameId,
            TenantId = tenantId,
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        // Create two specs - older one first
        var olderSpec = new RuleSpecEntity
        {
            TenantId = tenantId,
            GameId = gameId,
            Version = "v1-old",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _dbContext.RuleSpecs.Add(olderSpec);

        var newerSpec = new RuleSpecEntity
        {
            TenantId = tenantId,
            GameId = gameId,
            Version = "v2-new",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RuleSpecs.Add(newerSpec);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRuleSpecAsync(tenantId, gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("v2-new", result.version); // Should return the newer version
    }
}

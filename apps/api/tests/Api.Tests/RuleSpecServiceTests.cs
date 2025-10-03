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

    [Fact]
    public async Task UpdateRuleSpecAsync_ThrowsException_WhenGameNotExists()
    {
        // Arrange
        var tenantId = "test-tenant";
        var gameId = "nonexistent-game";
        var ruleSpec = new Api.Models.RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<Api.Models.RuleAtom>()
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateRuleSpecAsync(tenantId, gameId, ruleSpec)
        );
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task UpdateRuleSpecAsync_CreatesNewVersion_WhenGameExists()
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
        await _dbContext.SaveChangesAsync();

        var ruleSpec = new Api.Models.RuleSpec(
            gameId,
            "v2-updated",
            DateTime.UtcNow,
            new List<Api.Models.RuleAtom>
            {
                new Api.Models.RuleAtom("rule1", "Test rule 1", "Section A", "1", "1"),
                new Api.Models.RuleAtom("rule2", "Test rule 2", "Section B", "2", "3")
            }
        );

        // Act
        var result = await _service.UpdateRuleSpecAsync(tenantId, gameId, ruleSpec);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("v2-updated", result.version);
        Assert.Equal(2, result.rules.Count);
        Assert.Equal("rule1", result.rules[0].id);
        Assert.Equal("Test rule 1", result.rules[0].text);
    }

    [Fact]
    public async Task UpdateRuleSpecAsync_StoresRulesInCorrectOrder()
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
        await _dbContext.SaveChangesAsync();

        var ruleSpec = new Api.Models.RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<Api.Models.RuleAtom>
            {
                new Api.Models.RuleAtom("r3", "Third", null, null, null),
                new Api.Models.RuleAtom("r1", "First", null, null, null),
                new Api.Models.RuleAtom("r2", "Second", null, null, null)
            }
        );

        // Act
        var result = await _service.UpdateRuleSpecAsync(tenantId, gameId, ruleSpec);

        // Assert - should maintain order from input
        Assert.Equal("r3", result.rules[0].id);
        Assert.Equal("r1", result.rules[1].id);
        Assert.Equal("r2", result.rules[2].id);
    }

    [Fact]
    public async Task UpdateRuleSpecAsync_ParsesPageAndLineNumbers()
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
        await _dbContext.SaveChangesAsync();

        var ruleSpec = new Api.Models.RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<Api.Models.RuleAtom>
            {
                new Api.Models.RuleAtom("r1", "Rule with numbers", "Section", "42", "15")
            }
        );

        // Act
        var result = await _service.UpdateRuleSpecAsync(tenantId, gameId, ruleSpec);

        // Assert
        Assert.Equal("42", result.rules[0].page);
        Assert.Equal("15", result.rules[0].line);
    }

    [Fact]
    public async Task UpdateRuleSpecAsync_HandlesNullPageAndLine()
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
        await _dbContext.SaveChangesAsync();

        var ruleSpec = new Api.Models.RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<Api.Models.RuleAtom>
            {
                new Api.Models.RuleAtom("r1", "Rule without location", null, null, null)
            }
        );

        // Act
        var result = await _service.UpdateRuleSpecAsync(tenantId, gameId, ruleSpec);

        // Assert
        Assert.Null(result.rules[0].section);
        Assert.Null(result.rules[0].page);
        Assert.Null(result.rules[0].line);
    }

    [Fact]
    public async Task UpdateRuleSpecAsync_HandlesInvalidPageAndLineNumbers()
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
        await _dbContext.SaveChangesAsync();

        var ruleSpec = new Api.Models.RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<Api.Models.RuleAtom>
            {
                new Api.Models.RuleAtom("r1", "Rule with invalid numbers", "Section", "not-a-number", "also-not-a-number")
            }
        );

        // Act
        var result = await _service.UpdateRuleSpecAsync(tenantId, gameId, ruleSpec);

        // Assert - should handle gracefully and store as null
        Assert.Null(result.rules[0].page);
        Assert.Null(result.rules[0].line);
    }
}

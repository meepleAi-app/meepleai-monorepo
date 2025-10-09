using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for seed data migration (DB-02).
///
/// Feature: DB-02 - Seed Dataset Demo
/// As a developer or tester
/// I want demo data to be automatically seeded in the database
/// So that I can quickly test the application without manual data setup
///
/// Scenarios Tested:
/// - Demo users (Admin, Editor, User) are present with known credentials
/// - Demo games (Tic-Tac-Toe, Chess) are present
/// - Demo rule specs are associated with games
/// - Demo agents are created for each game
/// - Seed data is idempotent (can run multiple times safely)
/// </summary>
public class SeedDataTests : IntegrationTestBase
{
    public SeedDataTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    #region Demo Users Tests

    /// <summary>
    /// Scenario: Demo admin user exists after migration
    ///   Given database migrations have been applied
    ///   When I query for admin user
    ///   Then admin@meepleai.dev exists
    ///   And user has Admin role
    ///   And user has DisplayName "Demo Admin"
    /// </summary>
    [Fact]
    public async Task SeedData_DemoAdminUserExists()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Query for demo admin user
        var admin = await db.Users
            .FirstOrDefaultAsync(u => u.Email == "admin@meepleai.dev");

        // Then: Admin user exists with correct properties
        Assert.NotNull(admin);
        Assert.Equal("demo-admin-001", admin!.Id);
        Assert.Equal("Demo Admin", admin.DisplayName);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.NotEmpty(admin.PasswordHash);
    }

    /// <summary>
    /// Scenario: Demo editor user exists after migration
    ///   Given database migrations have been applied
    ///   When I query for editor user
    ///   Then editor@meepleai.dev exists
    ///   And user has Editor role
    ///   And user has DisplayName "Demo Editor"
    /// </summary>
    [Fact]
    public async Task SeedData_DemoEditorUserExists()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Query for demo editor user
        var editor = await db.Users
            .FirstOrDefaultAsync(u => u.Email == "editor@meepleai.dev");

        // Then: Editor user exists with correct properties
        Assert.NotNull(editor);
        Assert.Equal("demo-editor-001", editor!.Id);
        Assert.Equal("Demo Editor", editor.DisplayName);
        Assert.Equal(UserRole.Editor, editor.Role);
        Assert.NotEmpty(editor.PasswordHash);
    }

    /// <summary>
    /// Scenario: Demo regular user exists after migration
    ///   Given database migrations have been applied
    ///   When I query for regular user
    ///   Then user@meepleai.dev exists
    ///   And user has User role
    ///   And user has DisplayName "Demo User"
    /// </summary>
    [Fact]
    public async Task SeedData_DemoRegularUserExists()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Query for demo regular user
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == "user@meepleai.dev");

        // Then: Regular user exists with correct properties
        Assert.NotNull(user);
        Assert.Equal("demo-user-001", user!.Id);
        Assert.Equal("Demo User", user.DisplayName);
        Assert.Equal(UserRole.User, user.Role);
        Assert.NotEmpty(user.PasswordHash);
    }

    #endregion

    #region Demo Games Tests

    /// <summary>
    /// Scenario: Demo Tic-Tac-Toe game exists after migration
    ///   Given database migrations have been applied
    ///   When I query for Tic-Tac-Toe game
    ///   Then game with ID 'tic-tac-toe' exists
    ///   And game name is "Tic-Tac-Toe"
    /// </summary>
    [Fact]
    public async Task SeedData_TicTacToeGameExists()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Query for Tic-Tac-Toe game
        var game = await db.Games
            .FirstOrDefaultAsync(g => g.Id == "tic-tac-toe");

        // Then: Game exists with correct properties
        Assert.NotNull(game);
        Assert.Equal("Tic-Tac-Toe", game!.Name);
    }

    /// <summary>
    /// Scenario: Demo Chess game exists after migration
    ///   Given database migrations have been applied
    ///   When I query for Chess game
    ///   Then game with ID 'chess' exists
    ///   And game name is "Chess"
    /// </summary>
    [Fact]
    public async Task SeedData_ChessGameExists()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Query for Chess game
        var game = await db.Games
            .FirstOrDefaultAsync(g => g.Id == "chess");

        // Then: Game exists with correct properties
        Assert.NotNull(game);
        Assert.Equal("Chess", game!.Name);
    }

    #endregion

    #region Demo Rule Specs Tests

    /// <summary>
    /// Scenario: Demo rule spec exists for Tic-Tac-Toe
    ///   Given database migrations have been applied
    ///   When I query for Tic-Tac-Toe rule specs
    ///   Then rule spec with version "v1.0" exists
    ///   And rule spec is created by demo admin
    /// </summary>
    [Fact]
    public async Task SeedData_TicTacToeRuleSpecExists()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Query for Tic-Tac-Toe rule spec
        var ruleSpec = await db.RuleSpecs
            .Include(r => r.Game)
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.GameId == "tic-tac-toe" && r.Version == "v1.0");

        // Then: Rule spec exists with correct properties
        Assert.NotNull(ruleSpec);
        Assert.Equal("tic-tac-toe", ruleSpec!.GameId);
        Assert.Equal("v1.0", ruleSpec.Version);
        Assert.Equal("demo-admin-001", ruleSpec.CreatedByUserId);
        Assert.NotNull(ruleSpec.Game);
        Assert.Equal("Tic-Tac-Toe", ruleSpec.Game.Name);
    }

    /// <summary>
    /// Scenario: Demo rule spec exists for Chess
    ///   Given database migrations have been applied
    ///   When I query for Chess rule specs
    ///   Then rule spec with version "v1.0" exists
    ///   And rule spec is created by demo admin
    /// </summary>
    [Fact]
    public async Task SeedData_ChessRuleSpecExists()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Query for Chess rule spec
        var ruleSpec = await db.RuleSpecs
            .Include(r => r.Game)
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.GameId == "chess" && r.Version == "v1.0");

        // Then: Rule spec exists with correct properties
        Assert.NotNull(ruleSpec);
        Assert.Equal("chess", ruleSpec!.GameId);
        Assert.Equal("v1.0", ruleSpec.Version);
        Assert.Equal("demo-admin-001", ruleSpec.CreatedByUserId);
        Assert.NotNull(ruleSpec.Game);
        Assert.Equal("Chess", ruleSpec.Game.Name);
    }

    #endregion

    #region Demo Agents Tests

    /// <summary>
    /// Scenario: Demo agents exist for Tic-Tac-Toe
    ///   Given database migrations have been applied
    ///   When I query for Tic-Tac-Toe agents
    ///   Then two agents exist (explain and qa)
    ///   And agents have correct names and kinds
    /// </summary>
    [Fact]
    public async Task SeedData_TicTacToeAgentsExist()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Query for Tic-Tac-Toe agents
        var agents = await db.Agents
            .Where(a => a.GameId == "tic-tac-toe")
            .OrderBy(a => a.Kind)
            .ToListAsync();

        // Then: Two agents exist with correct properties
        Assert.Equal(2, agents.Count);

        var explainAgent = agents.FirstOrDefault(a => a.Kind == "explain");
        Assert.NotNull(explainAgent);
        Assert.Equal("agent-ttt-explain", explainAgent!.Id);
        Assert.Equal("Tic-Tac-Toe Explainer", explainAgent.Name);

        var qaAgent = agents.FirstOrDefault(a => a.Kind == "qa");
        Assert.NotNull(qaAgent);
        Assert.Equal("agent-ttt-qa", qaAgent!.Id);
        Assert.Equal("Tic-Tac-Toe Q&A", qaAgent.Name);
    }

    /// <summary>
    /// Scenario: Demo agents exist for Chess
    ///   Given database migrations have been applied
    ///   When I query for Chess agents
    ///   Then two agents exist (explain and qa)
    ///   And agents have correct names and kinds
    /// </summary>
    [Fact]
    public async Task SeedData_ChessAgentsExist()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Query for Chess agents
        var agents = await db.Agents
            .Where(a => a.GameId == "chess")
            .OrderBy(a => a.Kind)
            .ToListAsync();

        // Then: Two agents exist with correct properties
        Assert.Equal(2, agents.Count);

        var explainAgent = agents.FirstOrDefault(a => a.Kind == "explain");
        Assert.NotNull(explainAgent);
        Assert.Equal("agent-chess-explain", explainAgent!.Id);
        Assert.Equal("Chess Explainer", explainAgent.Name);

        var qaAgent = agents.FirstOrDefault(a => a.Kind == "qa");
        Assert.NotNull(qaAgent);
        Assert.Equal("agent-chess-qa", qaAgent!.Id);
        Assert.Equal("Chess Q&A", qaAgent.Name);
    }

    #endregion

    #region Data Integrity Tests

    /// <summary>
    /// Scenario: All seed data entities are properly linked
    ///   Given database migrations have been applied
    ///   When I verify data relationships
    ///   Then all foreign keys are valid
    ///   And no orphaned records exist
    /// </summary>
    [Fact]
    public async Task SeedData_AllDataIsProperlyLinked()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Query all seed data with relationships
        var gameIds = new[] { "tic-tac-toe", "chess" };
        var games = await db.Games
            .Where(g => gameIds.Contains(g.Id))
            .ToListAsync();

        // Then: All relationships are valid
        Assert.Equal(2, games.Count);

        foreach (var game in games)
        {
            // Each game should have at least one rule spec
            var ruleSpecs = await db.RuleSpecs
                .Where(r => r.GameId == game.Id)
                .ToListAsync();
            Assert.NotEmpty(ruleSpecs);

            // Each game should have exactly two agents (explain and qa)
            var agents = await db.Agents
                .Where(a => a.GameId == game.Id)
                .ToListAsync();
            Assert.Equal(2, agents.Count);

            // Verify agent kinds
            var agentKinds = agents.Select(a => a.Kind).OrderBy(k => k).ToList();
            Assert.Contains("explain", agentKinds);
            Assert.Contains("qa", agentKinds);
        }
    }

    /// <summary>
    /// Scenario: Seed data count matches expected baseline
    ///   Given database migrations have been applied
    ///   When I count seed entities
    ///   Then minimum expected counts are met:
    ///   - At least 3 users (admin, editor, user)
    ///   - At least 2 games (tic-tac-toe, chess)
    ///   - At least 2 rule specs (one per game)
    ///   - At least 4 agents (two per game)
    /// </summary>
    [Fact]
    public async Task SeedData_MinimumBaselineDataExists()
    {
        // Given: Database is initialized
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Count seed data entities
        var userCount = await db.Users
            .Where(u => u.Email.EndsWith("@meepleai.dev"))
            .CountAsync();

        var gameCount = await db.Games
            .Where(g => g.Id == "tic-tac-toe" || g.Id == "chess")
            .CountAsync();

        var ruleSpecCount = await db.RuleSpecs
            .Where(r => r.GameId == "tic-tac-toe" || r.GameId == "chess")
            .Where(r => r.Version == "v1.0")
            .CountAsync();

        var agentCount = await db.Agents
            .Where(a => a.GameId == "tic-tac-toe" || a.GameId == "chess")
            .CountAsync();

        // Then: Minimum counts are met
        Assert.True(userCount >= 3, $"Expected at least 3 demo users, found {userCount}");
        Assert.True(gameCount >= 2, $"Expected at least 2 demo games, found {gameCount}");
        Assert.True(ruleSpecCount >= 2, $"Expected at least 2 demo rule specs, found {ruleSpecCount}");
        Assert.True(agentCount >= 4, $"Expected at least 4 demo agents, found {agentCount}");
    }

    #endregion
}

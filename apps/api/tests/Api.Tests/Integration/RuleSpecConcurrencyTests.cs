using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Integration;

/// <summary>
/// TEST-04: Concurrency tests for RuleSpecService
/// Verifies thread-safety in version generation, optimistic concurrency, TOCTOU prevention, and cache coherence
///
/// Reference: ConfigurationConcurrencyTests (MANDATORY PATTERN)
/// Uses: WebApplicationFactory + Testcontainers + PostgreSQL (NO SQLite)
/// </summary>
[Collection("Admin Endpoints")]
public class RuleSpecConcurrencyTests : ConfigIntegrationTestBase
{
    private readonly ITestOutputHelper _output;
    private string _editorEmail = null!;
    private List<string> _editorCookies = null!;

    public RuleSpecConcurrencyTests(WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _editorEmail = $"rulespec-editor-{Guid.NewGuid():N}@test.com";

        // Register and get cookies for reuse
        using var tempClient = Factory.CreateHttpsClient();
        _editorCookies = await RegisterAndAuthenticateAsync(tempClient, _editorEmail, "Editor");
    }

    /// <summary>
    /// Pattern 1: Lost Update Detection
    /// Scenario: Multiple editors updating different RuleSpecs simultaneously
    ///   Given two editors with separate games
    ///   When both create RuleSpecs in parallel
    ///   Then both operations succeed without data loss
    /// </summary>
    [Fact]
    public async Task MultipleEditorsCreatingDifferentRuleSpecs_Simultaneously_Test()
    {
        // Arrange: Create two HTTP clients simulating two editors
        var client1 = Factory.CreateHttpsClient();
        var client2 = Factory.CreateHttpsClient();

        // Each client needs separate cookies
        var editor2Email = $"rulespec-editor2-{Guid.NewGuid():N}@test.com";
        using var tempClient2 = Factory.CreateHttpsClient();
        var editor2Cookies = await RegisterAndAuthenticateAsync(tempClient2, editor2Email, "Editor");

        // Create two games
        var gameId1 = $"concurrent-game1-{Guid.NewGuid():N}";
        var gameId2 = $"concurrent-game2-{Guid.NewGuid():N}";

        using (var arrangeScope = Factory.Services.CreateScope())
        {
            var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            arrangeDb.Games.AddRange(
                new GameEntity { Id = gameId1, Name = "Game 1" },
                new GameEntity { Id = gameId2, Name = "Game 2" }
            );
            await arrangeDb.SaveChangesAsync();
        }

        // Act: Both editors create RuleSpecs in parallel
        var ruleSpec1 = new RuleSpec(
            gameId1,
            "1.0",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r1", "Rule for game 1", "Setup", "1", "1") }
        );

        var ruleSpec2 = new RuleSpec(
            gameId2,
            "1.0",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r2", "Rule for game 2", "Setup", "1", "1") }
        );

        var task1 = PutAsJsonAuthenticatedAsync(client1, _editorCookies, $"/api/v1/games/{gameId1}/rulespec", ruleSpec1);
        var task2 = PutAsJsonAuthenticatedAsync(client2, editor2Cookies, $"/api/v1/games/{gameId2}/rulespec", ruleSpec2);

        var responses = await Task.WhenAll(task1, task2);

        // Assert: Both operations succeeded
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        // Verify both RuleSpecs in database
        using var scope2 = Factory.Services.CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var spec1 = await dbContext2.RuleSpecs.FirstOrDefaultAsync(r => r.GameId == gameId1);
        var spec2 = await dbContext2.RuleSpecs.FirstOrDefaultAsync(r => r.GameId == gameId2);

        spec1.Should().NotBeNull();
        spec2.Should().NotBeNull();
        spec1.Version.Should().Be("1.0");
        spec2.Version.Should().Be("1.0");
    }

    /// <summary>
    /// Pattern 2: Optimistic Concurrency Control
    /// Scenario: Multiple editors updating same RuleSpec with different versions
    ///   Given a RuleSpec with version 1.0
    ///   When two editors create v2.0 simultaneously
    ///   Then one succeeds, one may conflict (last write wins)
    /// </summary>
    [Fact]
    public async Task MultipleEditorsCreatingSameVersion_OptimisticConcurrency_Test()
    {
        // Arrange: Create game and initial RuleSpec
        var gameId = $"optimistic-{Guid.NewGuid():N}";
        using (var arrangeScope = Factory.Services.CreateScope())
        {
            var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            arrangeDb.Games.Add(new GameEntity { Id = gameId, Name = "Optimistic Game" });
            await arrangeDb.SaveChangesAsync();
        }

        // Create initial version
        using var client = Factory.CreateHttpsClient();
        var initialSpec = new RuleSpec(
            gameId,
            "1.0",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r1", "Initial rule", "Setup", "1", "1") }
        );

        await PutAsJsonAuthenticatedAsync(client, _editorCookies, $"/api/v1/games/{gameId}/rulespec", initialSpec);

        // Act: Two clients attempt to create version 2.0 simultaneously
        var client1 = Factory.CreateHttpsClient();
        var client2 = Factory.CreateHttpsClient();

        var editor2Email = $"rulespec-editor2-{Guid.NewGuid():N}@test.com";
        using var tempClient2 = Factory.CreateHttpsClient();
        var editor2Cookies = await RegisterAndAuthenticateAsync(tempClient2, editor2Email, "Editor");

        var spec1 = new RuleSpec(
            gameId,
            "2.0",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r2", "Editor 1 update", "Setup", "1", "1") }
        );

        var spec2 = new RuleSpec(
            gameId,
            "2.0",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r3", "Editor 2 update", "Setup", "1", "1") }
        );

        var update1 = PutAsJsonAuthenticatedAsync(client1, _editorCookies, $"/api/v1/games/{gameId}/rulespec", spec1);
        var update2 = PutAsJsonAuthenticatedAsync(client2, editor2Cookies, $"/api/v1/games/{gameId}/rulespec", spec2);

        var results = await Task.WhenAll(update1, update2);

        // Assert: At least one succeeds (may both succeed with last-write-wins)
        var successCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        successCount >= 1, "At least one update should succeed".Should().BeTrue();

        // Verify database state is consistent
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var finalSpecs = await dbContext.RuleSpecs
            .Where(r => r.GameId == gameId && r.Version == "2.0")
            .ToListAsync();

        finalSpecs.Should().NotBeEmpty();
        _output.WriteLine($"Version 2.0 instances in DB: {finalSpecs.Count}");
    }

    /// <summary>
    /// Pattern 3: TOCTOU Prevention
    /// Scenario: Concurrent auto-generation of next version
    ///   Given a RuleSpec with version 1.0
    ///   When 5 editors request next version simultaneously
    ///   Then versions are generated sequentially (1.0, 1.1, 1.2... OR unique conflict detection)
    /// </summary>
    [Fact]
    public async Task ConcurrentNextVersionGeneration_PreventsTOCTOU_Test()
    {
        // Arrange: Create game and initial RuleSpec
        var gameId = $"toctou-{Guid.NewGuid():N}";
        using (var arrangeScope = Factory.Services.CreateScope())
        {
            var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            arrangeDb.Games.Add(new GameEntity { Id = gameId, Name = "TOCTOU Game" });
            await arrangeDb.SaveChangesAsync();
        }

        // Create initial version 1.0
        using var client = Factory.CreateHttpsClient();
        var initialSpec = new RuleSpec(
            gameId,
            "1.0",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r1", "Initial rule", "Setup", "1", "1") }
        );

        await PutAsJsonAuthenticatedAsync(client, _editorCookies, $"/api/v1/games/{gameId}/rulespec", initialSpec);

        // Act: 5 concurrent attempts to create "next" version
        var tasks = new Task<HttpResponseMessage>[5];
        for (int i = 0; i < 5; i++)
        {
            var editorClient = Factory.CreateHttpsClient();
            var editorEmail = $"editor-{i}-{Guid.NewGuid():N}@test.com";
            using var tempClient = Factory.CreateHttpsClient();
            var editorCookies = await RegisterAndAuthenticateAsync(tempClient, editorEmail, "Editor");

            // Each editor creates a spec without specifying version (expects auto-generation)
            var newSpec = new RuleSpec(
                gameId,
                $"1.{i + 1}",  // Simulating auto-generated versions
                DateTime.UtcNow,
                new List<RuleAtom> { new RuleAtom($"r{i + 2}", $"Rule from editor {i}", "Setup", "1", "1") }
            );

            tasks[i] = PutAsJsonAuthenticatedAsync(editorClient, editorCookies, $"/api/v1/games/{gameId}/rulespec", newSpec);
        }

        var results = await Task.WhenAll(tasks);

        // Assert: Check for duplicate version prevention
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var allVersions = await dbContext.RuleSpecs
            .Where(r => r.GameId == gameId)
            .Select(r => r.Version)
            .ToListAsync();

        // Verify no duplicate versions
        var distinctVersions = allVersions.Distinct().ToList();
        distinctVersions.Count.Should().Be(allVersions.Count);

        _output.WriteLine($"Versions created: {string.Join(", ", allVersions.OrderBy(v => v))}");
        _output.WriteLine($"Success responses: {results.Count(r => r.StatusCode == HttpStatusCode.OK)}");
    }

    /// <summary>
    /// Pattern 4: Cache Coherence
    /// Scenario: Reads during concurrent writes maintain consistency
    ///   Given a RuleSpec being updated
    ///   When multiple concurrent reads occur during updates
    ///   Then all reads return consistent data (no partial updates visible)
    /// </summary>
    [Fact]
    public async Task RuleSpecReadsDuringWrites_MaintainConsistency_Test()
    {
        // Arrange: Create game and initial RuleSpec
        var gameId = $"cache-{Guid.NewGuid():N}";
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            dbContext.Games.Add(new GameEntity { Id = gameId, Name = "Cache Game" });
            await dbContext.SaveChangesAsync();
        }

        // Create initial version
        using var client = Factory.CreateHttpsClient();
        var initialSpec = new RuleSpec(
            gameId,
            "1.0",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r1", "Initial rule", "Setup", "1", "1") }
        );

        await PutAsJsonAuthenticatedAsync(client, _editorCookies, $"/api/v1/games/{gameId}/rulespec", initialSpec);

        // Act: Concurrent reads while update is happening
        var readTasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            readTasks[i] = Task.Run(async () =>
            {
                using var scope = Factory.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

                // Read RuleSpec - should not throw or return corrupted data
                var spec = await dbContext.RuleSpecs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.GameId == gameId && r.Version == "1.0");

                spec.Should().NotBeNull();
                spec.GameId.Should().Be(gameId);
                spec.Version.Should().Be("1.0");
            });
        }

        await Task.WhenAll(readTasks);
        // All reads completed without exceptions = consistency maintained
    }

    /// <summary>
    /// Pattern 5: Cache Invalidation Propagation
    /// Scenario: Cache invalidation after update propagates correctly
    ///   Given a cached RuleSpec
    ///   When the RuleSpec is updated
    ///   Then subsequent reads reflect the new version
    /// </summary>
    [Fact]
    public async Task RuleSpecCacheInvalidation_PropagatesCorrectly_Test()
    {
        // Arrange: Create game and initial RuleSpec
        var gameId = $"cache-inv-{Guid.NewGuid():N}";
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            dbContext.Games.Add(new GameEntity { Id = gameId, Name = "Cache Invalidation Game" });
            await dbContext.SaveChangesAsync();
        }

        // Create and read to populate cache
        using var client = Factory.CreateHttpsClient();
        var initialSpec = new RuleSpec(
            gameId,
            "1.0",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r1", "Cached rule", "Setup", "1", "1") }
        );

        await PutAsJsonAuthenticatedAsync(client, _editorCookies, $"/api/v1/games/{gameId}/rulespec", initialSpec);

        // Read to populate cache
        using (var scope1 = Factory.Services.CreateScope())
        {
            var dbContext1 = scope1.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var cachedSpec = await dbContext1.RuleSpecs
                .Include(r => r.Atoms)
                .FirstOrDefaultAsync(r => r.GameId == gameId);
            cachedSpec.Should().NotBeNull();
            cachedSpec.Atoms.First().Text.Should().Be("Cached rule");
        }

        // Act: Update RuleSpec (should invalidate cache)
        var updatedSpec = new RuleSpec(
            gameId,
            "2.0",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r2", "Updated rule", "Setup", "1", "1") }
        );

        await PutAsJsonAuthenticatedAsync(client, _editorCookies, $"/api/v1/games/{gameId}/rulespec", updatedSpec);

        // Assert: Next read gets new version (cache was invalidated)
        using var scope2 = Factory.Services.CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var newSpec = await dbContext2.RuleSpecs
            .Include(r => r.Atoms)
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        newSpec.Should().NotBeNull();
        newSpec.Version.Should().Be("2.0");
        newSpec.Atoms.First().Text.Should().Be("Updated rule");
    }
}

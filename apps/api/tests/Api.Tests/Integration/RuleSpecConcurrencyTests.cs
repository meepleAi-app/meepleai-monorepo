using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests.Integration;

/// <summary>
/// TEST-03 Phase 2: Concurrent access tests for RuleSpecService
/// Verifies thread-safety for version generation and updates
/// </summary>
public class RuleSpecConcurrencyTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecService _service;
    private readonly Mock<IAiResponseCacheService> _cacheMock;

    public RuleSpecConcurrencyTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        _cacheMock = new Mock<IAiResponseCacheService>();
        _cacheMock
            .Setup(x => x.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new RuleSpecService(_dbContext, _cacheMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Pattern 1: Concurrent Writes - Lost Update Detection
    /// Tests that concurrent version generation doesn't create duplicate versions
    /// </summary>
    [Fact]
    public async Task ConcurrentVersionGeneration_NoDuplicates_Test()
    {
        // Arrange: Create game and user
        var gameId = "chess-concurrency";
        var userId = "user-1";

        await _dbContext.Games.AddAsync(new GameEntity
        {
            Id = gameId,
            Name = "Chess",
            Description = "Chess game for concurrency testing",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.Users.AddAsync(new UserEntity
        {
            Id = userId,
            Email = "test@test.com",
            Username = "testuser",
            Role = UserRole.Editor,
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        // Act: Create 5 versions concurrently without specifying version numbers
        var tasks = Enumerable.Range(1, 5).Select(async i =>
        {
            var ruleSpec = new RuleSpec
            {
                gameId = gameId,
                version = null, // Let service generate version
                rules = new List<RuleAtom>
                {
                    new RuleAtom
                    {
                        id = $"rule-{i}",
                        text = $"Rule text {i}",
                        section = "Rules",
                        page = "1",
                        line = "1"
                    }
                }
            };

            return await _service.UpdateRuleSpecAsync(gameId, ruleSpec, userId);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: All versions are unique (no duplicates due to race condition)
        var versions = results.Select(r => r.version).ToList();
        var uniqueVersions = versions.Distinct().ToList();

        _output.WriteLine($"Generated versions: {string.Join(", ", versions)}");

        Assert.Equal(5, versions.Count);
        Assert.Equal(5, uniqueVersions.Count); // Should be 5 unique versions

        // Verify all versions are in database
        var dbVersions = await _dbContext.RuleSpecs
            .Where(r => r.GameId == gameId)
            .Select(r => r.Version)
            .ToListAsync();

        Assert.Equal(5, dbVersions.Count);
    }

    /// <summary>
    /// Pattern 2: Optimistic Concurrency - Read-Modify-Write
    /// Tests that concurrent updates with specific versions handle conflicts
    /// </summary>
    [Fact]
    public async Task ConcurrentUpdatesWithSameVersion_DetectsConflict_Test()
    {
        // Arrange: Create game and user
        var gameId = "chess-version-conflict";
        var userId = "user-1";

        await _dbContext.Games.AddAsync(new GameEntity
        {
            Id = gameId,
            Name = "Chess",
            Description = "Chess game for version conflict testing",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.Users.AddAsync(new UserEntity
        {
            Id = userId,
            Email = "test@test.com",
            Username = "testuser",
            Role = UserRole.Editor,
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        // Act: Two concurrent updates trying to create version "1.0"
        var ruleSpec1 = new RuleSpec
        {
            gameId = gameId,
            version = "1.0",
            rules = new List<RuleAtom>
            {
                new RuleAtom
                {
                    id = "rule-1",
                    text = "First update",
                    section = "Rules",
                    page = "1",
                    line = "1"
                }
            }
        };

        var ruleSpec2 = new RuleSpec
        {
            gameId = gameId,
            version = "1.0", // Same version!
            rules = new List<RuleAtom>
            {
                new RuleAtom
                {
                    id = "rule-2",
                    text = "Second update",
                    section = "Rules",
                    page = "1",
                    line = "1"
                }
            }
        };

        var task1 = Task.Run(async () => await _service.UpdateRuleSpecAsync(gameId, ruleSpec1, userId));
        var task2 = Task.Run(async () => await _service.UpdateRuleSpecAsync(gameId, ruleSpec2, userId));

        // Assert: One should succeed, one should fail with InvalidOperationException
        var exceptions = new List<Exception>();

        try
        {
            await task1;
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
        }

        try
        {
            await task2;
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
        }

        // Exactly one should have thrown InvalidOperationException
        Assert.Single(exceptions);
        Assert.IsType<InvalidOperationException>(exceptions.First());
        Assert.Contains("already exists", exceptions.First().Message);

        // Verify only one version "1.0" exists in database
        var dbVersions = await _dbContext.RuleSpecs
            .Where(r => r.GameId == gameId && r.Version == "1.0")
            .CountAsync();

        Assert.Equal(1, dbVersions);
    }

    /// <summary>
    /// Pattern 3: TOCTOU (Time-Of-Check-Time-Of-Use)
    /// Tests that version existence checks don't have race conditions
    /// </summary>
    [Fact]
    public async Task ConcurrentVersionChecks_NoTOCTOU_Test()
    {
        // Arrange: Create game and user
        var gameId = "chess-toctou";
        var userId = "user-1";

        await _dbContext.Games.AddAsync(new GameEntity
        {
            Id = gameId,
            Name = "Chess",
            Description = "Chess game for TOCTOU testing",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.Users.AddAsync(new UserEntity
        {
            Id = userId,
            Email = "test@test.com",
            Username = "testuser",
            Role = UserRole.Editor,
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        // Act: Create initial version
        var initialSpec = new RuleSpec
        {
            gameId = gameId,
            version = "1.0",
            rules = new List<RuleAtom>
            {
                new RuleAtom
                {
                    id = "rule-initial",
                    text = "Initial version",
                    section = "Rules",
                    page = "1",
                    line = "1"
                }
            }
        };

        await _service.UpdateRuleSpecAsync(gameId, initialSpec, userId);

        // Now 10 concurrent attempts to auto-generate next version
        var tasks = Enumerable.Range(1, 10).Select(async i =>
        {
            var ruleSpec = new RuleSpec
            {
                gameId = gameId,
                version = null, // Auto-generate
                rules = new List<RuleAtom>
                {
                    new RuleAtom
                    {
                        id = $"rule-{i}",
                        text = $"Rule text {i}",
                        section = "Rules",
                        page = "1",
                        line = "1"
                    }
                }
            };

            return await _service.UpdateRuleSpecAsync(gameId, ruleSpec, userId);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: All auto-generated versions are unique
        var versions = results.Select(r => r.version).ToList();
        var uniqueVersions = versions.Distinct().ToList();

        _output.WriteLine($"Auto-generated versions: {string.Join(", ", versions)}");

        Assert.Equal(10, versions.Count);
        Assert.Equal(10, uniqueVersions.Count);

        // Verify all 11 versions (1.0 + 10 auto-generated) exist in database
        var dbCount = await _dbContext.RuleSpecs
            .Where(r => r.GameId == gameId)
            .CountAsync();

        Assert.Equal(11, dbCount);
    }

    /// <summary>
    /// Pattern 4: Cache Coherence - Invalidation Propagation
    /// Tests that cache invalidation happens correctly during concurrent updates
    /// </summary>
    [Fact]
    public async Task ConcurrentUpdates_CacheInvalidationPropagates_Test()
    {
        // Arrange: Create game and user
        var gameId = "chess-cache";
        var userId = "user-1";

        await _dbContext.Games.AddAsync(new GameEntity
        {
            Id = gameId,
            Name = "Chess",
            Description = "Chess game for cache testing",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.Users.AddAsync(new UserEntity
        {
            Id = userId,
            Email = "test@test.com",
            Username = "testuser",
            Role = UserRole.Editor,
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        // Act: Create 5 versions concurrently
        var tasks = Enumerable.Range(1, 5).Select(async i =>
        {
            var ruleSpec = new RuleSpec
            {
                gameId = gameId,
                version = $"{i}.0",
                rules = new List<RuleAtom>
                {
                    new RuleAtom
                    {
                        id = $"rule-{i}",
                        text = $"Rule text {i}",
                        section = "Rules",
                        page = "1",
                        line = "1"
                    }
                }
            };

            return await _service.UpdateRuleSpecAsync(gameId, ruleSpec, userId);
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert: Cache invalidation was called exactly 5 times (once per update)
        _cacheMock.Verify(
            x => x.InvalidateGameAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Exactly(5)
        );
    }
}

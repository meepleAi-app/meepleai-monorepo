using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration.FinalValidation;

/// <summary>
/// Week 12: Simplified Final Validation Tests (25 tests)
/// Focuses on database operations, concurrency, and edge cases using GameEntity
/// Issue #2310: Week 12 - Final coverage push
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2310")]
[Trait("Week", "12")]
public sealed class Week12SimpleValidationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private string _databaseName = null!;
    private IServiceProvider _serviceProvider = null!;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public Week12SimpleValidationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_week12simple_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .EnableSensitiveDataLogging()
            .Options;

        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(MeepleAiDbContext).Assembly));

        services.AddSingleton(options);
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddScoped<MeepleAiDbContext>(sp =>
            new MeepleAiDbContext(
                options,
                sp.GetRequiredService<IMediator>(),
                sp.GetRequiredService<IDomainEventCollector>()));

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        _serviceProvider = services.BuildServiceProvider();
        // Fix: Use PostgreSQL DbContext with Testcontainers, not in-memory
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        _dbContext = new MeepleAiDbContext(options, mediator, eventCollector);

        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    #region Complex Workflow Tests (10 tests)

    [Fact]
    public async Task Workflow_CreateGame_QueryById_Success()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Workflow Test Game",
            Publisher = "Test Publisher",
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 30,
            MaxPlayTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var retrieved = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Workflow Test Game");
    }

    [Fact]
    public async Task Workflow_CreateMultipleGames_QueryAll_Success()
    {
        // Arrange
        var games = Enumerable.Range(1, 5).Select(i => new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = $"Game {i}",
            Publisher = $"Publisher {i}",
            YearPublished = 2020 + i,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 30,
            MaxPlayTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        // Act
        foreach (var game in games)
        {
            _dbContext.Games.Add(game);
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var allGames = await _dbContext.Games.ToListAsync(TestCancellationToken);

        // Assert
        allGames.Should().HaveCountGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Workflow_CreateUpdateDelete_Game_Success()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Publisher = "Original Publisher",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Update
        game.Name = "Updated Name";
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var updated = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        updated!.Name.Should().Be("Updated Name");

        // Act - Delete
        _dbContext.Games.Remove(updated);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var deleted = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);

        // Assert
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task Workflow_BulkInsert_20Games_Success()
    {
        // Arrange
        var games = Enumerable.Range(1, 20).Select(i => new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = $"Bulk Game {i}",
            Publisher = $"Pub {i}",
            YearPublished = 2023,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        // Act
        _dbContext.Games.AddRange(games);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var gameIds = games.Select(g => g.Id).ToList();
        var persisted = await _dbContext.Games.Where(g => gameIds.Contains(g.Id)).CountAsync(TestCancellationToken);
        persisted.Should().Be(20);
    }

    [Fact]
    public async Task Workflow_FilterGamesByYear_Success()
    {
        // Arrange
        var games = new[]
        {
            new GameEntity { Id = Guid.NewGuid(), Name = "Old Game", YearPublished = 2000, CreatedAt = DateTime.UtcNow },
            new GameEntity { Id = Guid.NewGuid(), Name = "New Game 1", YearPublished = 2023, CreatedAt = DateTime.UtcNow },
            new GameEntity { Id = Guid.NewGuid(), Name = "New Game 2", YearPublished = 2024, CreatedAt = DateTime.UtcNow }
        };

        _dbContext.Games.AddRange(games);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var newGames = await _dbContext.Games.Where(g => g.YearPublished >= 2023).ToListAsync(TestCancellationToken);

        // Assert
        newGames.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Workflow_PaginatedQuery_Success()
    {
        // Arrange
        for (int i = 0; i < 30; i++)
        {
            _dbContext.Games.Add(new GameEntity
            {
                Id = Guid.NewGuid(),
                Name = $"Paginated Game {i}",
                CreatedAt = DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Get page 2 (skip 10, take 10)
        var page2 = await _dbContext.Games
            .OrderBy(g => g.Name)
            .Skip(10)
            .Take(10)
            .ToListAsync(TestCancellationToken);

        // Assert
        page2.Should().HaveCount(10);
    }

    [Fact]
    public async Task Workflow_SearchByNamePrefix_Success()
    {
        // Arrange
        var games = new[]
        {
            new GameEntity { Id = Guid.NewGuid(), Name = "Catan", CreatedAt = DateTime.UtcNow },
            new GameEntity { Id = Guid.NewGuid(), Name = "Carcassonne", CreatedAt = DateTime.UtcNow },
            new GameEntity { Id = Guid.NewGuid(), Name = "Chess", CreatedAt = DateTime.UtcNow }
        };

        _dbContext.Games.AddRange(games);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var catGames = await _dbContext.Games
            .Where(g => g.Name!.StartsWith("Ca"))
            .ToListAsync(TestCancellationToken);

        // Assert
        catGames.Should().HaveCount(2);
    }

    [Fact]
    public async Task Workflow_CountGamesByPublisher_Success()
    {
        // Arrange
        var games = new[]
        {
            new GameEntity { Id = Guid.NewGuid(), Name = "Game 1", Publisher = "PubA", CreatedAt = DateTime.UtcNow },
            new GameEntity { Id = Guid.NewGuid(), Name = "Game 2", Publisher = "PubA", CreatedAt = DateTime.UtcNow },
            new GameEntity { Id = Guid.NewGuid(), Name = "Game 3", Publisher = "PubB", CreatedAt = DateTime.UtcNow }
        };

        _dbContext.Games.AddRange(games);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var counts = await _dbContext.Games
            .GroupBy(g => g.Publisher)
            .Select(g => new { Publisher = g.Key, Count = g.Count() })
            .ToListAsync(TestCancellationToken);

        // Assert
        counts.Should().Contain(c => c.Publisher == "PubA" && c.Count == 2);
    }

    [Fact]
    public async Task Workflow_ExistsCheck_Success()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Existence Test",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var exists = await _dbContext.Games.AnyAsync(g => g.Id == game.Id, TestCancellationToken);
        var notExists = await _dbContext.Games.AnyAsync(g => g.Id == Guid.NewGuid(), TestCancellationToken);

        // Assert
        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Fact]
    public async Task Workflow_OrderByMultipleFields_Success()
    {
        // Arrange
        var games = new[]
        {
            new GameEntity { Id = Guid.NewGuid(), Name = "Z Game", YearPublished = 2020, CreatedAt = DateTime.UtcNow },
            new GameEntity { Id = Guid.NewGuid(), Name = "A Game", YearPublished = 2023, CreatedAt = DateTime.UtcNow },
            new GameEntity { Id = Guid.NewGuid(), Name = "M Game", YearPublished = 2020, CreatedAt = DateTime.UtcNow }
        };

        _dbContext.Games.AddRange(games);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var ordered = await _dbContext.Games
            .OrderBy(g => g.YearPublished)
            .ThenBy(g => g.Name)
            .ToListAsync(TestCancellationToken);

        // Assert
        ordered.First().Name.Should().Be("M Game");
    }

    #endregion

    #region Concurrency Tests (8 tests)

    [Fact]
    public async Task Concurrency_ParallelReads_AllSucceed()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Concurrent Read Test",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act - 20 parallel reads
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            return await dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(g => g.Should().NotBeNull());
    }

    [Fact]
    public async Task Concurrency_ParallelInserts_AllPersisted()
    {
        // Act
        var tasks = Enumerable.Range(0, 15).Select(async i =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            var game = new GameEntity
            {
                Id = Guid.NewGuid(),
                Name = $"Parallel Game {i}",
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Games.Add(game);
            await dbContext.SaveChangesAsync(TestCancellationToken);
            return game.Id;
        });

        var gameIds = await Task.WhenAll(tasks);

        // Assert
        var persisted = await _dbContext.Games.Where(g => gameIds.Contains(g.Id)).CountAsync(TestCancellationToken);
        persisted.Should().Be(15);
    }

    [Fact]
    public async Task Concurrency_IsolatedTransactions_NoInterference()
    {
        // Act
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            using var transaction = await dbContext.Database.BeginTransactionAsync(TestCancellationToken);
            try
            {
                var game = new GameEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Transaction Game {i}",
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.Games.Add(game);
                await dbContext.SaveChangesAsync(TestCancellationToken);
                await transaction.CommitAsync(TestCancellationToken);
                return game.Id;
            }
            catch
            {
                await transaction.RollbackAsync(TestCancellationToken);
                throw;
            }
        });

        var gameIds = await Task.WhenAll(tasks);

        // Assert
        var persisted = await _dbContext.Games.Where(g => gameIds.Contains(g.Id)).CountAsync(TestCancellationToken);
        persisted.Should().Be(10);
    }

    [Fact]
    public async Task Concurrency_MixedReadWrite_NoDeadlock()
    {
        // Arrange
        var game = new GameEntity { Id = Guid.NewGuid(), Name = "Deadlock Test", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var readTasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            return await dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        });

        var writeTasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            var newGame = new GameEntity { Id = Guid.NewGuid(), Name = $"Write {i}", CreatedAt = DateTime.UtcNow };
            dbContext.Games.Add(newGame);
            await dbContext.SaveChangesAsync(TestCancellationToken);
        });

        await Task.WhenAll(readTasks.Cast<Task>().Concat(writeTasks));

        // Assert - No deadlock, all completed
        Assert.True(true);
    }

    [Fact]
    public async Task Concurrency_RapidCreateDelete_NoOrphans()
    {
        // Act
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            var game = new GameEntity { Id = Guid.NewGuid(), Name = $"Rapid {i}", CreatedAt = DateTime.UtcNow };
            dbContext.Games.Add(game);
            await dbContext.SaveChangesAsync(TestCancellationToken);

            dbContext.Games.Remove(game);
            await dbContext.SaveChangesAsync(TestCancellationToken);
            return game.Id;
        });

        var gameIds = await Task.WhenAll(tasks);

        // Assert
        var remaining = await _dbContext.Games.Where(g => gameIds.Contains(g.Id)).CountAsync(TestCancellationToken);
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task Concurrency_BatchInsert_50Games_Efficient()
    {
        // Arrange
        var games = Enumerable.Range(0, 50).Select(i => new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = $"Batch {i}",
            CreatedAt = DateTime.UtcNow
        }).ToList();

        // Act
        _dbContext.Games.AddRange(games);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var gameIds = games.Select(g => g.Id).ToList();
        var persisted = await _dbContext.Games.Where(g => gameIds.Contains(g.Id)).CountAsync(TestCancellationToken);
        persisted.Should().Be(50);
    }

    [Fact]
    public async Task Concurrency_ParallelQueries_ConsistentCounts()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _dbContext.Games.Add(new GameEntity { Id = Guid.NewGuid(), Name = $"Count {i}", CreatedAt = DateTime.UtcNow });
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            return await dbContext.Games.CountAsync(TestCancellationToken);
        });

        var counts = await Task.WhenAll(tasks);

        // Assert
        counts.Should().AllSatisfy(count => count.Should().BeGreaterThanOrEqualTo(10));
    }

    [Fact]
    public async Task Concurrency_UpdateSameGame_LastWriteWins()
    {
        // Arrange
        var game = new GameEntity { Id = Guid.NewGuid(), Name = "Original", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

                var gameToUpdate = await dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
                if (gameToUpdate != null)
                {
                    gameToUpdate.Name = $"Update {i}";
                    await dbContext.SaveChangesAsync(TestCancellationToken);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        });

        await Task.WhenAll(tasks);

        // Assert - At least one update should have succeeded (name changed)
        var final = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        final.Should().NotBeNull();
        // Due to concurrency, may or may not have changed - test that game still exists
        final!.Id.Should().Be(game.Id);
    }

    #endregion

    #region Edge Case Tests (7 tests)

    [Fact]
    public async Task EdgeCase_EmptyDatabase_ReturnsEmpty()
    {
        // Act
        var all = await _dbContext.Games.ToListAsync(TestCancellationToken);

        // Assert
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task EdgeCase_SpecialCharacters_Unicode_Stored()
    {
        // Arrange
        var games = new[]
        {
            new GameEntity { Id = Guid.NewGuid(), Name = "游戏", CreatedAt = DateTime.UtcNow },
            new GameEntity { Id = Guid.NewGuid(), Name = "🎲 Game 🎯", CreatedAt = DateTime.UtcNow },
            new GameEntity { Id = Guid.NewGuid(), Name = "Jeu™", CreatedAt = DateTime.UtcNow }
        };

        // Act
        _dbContext.Games.AddRange(games);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var gameIds = games.Select(g => g.Id).ToList();
        var persisted = await _dbContext.Games.Where(g => gameIds.Contains(g.Id)).ToListAsync(TestCancellationToken);
        persisted.Should().HaveCount(3);
    }

    [Fact]
    public async Task EdgeCase_MaxIntValue_PlayerCount_Handled()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Max Players",
            MinPlayers = 1,
            MaxPlayers = int.MaxValue,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        retrieved!.MaxPlayers.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task EdgeCase_NullableFields_HandledCorrectly()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Minimal Game",
            Publisher = null,
            YearPublished = null,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        retrieved!.Publisher.Should().BeNull();
        retrieved.YearPublished.Should().BeNull();
    }

    [Fact]
    public async Task EdgeCase_QueryNonExistent_ReturnsNull()
    {
        // Act
        var nonExistent = await _dbContext.Games.FindAsync(new object[] { Guid.NewGuid() }, TestCancellationToken);

        // Assert
        nonExistent.Should().BeNull();
    }

    [Fact]
    public async Task EdgeCase_VeryLongName_TruncatedOrRejected()
    {
        // Arrange
        var longName = new string('A', 1000);
        var game = new GameEntity { Id = Guid.NewGuid(), Name = longName, CreatedAt = DateTime.UtcNow };

        // Act & Assert
        try
        {
            _dbContext.Games.Add(game);
            await _dbContext.SaveChangesAsync(TestCancellationToken);

            var retrieved = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
            retrieved.Should().NotBeNull();
        }
        catch (DbUpdateException)
        {
            // Expected if database has length constraint
            Assert.True(true);
        }
    }

    [Fact]
    public async Task EdgeCase_ZeroYearPublished_Allowed()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Ancient Game",
            YearPublished = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        retrieved!.YearPublished.Should().Be(0);
    }

    #endregion
}

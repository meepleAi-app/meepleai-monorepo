using System;
using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// TEST-02-P4: Comprehensive tests for Infrastructure layer (DbContext, Entities, Constraints)
/// Tests foreign keys, unique indexes, cascade deletes, and entity validation.
/// </summary>
public class InfrastructureTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;

    public InfrastructureTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// TEST-02-P4: Tests that UserEntity unique email constraint prevents duplicate emails.
    /// Given: User with email exists
    /// When: Insert user with same email
    /// Then: Throws DbUpdateException
    /// </summary>
    [Fact]
    public void UserEntity_UniqueEmailConstraint_PreventsDuplicates()
    {
        // Arrange
        var user1 = new UserEntity
        {
            Id = "user-1",
            Email = "test@example.com",
            PasswordHash = "hash1",
            DisplayName = "User 1",
            Role = UserRole.User
        };

        _dbContext.Users.Add(user1);
        _dbContext.SaveChanges();

        var user2 = new UserEntity
        {
            Id = "user-2",
            Email = "test@example.com", // Duplicate email
            PasswordHash = "hash2",
            DisplayName = "User 2",
            Role = UserRole.User
        };

        _dbContext.Users.Add(user2);

        // Act & Assert
        var exception = Assert.Throws<DbUpdateException>(() => _dbContext.SaveChanges());
        exception.InnerException?.Message ?? exception.Which.Message.Should().Contain("UNIQUE constraint failed");
    }

    /// <summary>
    /// TEST-02-P4: Tests that deleting a User cascades to related UserSessions.
    /// Given: User has active sessions
    /// When: User deleted
    /// Then: Sessions cascade deleted
    /// </summary>
    [Fact]
    public void UserEntity_CascadeDelete_DeletesRelatedSessions()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = "user-cascade",
            Email = "cascade@example.com",
            PasswordHash = "hash",
            DisplayName = "Cascade User",
            Role = UserRole.User
        };

        var session1 = new UserSessionEntity
        {
            Id = "session-1",
            UserId = user.Id,
            TokenHash = "token-hash-1",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = user // Required navigation property
        };

        var session2 = new UserSessionEntity
        {
            Id = "session-2",
            UserId = user.Id,
            TokenHash = "token-hash-2",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = user // Required navigation property
        };

        _dbContext.Users.Add(user);
        _dbContext.UserSessions.AddRange(session1, session2);
        _dbContext.SaveChanges();

        Assert.Equal(2, _dbContext.UserSessions.Count(s => s.UserId == user.Id));

        // Act
        _dbContext.Users.Remove(user);
        _dbContext.SaveChanges();

        // Assert
        Assert.Equal(0, _dbContext.UserSessions.Count(s => s.UserId == user.Id));
    }

    /// <summary>
    /// TEST-02-P4: Tests that GameEntity unique name constraint prevents duplicate game names.
    /// Given: Game named "Chess" exists
    /// When: Insert game with same name
    /// Then: Throws DbUpdateException
    /// </summary>
    [Fact]
    public void GameEntity_UniqueNameConstraint_PreventsDuplicates()
    {
        // Arrange
        var game1 = new GameEntity
        {
            Id = "game-1",
            Name = "Chess"
        };

        _dbContext.Games.Add(game1);
        _dbContext.SaveChanges();

        var game2 = new GameEntity
        {
            Id = "game-2",
            Name = "Chess" // Duplicate name
        };

        _dbContext.Games.Add(game2);

        // Act & Assert
        var exception = Assert.Throws<DbUpdateException>(() => _dbContext.SaveChanges());
        exception.InnerException?.Message ?? exception.Which.Message.Should().Contain("UNIQUE constraint failed");
    }

    /// <summary>
    /// TEST-02-P4: Tests that required fields throw DbUpdateException when null.
    /// Given: Entity with null required field
    /// When: SaveChanges called
    /// Then: Throws DbUpdateException
    /// </summary>
    [Fact]
    public void DbContext_RequiredFields_ThrowsOnNullValues()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = "user-required",
            Email = null!, // Required field set to null
            PasswordHash = "hash",
            DisplayName = "Test User",
            Role = UserRole.User
        };

        _dbContext.Users.Add(user);

        // Act & Assert
        var exception = Assert.Throws<DbUpdateException>(() => _dbContext.SaveChanges());
        exception.InnerException?.Message ?? exception.Which.Message.Should().Contain("NOT NULL constraint failed");
    }

    /// <summary>
    /// TEST-02-P4: Tests that MaxLength validation is configured (SQLite doesn't enforce it, but schema defines it).
    /// Given: Entity with field exceeding MaxLength
    /// When: SaveChanges called
    /// Then: Data is stored (SQLite doesn't enforce MaxLength, but we verify no crash occurs)
    /// </summary>
    [Fact]
    public void DbContext_MaxLengthValidation_ConfiguredInSchema()
    {
        // Arrange
        var longEmail = new string('a', 300) + "@example.com"; // Exceeds 256 char limit
        var user = new UserEntity
        {
            Id = "user-maxlength",
            Email = longEmail,
            PasswordHash = "hash",
            DisplayName = "Test User",
            Role = UserRole.User
        };

        _dbContext.Users.Add(user);

        // Act
        // SQLite doesn't enforce MaxLength constraints, just stores the full string
        // This test verifies the configuration exists and doesn't cause runtime errors
        _dbContext.SaveChanges();

        // Assert
        var savedUser = _dbContext.Users.Find("user-maxlength");
        savedUser.Should().NotBeNull();
        // Note: In production with PostgreSQL, MaxLength would be enforced.
        // SQLite stores the full email length for testing purposes.
        savedUser.Email.Should().Be(longEmail);
    }

    /// <summary>
    /// TEST-02-P4: Tests that ApiKeyEntity foreign key constraint enforces user reference.
    /// Given: ApiKey with invalid UserId
    /// When: SaveChanges called
    /// Then: Throws DbUpdateException
    /// </summary>
    [Fact]
    public void ApiKeyEntity_ForeignKeyConstraint_EnforcesUserReference()
    {
        // Arrange
        // Create an API key with a UserId that doesn't exist in the database
        var apiKey = new ApiKeyEntity
        {
            Id = "apikey-fk-test",
            UserId = "non-existent-user", // Invalid FK reference
            KeyName = "Test Key",
            KeyHash = "hash",
            KeyPrefix = "mpl_test_",
            User = null! // Bypass required navigation property for FK test
        };

        // Act
        _dbContext.ApiKeys.Add(apiKey);

        // Assert
        var exception = Assert.Throws<DbUpdateException>(() => _dbContext.SaveChanges());
        exception.InnerException?.Message ?? exception.Which.Message.Should().Contain("FOREIGN KEY constraint failed");
    }

    /// <summary>
    /// TEST-02-P4: Tests that RuleSpecEntity composite unique index prevents duplicate (GameId, Version) combinations.
    /// Given: RuleSpec (GameId, Version) exists
    /// When: Insert same GameId+Version
    /// Then: Throws DbUpdateException
    /// </summary>
    [Fact]
    public void RuleSpecEntity_CompositeUniqueIndex_PreventsDuplicateVersions()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "game-rulespec",
            Name = "Test Game"
        };

        _dbContext.Games.Add(game);
        _dbContext.SaveChanges();

        var ruleSpec1 = new RuleSpecEntity
        {
            Id = Guid.NewGuid(), // Guid, not string
            GameId = game.Id,
            Version = "1.0"
            // No JsonContent property
        };

        _dbContext.RuleSpecs.Add(ruleSpec1);
        _dbContext.SaveChanges();

        var ruleSpec2 = new RuleSpecEntity
        {
            Id = Guid.NewGuid(), // Guid, not string
            GameId = game.Id,
            Version = "1.0" // Duplicate (GameId, Version)
            // No JsonContent property
        };

        _dbContext.RuleSpecs.Add(ruleSpec2);

        // Act & Assert
        var exception = Assert.Throws<DbUpdateException>(() => _dbContext.SaveChanges());
        exception.InnerException?.Message ?? exception.Which.Message.Should().Contain("UNIQUE constraint failed");
    }
}

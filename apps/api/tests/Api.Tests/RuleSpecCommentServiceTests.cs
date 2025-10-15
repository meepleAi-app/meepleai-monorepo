using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

public class RuleSpecCommentServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecCommentService _service;

    public RuleSpecCommentServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        _service = new RuleSpecCommentService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddCommentAsync_WhenValidData_CreatesComment()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "user-1",
            Email = "user1@example.com",
            DisplayName = "User One",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.RuleSpecs.Add(ruleSpec);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AddCommentAsync(
            game.Id,
            "v1",
            "atom-1",
            user.Id,
            "This is a test comment");

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal("v1", result.Version);
        Assert.Equal("atom-1", result.AtomId);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("User One", result.UserDisplayName);
        Assert.Equal("This is a test comment", result.CommentText);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
        Assert.Null(result.UpdatedAt);

        var savedComment = await _dbContext.RuleSpecComments.FirstAsync();
        Assert.Equal(result.Id, savedComment.Id);
        Assert.Equal("This is a test comment", savedComment.CommentText);
    }

    [Fact]
    public async Task AddCommentAsync_WithNullAtomId_CreatesVersionLevelComment()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "user-1",
            Email = "user1@example.com",
            DisplayName = "User One",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.RuleSpecs.Add(ruleSpec);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AddCommentAsync(
            game.Id,
            "v1",
            null,
            user.Id,
            "Version-level comment");

        // Assert
        Assert.Null(result.AtomId);
        Assert.Equal("Version-level comment", result.CommentText);
    }

    [Fact]
    public async Task AddCommentAsync_WhenVersionNotFound_Throws()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "user-1",
            Email = "user1@example.com",
            DisplayName = "User One",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddCommentAsync(game.Id, "v99", null, user.Id, "Comment"));

        Assert.Contains("RuleSpec version v99 not found", ex.Message);
    }

    [Fact]
    public async Task AddCommentAsync_WhenUserNotFound_Throws()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.RuleSpecs.Add(ruleSpec);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddCommentAsync(game.Id, "v1", null, "missing-user", "Comment"));

        Assert.Contains("User missing-user not found", ex.Message);
    }

    [Fact]
    public async Task AddCommentAsync_WhenUserHasNoDisplayName_UsesEmail()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "user-1",
            Email = "user1@example.com",
            DisplayName = null, // No display name
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.RuleSpecs.Add(ruleSpec);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AddCommentAsync(
            game.Id,
            "v1",
            null,
            user.Id,
            "Comment");

        // Assert
        Assert.Equal("user1@example.com", result.UserDisplayName);
    }

    [Fact]
    public async Task GetCommentsForVersionAsync_ReturnsCommentsOrderedByCreatedAt()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var user1 = new UserEntity
        {
            Id = "user-1",
            Email = "user1@example.com",
            DisplayName = "User One",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var user2 = new UserEntity
        {
            Id = "user-2",
            Email = "user2@example.com",
            DisplayName = "User Two",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.AddRange(user1, user2);
        _dbContext.RuleSpecs.Add(ruleSpec);
        await _dbContext.SaveChangesAsync();

        var comment1 = new RuleSpecCommentEntity
        {
            GameId = game.Id,
            Version = "v1",
            AtomId = "atom-1",
            UserId = user1.Id,
            CommentText = "First comment",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var comment2 = new RuleSpecCommentEntity
        {
            GameId = game.Id,
            Version = "v1",
            AtomId = null,
            UserId = user2.Id,
            CommentText = "Second comment",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var comment3 = new RuleSpecCommentEntity
        {
            GameId = game.Id,
            Version = "v1",
            AtomId = "atom-1",
            UserId = user1.Id,
            CommentText = "Third comment",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RuleSpecComments.AddRange(comment1, comment2, comment3);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCommentsForVersionAsync(game.Id, "v1");

        // Assert
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal("v1", result.Version);
        Assert.Equal(3, result.TotalComments);
        Assert.Collection(
            result.Comments,
            c =>
            {
                Assert.Equal("First comment", c.CommentText);
                Assert.Equal("User One", c.UserDisplayName);
                Assert.Equal("atom-1", c.AtomId);
            },
            c =>
            {
                Assert.Equal("Second comment", c.CommentText);
                Assert.Equal("User Two", c.UserDisplayName);
                Assert.Null(c.AtomId);
            },
            c =>
            {
                Assert.Equal("Third comment", c.CommentText);
                Assert.Equal("User One", c.UserDisplayName);
                Assert.Equal("atom-1", c.AtomId);
            });
    }

    [Fact]
    public async Task GetCommentsForVersionAsync_WhenNoComments_ReturnsEmptyList()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.RuleSpecs.Add(ruleSpec);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCommentsForVersionAsync(game.Id, "v1");

        // Assert
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal("v1", result.Version);
        Assert.Empty(result.Comments);
        Assert.Equal(0, result.TotalComments);
    }

    [Fact]
    public async Task UpdateCommentAsync_WhenOwner_UpdatesComment()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "user-1",
            Email = "user1@example.com",
            DisplayName = "User One",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        var comment = new RuleSpecCommentEntity
        {
            GameId = game.Id,
            Version = "v1",
            AtomId = "atom-1",
            UserId = user.Id,
            CommentText = "Original text",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.RuleSpecs.Add(ruleSpec);
        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync();

        var originalCreatedAt = comment.CreatedAt;

        // Act
        var result = await _service.UpdateCommentAsync(
            comment.Id,
            user.Id,
            "Updated text");

        // Assert
        Assert.Equal(comment.Id, result.Id);
        Assert.Equal("Updated text", result.CommentText);
        Assert.Equal(originalCreatedAt, result.CreatedAt);
        Assert.NotNull(result.UpdatedAt);
        Assert.True(result.UpdatedAt <= DateTime.UtcNow);

        var savedComment = await _dbContext.RuleSpecComments.FirstAsync();
        Assert.Equal("Updated text", savedComment.CommentText);
        Assert.NotNull(savedComment.UpdatedAt);
    }

    [Fact]
    public async Task UpdateCommentAsync_WhenCommentNotFound_Throws()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateCommentAsync(missingId, "user-1", "New text"));

        Assert.Contains($"Comment {missingId} not found", ex.Message);
    }

    [Fact]
    public async Task UpdateCommentAsync_WhenNotOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var owner = new UserEntity
        {
            Id = "owner",
            Email = "owner@example.com",
            DisplayName = "Owner",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var otherUser = new UserEntity
        {
            Id = "other",
            Email = "other@example.com",
            DisplayName = "Other",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        var comment = new RuleSpecCommentEntity
        {
            GameId = game.Id,
            Version = "v1",
            AtomId = null,
            UserId = owner.Id,
            CommentText = "Owner's comment",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.AddRange(owner, otherUser);
        _dbContext.RuleSpecs.Add(ruleSpec);
        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.UpdateCommentAsync(comment.Id, otherUser.Id, "Hijacked text"));

        Assert.Contains($"User {otherUser.Id} is not authorized", ex.Message);
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenOwner_DeletesComment()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "user-1",
            Email = "user1@example.com",
            DisplayName = "User One",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        var comment = new RuleSpecCommentEntity
        {
            GameId = game.Id,
            Version = "v1",
            AtomId = "atom-1",
            UserId = user.Id,
            CommentText = "To be deleted",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.RuleSpecs.Add(ruleSpec);
        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DeleteCommentAsync(comment.Id, user.Id, isAdmin: false);

        // Assert
        Assert.True(result);
        Assert.Empty(_dbContext.RuleSpecComments);
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenAdmin_DeletesAnyComment()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var owner = new UserEntity
        {
            Id = "owner",
            Email = "owner@example.com",
            DisplayName = "Owner",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var admin = new UserEntity
        {
            Id = "admin",
            Email = "admin@example.com",
            DisplayName = "Admin",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        var comment = new RuleSpecCommentEntity
        {
            GameId = game.Id,
            Version = "v1",
            AtomId = null,
            UserId = owner.Id,
            CommentText = "Owner's comment",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.AddRange(owner, admin);
        _dbContext.RuleSpecs.Add(ruleSpec);
        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DeleteCommentAsync(comment.Id, admin.Id, isAdmin: true);

        // Assert
        Assert.True(result);
        Assert.Empty(_dbContext.RuleSpecComments);
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenNotOwnerAndNotAdmin_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };

        var owner = new UserEntity
        {
            Id = "owner",
            Email = "owner@example.com",
            DisplayName = "Owner",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var otherUser = new UserEntity
        {
            Id = "other",
            Email = "other@example.com",
            DisplayName = "Other",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var ruleSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };

        var comment = new RuleSpecCommentEntity
        {
            GameId = game.Id,
            Version = "v1",
            AtomId = null,
            UserId = owner.Id,
            CommentText = "Protected comment",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.AddRange(owner, otherUser);
        _dbContext.RuleSpecs.Add(ruleSpec);
        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DeleteCommentAsync(comment.Id, otherUser.Id, isAdmin: false));

        Assert.Contains($"User {otherUser.Id} is not authorized", ex.Message);
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenCommentNotFound_ReturnsFalse()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act
        var result = await _service.DeleteCommentAsync(missingId, "user-1", isAdmin: false);

        // Assert
        Assert.False(result);
    }
}

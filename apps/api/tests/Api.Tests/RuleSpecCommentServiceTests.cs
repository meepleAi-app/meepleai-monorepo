using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

public class RuleSpecCommentServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecCommentService _service;

    public RuleSpecCommentServiceTests(ITestOutputHelper output)
    {
        _output = output;
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
        result.Id.Should().NotBe(Guid.Empty);
        result.GameId.Should().BeEquivalentTo(game.Id);
        result.Version.Should().BeEquivalentTo("v1");
        result.AtomId.Should().BeEquivalentTo("atom-1");
        result.UserId.Should().BeEquivalentTo(user.Id);
        result.UserDisplayName.Should().BeEquivalentTo("User One");
        result.CommentText.Should().BeEquivalentTo("This is a test comment");
        (result.CreatedAt <= DateTime.UtcNow).Should().BeTrue();
        result.UpdatedAt.Should().BeNull();

        var savedComment = await _dbContext.RuleSpecComments.FirstAsync();
        savedComment.Id.Should().Be(result.Id);
        savedComment.CommentText.Should().Be("This is a test comment");
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
        result.AtomId.Should().BeNull();
        result.CommentText.Should().BeEquivalentTo("Version-level comment");
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
        var act = async () => _service.AddCommentAsync(game.Id, "v99", null, user.Id, "Comment");
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();

        ex.Which.Message.Should().Contain("RuleSpec version v99 not found");
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
        var act = async () => _service.AddCommentAsync(game.Id, "v1", null, "missing-user", "Comment");
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();

        ex.Which.Message.Should().Contain("User missing-user not found");
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
        result.UserDisplayName.Should().BeEquivalentTo("user1@example.com");
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
        result.GameId.Should().BeEquivalentTo(game.Id);
        result.Version.Should().BeEquivalentTo("v1");
        result.TotalComments.Should().Be(3);
        Assert.Collection(
            result.Comments,
            c =>
            {
                c.CommentText.Should().Be("First comment");
                c.UserDisplayName.Should().Be("User One");
                c.AtomId.Should().Be("atom-1");
            },
            c =>
            {
                c.CommentText.Should().Be("Second comment");
                c.UserDisplayName.Should().Be("User Two");
                c.AtomId.Should().BeNull();
            },
            c =>
            {
                c.CommentText.Should().Be("Third comment");
                c.UserDisplayName.Should().Be("User One");
                c.AtomId.Should().Be("atom-1");
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
        result.GameId.Should().BeEquivalentTo(game.Id);
        result.Version.Should().BeEquivalentTo("v1");
        result.Comments.Should().BeEmpty();
        result.TotalComments.Should().Be(0);
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
        result.Id.Should().Be(comment.Id);
        result.CommentText.Should().Be("Updated text");
        result.CreatedAt.Should().Be(originalCreatedAt);
        result.UpdatedAt.Should().NotBeNull();
        (result.UpdatedAt <= DateTime.UtcNow).Should().BeTrue();

        var savedComment = await _dbContext.RuleSpecComments.FirstAsync();
        savedComment.CommentText.Should().Be("Updated text");
        savedComment.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateCommentAsync_WhenCommentNotFound_Throws()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act & Assert
        var act = async () => _service.UpdateCommentAsync(missingId, "user-1", "New text");
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();

        ex.Which.Message.Should().Contain($"Comment {missingId} not found");
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
        var act = async () => _service.UpdateCommentAsync(comment.Id, otherUser.Id, "Hijacked text");
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();

        ex.Which.Message.Should().Contain($"User {otherUser.Id} is not authorized");
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
        result.Should().BeTrue();
        _dbContext.RuleSpecComments.Should().BeEmpty();
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
        result.Should().BeTrue();
        _dbContext.RuleSpecComments.Should().BeEmpty();
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
        var act = async () => _service.DeleteCommentAsync(comment.Id, otherUser.Id, isAdmin: false);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();

        ex.Which.Message.Should().Contain($"User {otherUser.Id} is not authorized");
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenCommentNotFound_ReturnsFalse()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act
        var result = await _service.DeleteCommentAsync(missingId, "user-1", isAdmin: false);

        // Assert
        result.Should().BeFalse();
    }
}

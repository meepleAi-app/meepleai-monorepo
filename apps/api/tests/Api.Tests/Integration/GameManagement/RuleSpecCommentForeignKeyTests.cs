using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for RuleSpecComment FK constraints.
/// Tests DeleteBehavior.Restrict for User, DeleteBehavior.Cascade for Game.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection("SharedTestcontainers")]
public class RuleSpecCommentForeignKeyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_rulecomment_fk_{Guid.NewGuid():N}";

    public RuleSpecCommentForeignKeyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(_connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task DeleteUser_WithRuleSpecComments_ThrowsDbUpdateException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "commenter@test.com", DisplayName = "Commenter", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Catan" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var comment = new RuleSpecCommentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Version = "1.0",
            UserId = userId,
            CommentText = "Great rules explanation!",
            CreatedAt = DateTime.UtcNow,
            IsResolved = false,
            MentionedUserIds = new List<Guid>()
        };
        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - FK Restrict prevents user deletion
        _dbContext.Users.Remove(user);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken)
        );

        exception.Should().NotBeNull();
        exception.Message.Should().Contain("violates foreign key constraint");
    }

    [Fact]
    public async Task DeleteGame_WithRuleSpecComments_CascadesDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "user@test.com", DisplayName = "User", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Wingspan" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var commentId = Guid.NewGuid();
        var comment = new RuleSpecCommentEntity
        {
            Id = commentId,
            GameId = gameId,
            Version = "2.0",
            UserId = userId,
            CommentText = "Bird powers are confusing",
            CreatedAt = DateTime.UtcNow,
            IsResolved = false,
            MentionedUserIds = new List<Guid>()
        };
        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete game (cascades to comments)
        _dbContext.Games.Remove(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Comment deleted via cascade
        var deletedComment = await _dbContext.RuleSpecComments.FindAsync(commentId);
        deletedComment.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRuleSpecComment_LeavesUserAndGameIntact()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "user@test.com", DisplayName = "User", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Azul" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var commentId = Guid.NewGuid();
        var comment = new RuleSpecCommentEntity
        {
            Id = commentId,
            GameId = gameId,
            Version = "1.0",
            UserId = userId,
            CommentText = "Resolved typo",
            CreatedAt = DateTime.UtcNow,
            IsResolved = true,
            MentionedUserIds = new List<Guid>()
        };
        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete comment
        _dbContext.RuleSpecComments.Remove(comment);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - User and game remain
        var remainingUser = await _dbContext.Users.FindAsync(userId);
        remainingUser.Should().NotBeNull();

        var remainingGame = await _dbContext.Games.FindAsync(gameId);
        remainingGame.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteParentComment_WithReplies_ThrowsDbUpdateException()
    {
        // Arrange (self-referencing FK test)
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "user@test.com", DisplayName = "User", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Root" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var parentCommentId = Guid.NewGuid();
        var parentComment = new RuleSpecCommentEntity
        {
            Id = parentCommentId,
            GameId = gameId,
            Version = "1.0",
            UserId = userId,
            CommentText = "Parent question",
            CreatedAt = DateTime.UtcNow,
            IsResolved = false,
            MentionedUserIds = new List<Guid>()
        };
        _dbContext.RuleSpecComments.Add(parentComment);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var replyComment = new RuleSpecCommentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Version = "1.0",
            UserId = userId,
            CommentText = "Reply to parent",
            ParentCommentId = parentCommentId,
            CreatedAt = DateTime.UtcNow,
            IsResolved = false,
            MentionedUserIds = new List<Guid>()
        };
        _dbContext.RuleSpecComments.Add(replyComment);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - FK Restrict prevents parent deletion with replies
        _dbContext.RuleSpecComments.Remove(parentComment);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken)
        );

        exception.Should().NotBeNull();
        exception.Message.Should().Contain("violates foreign key constraint");
    }
}

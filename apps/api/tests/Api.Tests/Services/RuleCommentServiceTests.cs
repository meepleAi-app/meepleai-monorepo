using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Services;

/// <summary>
/// Comprehensive unit tests for RuleCommentService (EDIT-05).
///
/// Feature: Rule Specification Comments
/// As a rule specification reviewer
/// I want to add inline comments, mentions, and threaded discussions
/// So that I can collaboratively review and improve rule specifications
///
/// Test Strategy: SQLite in-memory database for fast, isolated unit tests
/// Coverage Target: 90%+ (method, branch, line)
/// </summary>
public class RuleCommentServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleCommentService _service;

    // Test data constants
    private const string TestGameId = "game-123";
    private const string TestVersion = "v1.0";
    private const string TestUserId1 = "user-1";
    private const string TestUserId2 = "user-2";
    private const string TestUserId3 = "user-3";

    public RuleCommentServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // Setup SQLite in-memory database
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        // Enable foreign keys for SQLite
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        _service = new RuleCommentService(
            _dbContext,
            NullLogger<RuleCommentService>.Instance);

        SeedTestData();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private void SeedTestData()
    {
        // Add test users with display names matching mention patterns
        var users = new[]
        {
            new UserEntity
            {
                Id = TestUserId1,
                Email = "alice@example.com",
                DisplayName = "alice",
                PasswordHash = "hash1"
            },
            new UserEntity
            {
                Id = TestUserId2,
                Email = "bob@example.com",
                DisplayName = "Bob",
                PasswordHash = "hash2"
            },
            new UserEntity
            {
                Id = TestUserId3,
                Email = "admin@example.com",
                DisplayName = "admin",
                PasswordHash = "hash3"
            }
        };

        _dbContext.Users.AddRange(users);

        // Add test game
        var game = new GameEntity
        {
            Id = TestGameId,
            Name = "Test Game"
        };

        _dbContext.Games.Add(game);
        _dbContext.SaveChanges();
    }

    #region CreateCommentAsync Tests

    [Fact]
    public async Task CreateCommentAsync_ValidInput_CreatesComment()
    {
        // Arrange
        var commentText = "This is a valid comment";

        // Act
        var result = await _service.CreateCommentAsync(
            TestGameId,
            TestVersion,
            lineNumber: 10,
            commentText,
            TestUserId1);

        // Assert
        result.Should().NotBeNull();
        result.GameId.Should().BeEquivalentTo(TestGameId);
        result.Version.Should().BeEquivalentTo(TestVersion);
        result.LineNumber.Should().Be(10);
        result.CommentText.Should().BeEquivalentTo(commentText);
        result.UserId.Should().BeEquivalentTo(TestUserId1);
        result.UserDisplayName.Should().BeEquivalentTo("alice");
        result.IsResolved.Should().BeFalse();
        result.ParentCommentId.Should().BeNull();
        result.Replies.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateCommentAsync_WithNullLineNumber_CreatesTopLevelComment()
    {
        // Arrange
        var commentText = "Top-level comment without line number";

        // Act
        var result = await _service.CreateCommentAsync(
            TestGameId,
            TestVersion,
            lineNumber: null,
            commentText,
            TestUserId1);

        // Assert
        result.Should().NotBeNull();
        result.LineNumber.Should().BeNull();
        result.CommentText.Should().BeEquivalentTo(commentText);
    }

    [Fact]
    public async Task CreateCommentAsync_WithMentions_ExtractsAndStoresMentionedUsers()
    {
        // Arrange
        var commentText = "Hey @alice and @Bob, check this out!";

        // Act
        var result = await _service.CreateCommentAsync(
            TestGameId,
            TestVersion,
            lineNumber: 5,
            commentText,
            TestUserId3);

        // Assert
        result.Should().NotBeNull();
        result.MentionedUserIds.Count.Should().Be(2);
        result.MentionedUserIds.Should().Contain(TestUserId1); // alice
        result.MentionedUserIds.Should().Contain(TestUserId2); // Bob
    }

    [Fact]
    public async Task CreateCommentAsync_WithEmailPrefixMention_ResolvesUser()
    {
        // Arrange
        var commentText = "Notify @admin about this";

        // Act
        var result = await _service.CreateCommentAsync(
            TestGameId,
            TestVersion,
            lineNumber: 1,
            commentText,
            TestUserId1);

        // Assert
        result.Should().NotBeNull();
        result.MentionedUserIds.Should().ContainSingle();
        result.MentionedUserIds.Should().Contain(TestUserId3); // admin@example.com
    }

    [Fact]
    public async Task CreateCommentAsync_EmptyText_ThrowsValidationException()
    {
        // Act & Assert
        var act = async () => await _service.CreateCommentAsync(
                TestGameId,
                TestVersion,
                lineNumber: 1,
                commentText: "",
                TestUserId1);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task CreateCommentAsync_WhitespaceText_ThrowsValidationException()
    {
        // Act & Assert
        var act = async () => await _service.CreateCommentAsync(
                TestGameId,
                TestVersion,
                lineNumber: 1,
                commentText: "   ",
                TestUserId1);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task CreateCommentAsync_TextExceedsMaxLength_ThrowsValidationException()
    {
        // Arrange
        var commentText = new string('x', 10001); // 10,001 chars

        // Act & Assert
        var act = async () => await _service.CreateCommentAsync(
                TestGameId,
                TestVersion,
                lineNumber: 1,
                commentText,
                TestUserId1);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("maximum length");
        ex.Which.Message.Should().Contain("10000");
    }

    [Fact]
    public async Task CreateCommentAsync_NegativeLineNumber_ThrowsValidationException()
    {
        // Act & Assert
        var act = async () => await _service.CreateCommentAsync(
                TestGameId,
                TestVersion,
                lineNumber: -1,
                commentText: "Invalid line",
                TestUserId1);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("positive");
    }

    [Fact]
    public async Task CreateCommentAsync_ZeroLineNumber_ThrowsValidationException()
    {
        // Act & Assert
        var act = async () => await _service.CreateCommentAsync(
                TestGameId,
                TestVersion,
                lineNumber: 0,
                commentText: "Invalid line",
                TestUserId1);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("positive");
    }

    #endregion

    #region ReplyToCommentAsync Tests

    [Fact]
    public async Task ReplyToCommentAsync_ValidParent_CreatesReply()
    {
        // Arrange
        var parent = await _service.CreateCommentAsync(
            TestGameId,
            TestVersion,
            lineNumber: 10,
            "Parent comment",
            TestUserId1);

        // Act
        var reply = await _service.ReplyToCommentAsync(
            parent.Id,
            "This is a reply",
            TestUserId2);

        // Assert
        reply.Should().NotBeNull();
        reply.ParentCommentId.Should().Be(parent.Id);
        reply.GameId.Should().Be(parent.GameId);
        reply.Version.Should().Be(parent.Version);
        reply.LineNumber.Should().Be(parent.LineNumber);
        reply.UserId.Should().Be(TestUserId2);
        reply.UserDisplayName.Should().Be("Bob");
    }

    [Fact]
    public async Task ReplyToCommentAsync_NonExistentParent_ThrowsNotFoundException()
    {
        // Arrange
        var fakeParentId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _service.ReplyToCommentAsync(
                fakeParentId,
                "Reply to nothing",
                TestUserId1);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(fakeParentId.ToString());
    }

    [Fact]
    public async Task ReplyToCommentAsync_ExceedsMaxDepth_ThrowsValidationException()
    {
        // Arrange - Create chain at max depth (5 levels)
        var level0 = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Level 0", TestUserId1);
        var level1 = await _service.ReplyToCommentAsync(level0.Id, "Level 1", TestUserId2);
        var level2 = await _service.ReplyToCommentAsync(level1.Id, "Level 2", TestUserId1);
        var level3 = await _service.ReplyToCommentAsync(level2.Id, "Level 3", TestUserId2);
        var level4 = await _service.ReplyToCommentAsync(level3.Id, "Level 4", TestUserId1);
        var level5 = await _service.ReplyToCommentAsync(level4.Id, "Level 5", TestUserId2);

        // Act & Assert - Attempt level 6 should fail
        var act = async () => await _service.ReplyToCommentAsync(level5.Id, "Level 6 - too deep", TestUserId1);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.ToLower().Should().Contain("thread depth");
        ex.Which.Message.Should().Contain("5");
    }

    [Fact]
    public async Task ReplyToCommentAsync_WithMentions_ExtractsUsers()
    {
        // Arrange
        var parent = await _service.CreateCommentAsync(
            TestGameId,
            TestVersion,
            lineNumber: 1,
            "Parent",
            TestUserId1);

        // Act
        var reply = await _service.ReplyToCommentAsync(
            parent.Id,
            "[@bob](https://github.com/bob) what do you think?",
            TestUserId3);

        // Assert
        reply.MentionedUserIds.Should().ContainSingle();
        reply.MentionedUserIds.Should().Contain(TestUserId2);
    }

    [Fact]
    public async Task ReplyToCommentAsync_EmptyText_ThrowsValidationException()
    {
        // Arrange
        var parent = await _service.CreateCommentAsync(
            TestGameId,
            TestVersion,
            lineNumber: 1,
            "Parent",
            TestUserId1);

        // Act & Assert
        var act = async () => await _service.ReplyToCommentAsync(parent.Id, "", TestUserId1);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("cannot be empty");
    }

    #endregion

    #region GetCommentsForRuleSpecAsync Tests

    [Fact]
    public async Task GetCommentsForRuleSpecAsync_NoComments_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetCommentsForRuleSpecAsync(
            TestGameId,
            TestVersion,
            includeResolved: true);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCommentsForRuleSpecAsync_MultipleComments_ReturnsAllTopLevel()
    {
        // Arrange
        var comment1 = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Comment 1", TestUserId1);
        var comment2 = await _service.CreateCommentAsync(TestGameId, TestVersion, 2, "Comment 2", TestUserId2);
        var reply = await _service.ReplyToCommentAsync(comment1.Id, "Reply to 1", TestUserId3);

        // Act
        var result = await _service.GetCommentsForRuleSpecAsync(
            TestGameId,
            TestVersion,
            includeResolved: true);

        // Assert
        result.Count.Should().Be(2); // Only top-level comments
        result.Should().Contain(c => c.Id == comment1.Id);
        result.Should().Contain(c => c.Id == comment2.Id);
        result.Should().NotContain(c => c.Id == reply.Id); // Reply not at top level
    }

    [Fact]
    public async Task GetCommentsForRuleSpecAsync_IncludesRepliesInHierarchy()
    {
        // Arrange
        var parent = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Parent", TestUserId1);
        var reply1 = await _service.ReplyToCommentAsync(parent.Id, "Reply 1", TestUserId2);
        var reply2 = await _service.ReplyToCommentAsync(parent.Id, "Reply 2", TestUserId3);

        // Act
        var result = await _service.GetCommentsForRuleSpecAsync(
            TestGameId,
            TestVersion,
            includeResolved: true);

        // Assert
        result.Should().ContainSingle();
        var parentComment = result.First();
        parentComment.Replies.Count.Should().Be(2);
        parentComment.Replies.Should().Contain(r => r.Id == reply1.Id);
        parentComment.Replies.Should().Contain(r => r.Id == reply2.Id);
    }

    [Fact]
    public async Task GetCommentsForRuleSpecAsync_IncludeResolvedFalse_FiltersResolvedComments()
    {
        // Arrange
        var comment1 = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Unresolved", TestUserId1);
        var comment2 = await _service.CreateCommentAsync(TestGameId, TestVersion, 2, "To resolve", TestUserId2);
        await _service.ResolveCommentAsync(comment2.Id, TestUserId3, resolveReplies: false);

        // Act
        var result = await _service.GetCommentsForRuleSpecAsync(
            TestGameId,
            TestVersion,
            includeResolved: false);

        // Assert
        result.Should().ContainSingle();
        result.First().Id.Should().Be(comment1.Id);
    }

    [Fact]
    public async Task GetCommentsForRuleSpecAsync_OrdersByCreatedAt()
    {
        // Arrange - Create comments with slight delays to ensure ordering
        var comment1 = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "First", TestUserId1);
        await Task.Delay(10);
        var comment2 = await _service.CreateCommentAsync(TestGameId, TestVersion, 2, "Second", TestUserId2);
        await Task.Delay(10);
        var comment3 = await _service.CreateCommentAsync(TestGameId, TestVersion, 3, "Third", TestUserId3);

        // Act
        var result = await _service.GetCommentsForRuleSpecAsync(
            TestGameId,
            TestVersion,
            includeResolved: true);

        // Assert
        result.Count.Should().Be(3);
        result[0].Id.Should().Be(comment1.Id);
        result[1].Id.Should().Be(comment2.Id);
        result[2].Id.Should().Be(comment3.Id);
    }

    #endregion

    #region GetCommentsForLineAsync Tests

    [Fact]
    public async Task GetCommentsForLineAsync_FiltersByLineNumber()
    {
        // Arrange
        var line5Comment1 = await _service.CreateCommentAsync(TestGameId, TestVersion, 5, "Line 5 - Comment 1", TestUserId1);
        var line5Comment2 = await _service.CreateCommentAsync(TestGameId, TestVersion, 5, "Line 5 - Comment 2", TestUserId2);
        var line10Comment = await _service.CreateCommentAsync(TestGameId, TestVersion, 10, "Line 10", TestUserId3);

        // Act
        var result = await _service.GetCommentsForLineAsync(TestGameId, TestVersion, 5);

        // Assert
        result.Count.Should().Be(2);
        result.Should().OnlyContain(c => c.LineNumber == 5);
        result.Should().Contain(c => c.Id == line5Comment1.Id);
        result.Should().Contain(c => c.Id == line5Comment2.Id);
    }

    [Fact]
    public async Task GetCommentsForLineAsync_NoCommentsForLine_ReturnsEmptyList()
    {
        // Arrange
        await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Line 1", TestUserId1);

        // Act
        var result = await _service.GetCommentsForLineAsync(TestGameId, TestVersion, 99);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCommentsForLineAsync_NegativeLineNumber_ThrowsValidationException()
    {
        // Act & Assert
        var act = async () => await _service.GetCommentsForLineAsync(TestGameId, TestVersion, -1);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("positive");
    }

    [Fact]
    public async Task GetCommentsForLineAsync_OnlyReturnsTopLevelComments()
    {
        // Arrange
        var parent = await _service.CreateCommentAsync(TestGameId, TestVersion, 5, "Parent on line 5", TestUserId1);
        var reply = await _service.ReplyToCommentAsync(parent.Id, "Reply", TestUserId2);

        // Act
        var result = await _service.GetCommentsForLineAsync(TestGameId, TestVersion, 5);

        // Assert
        result.Should().ContainSingle();
        result.First().Id.Should().Be(parent.Id);
        result.First().Replies.Should().ContainSingle();
    }

    #endregion

    #region ResolveCommentAsync Tests

    [Fact]
    public async Task ResolveCommentAsync_ValidComment_ResolvesSuccessfully()
    {
        // Arrange
        var comment = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "To resolve", TestUserId1);

        // Act
        var result = await _service.ResolveCommentAsync(comment.Id, TestUserId2, resolveReplies: false);

        // Assert
        result.IsResolved.Should().BeTrue();
        result.ResolvedByUserId.Should().BeEquivalentTo(TestUserId2);
        result.ResolvedByDisplayName.Should().BeEquivalentTo("Bob");
        result.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveCommentAsync_NonExistentComment_ThrowsNotFoundException()
    {
        // Arrange
        var fakeId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _service.ResolveCommentAsync(fakeId, TestUserId1, resolveReplies: false);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(fakeId.ToString());
    }

    [Fact]
    public async Task ResolveCommentAsync_WithResolveRepliesTrue_ResolvesAllChildren()
    {
        // Arrange
        var parent = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Parent", TestUserId1);
        var child1 = await _service.ReplyToCommentAsync(parent.Id, "Child 1", TestUserId2);
        var child2 = await _service.ReplyToCommentAsync(parent.Id, "Child 2", TestUserId3);
        var grandchild = await _service.ReplyToCommentAsync(child1.Id, "Grandchild", TestUserId1);

        // Act
        await _service.ResolveCommentAsync(parent.Id, TestUserId2, resolveReplies: true);

        // Assert - Fetch fresh from database to see updated status
        var comments = await _service.GetCommentsForRuleSpecAsync(TestGameId, TestVersion, includeResolved: true);
        var parentComment = comments.First(c => c.Id == parent.Id);

        parentComment.IsResolved.Should().BeTrue();
        parentComment.Replies.Count.Should().Be(2);
        parentComment.Replies.Should().OnlyContain(reply => reply.IsResolved);

        // Verify grandchild is also resolved by checking database directly
        var grandchildEntity = await _dbContext.RuleSpecComments.FindAsync(grandchild.Id);
        grandchildEntity.Should().NotBeNull();
        grandchildEntity.IsResolved.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveCommentAsync_WithResolveRepliesFalse_OnlyResolvesParent()
    {
        // Arrange
        var parent = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Parent", TestUserId1);
        var child = await _service.ReplyToCommentAsync(parent.Id, "Child", TestUserId2);

        // Act
        var result = await _service.ResolveCommentAsync(parent.Id, TestUserId3, resolveReplies: false);

        // Assert
        result.IsResolved.Should().BeTrue();

        // Verify child is NOT resolved
        var comments = await _service.GetCommentsForRuleSpecAsync(TestGameId, TestVersion, includeResolved: true);
        var parentWithReplies = comments.First(c => c.Id == parent.Id);
        parentWithReplies.Replies.First().IsResolved.Should().BeFalse();
    }

    #endregion

    #region UnresolveCommentAsync Tests

    [Fact]
    public async Task UnresolveCommentAsync_ResolvedComment_UnresolvesSuccessfully()
    {
        // Arrange
        var comment = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Resolved", TestUserId1);
        await _service.ResolveCommentAsync(comment.Id, TestUserId2, resolveReplies: false);

        // Act
        var result = await _service.UnresolveCommentAsync(comment.Id, unresolveParent: false);

        // Assert
        result.IsResolved.Should().BeFalse();
        result.ResolvedByUserId.Should().BeNull();
        result.ResolvedByDisplayName.Should().BeNull();
        result.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task UnresolveCommentAsync_NonExistentComment_ThrowsNotFoundException()
    {
        // Arrange
        var fakeId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _service.UnresolveCommentAsync(fakeId, unresolveParent: false);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(fakeId.ToString());
    }

    [Fact]
    public async Task UnresolveCommentAsync_WithUnresolveParentTrue_UnresolvesParent()
    {
        // Arrange
        var parent = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Parent", TestUserId1);
        var child = await _service.ReplyToCommentAsync(parent.Id, "Child", TestUserId2);
        await _service.ResolveCommentAsync(parent.Id, TestUserId3, resolveReplies: true);

        // Act - Unresolve child and parent
        await _service.UnresolveCommentAsync(child.Id, unresolveParent: true);

        // Assert - Verify both are unresolved
        var comments = await _service.GetCommentsForRuleSpecAsync(TestGameId, TestVersion, includeResolved: true);
        var parentComment = comments.First(c => c.Id == parent.Id);
        var childComment = parentComment.Replies.First(r => r.Id == child.Id);

        parentComment.IsResolved.Should().BeFalse();
        childComment.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task UnresolveCommentAsync_WithUnresolveParentFalse_OnlyUnresolvesChild()
    {
        // Arrange
        var parent = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Parent", TestUserId1);
        var child = await _service.ReplyToCommentAsync(parent.Id, "Child", TestUserId2);
        await _service.ResolveCommentAsync(parent.Id, TestUserId3, resolveReplies: true);

        // Act - Unresolve child only
        await _service.UnresolveCommentAsync(child.Id, unresolveParent: false);

        // Assert
        var comments = await _service.GetCommentsForRuleSpecAsync(TestGameId, TestVersion, includeResolved: true);
        var parentComment = comments.First(c => c.Id == parent.Id);
        var childComment = parentComment.Replies.First(r => r.Id == child.Id);

        parentComment.IsResolved.Should().BeTrue(); // Parent still resolved
        childComment.IsResolved.Should().BeFalse(); // Child unresolved
    }

    [Fact]
    public async Task UnresolveCommentAsync_TopLevelComment_NoParentToUnresolve()
    {
        // Arrange
        var comment = await _service.CreateCommentAsync(TestGameId, TestVersion, 1, "Top-level", TestUserId1);
        await _service.ResolveCommentAsync(comment.Id, TestUserId2, resolveReplies: false);

        // Act - Should not throw even though unresolveParent=true
        var result = await _service.UnresolveCommentAsync(comment.Id, unresolveParent: true);

        // Assert
        result.IsResolved.Should().BeFalse();
    }

    #endregion

    #region ExtractMentionedUsersAsync Tests

    [Fact]
    public async Task ExtractMentionedUsersAsync_NoMentions_ReturnsEmptyList()
    {
        // Act
        var result = await _service.ExtractMentionedUsersAsync("This text has no mentions");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractMentionedUsersAsync_EmptyString_ReturnsEmptyList()
    {
        // Act
        var result = await _service.ExtractMentionedUsersAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractMentionedUsersAsync_SingleMention_ReturnsUserId()
    {
        // Act
        var result = await _service.ExtractMentionedUsersAsync("Hey @alice, check this!");

        // Assert
        result.Should().ContainSingle();
        result.Should().Contain(TestUserId1); // alice
    }

    [Fact]
    public async Task ExtractMentionedUsersAsync_MultipleMentions_ReturnsAllUserIds()
    {
        // Act
        var result = await _service.ExtractMentionedUsersAsync("@alice and @Bob, please review with @admin");

        // Assert
        result.Count.Should().Be(3);
        result.Should().Contain(TestUserId1); // alice
        result.Should().Contain(TestUserId2); // Bob
        result.Should().Contain(TestUserId3); // admin
    }

    [Fact]
    public async Task ExtractMentionedUsersAsync_CaseInsensitive_MatchesDisplayName()
    {
        // Act - Mixed case mentions
        var result = await _service.ExtractMentionedUsersAsync("@ALICE @bob @Admin");

        // Assert
        result.Count.Should().Be(3);
        result.Should().Contain(TestUserId1);
        result.Should().Contain(TestUserId2);
        result.Should().Contain(TestUserId3);
    }

    [Fact]
    public async Task ExtractMentionedUsersAsync_EmailPrefix_MatchesUser()
    {
        // Act - Mention by email prefix
        var result = await _service.ExtractMentionedUsersAsync("Contact @admin about this");

        // Assert
        result.Should().ContainSingle();
        result.Should().Contain(TestUserId3); // admin@example.com
    }

    [Fact]
    public async Task ExtractMentionedUsersAsync_DuplicateMentions_Deduplicated()
    {
        // Act
        var result = await _service.ExtractMentionedUsersAsync("@alice @alice @ALICE");

        // Assert
        result.Should().ContainSingle();
        result.Should().Contain(TestUserId1);
    }

    [Fact]
    public async Task ExtractMentionedUsersAsync_InvalidUsername_SkipsUnknownUsers()
    {
        // Act
        var result = await _service.ExtractMentionedUsersAsync("@alice @unknownuser @Bob");

        // Assert
        result.Count.Should().Be(2); // Only alice and Bob
        result.Should().Contain(TestUserId1);
        result.Should().Contain(TestUserId2);
    }

    [Fact]
    public async Task ExtractMentionedUsersAsync_SpecialCharacters_OnlyMatchesWordCharacters()
    {
        // Act - @ followed by non-word chars shouldn't match
        var result = await _service.ExtractMentionedUsersAsync("@alice is valid but @alice! @alice? are not separate");

        // Assert
        result.Should().ContainSingle();
        result.Should().Contain(TestUserId1);
    }

    #endregion
}

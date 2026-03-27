using Api.Tests.Infrastructure;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Comprehensive integration tests for ReplyToRuleComment workflow (Issue #1691).
/// Tests the complete comment reply pipeline using Testcontainers for real infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Create valid replies
/// 2. Thread Depth Validation: Maximum depth enforcement
/// 3. Parent Not Found: Invalid parent handling
/// 4. Nested Replies: Multi-level thread creation
/// 5. Circular Reference Protection: Prevent infinite loops
/// 6. Context Inheritance: Verify game/version inheritance
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for ReplyToRuleCommentCommandHandler
/// Execution Time Target: <60s
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Issue", "2031")]
[Trait("Category", TestCategories.Integration)]
public sealed class ReplyToRuleCommentIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private Guid _testUserId;
    private Guid _otherUserId;
    private Guid _testGameId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ReplyToRuleCommentIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        _databaseName = $"test_replycomment_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Register handler explicitly
        services.AddScoped<ReplyToRuleCommentCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await EnsureCreatedWithRetry(_dbContext);

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        // Issue #2031: Use SharedTestcontainersFixture for cleanup
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task SeedTestDataAsync()
    {
        // Seed test user
        _testUserId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = _testUserId,
            Email = "test@meepleai.dev",
            DisplayName = "TestUser",
            Role = "Editor",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);

        // Seed other user
        _otherUserId = Guid.NewGuid();
        var otherUser = new UserEntity
        {
            Id = _otherUserId,
            Email = "other@meepleai.dev",
            DisplayName = "OtherUser",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(otherUser);

        // Seed game
        _testGameId = Guid.NewGuid();
        var game = new GameEntity
        {
            Id = _testGameId,
            Name = "Test Game for Replies",
            Publisher = "Test Publisher",
            YearPublished = 2024,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private static async Task EnsureCreatedWithRetry(MeepleAiDbContext context)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await context.Database.MigrateAsync(TestCancellationToken);
                return;
            }
            catch (Npgsql.NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    private async Task<Guid> CreateTestCommentAsync(Guid userId, string text = "Test comment", Guid? parentId = null)
    {
        var comment = new RuleSpecCommentEntity
        {
            Id = Guid.NewGuid(),
            GameId = _testGameId,
            Version = "1.0.0",
            CommentText = text,
            UserId = userId,
            ParentCommentId = parentId,
            LineNumber = 42,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return comment.Id;
    }

    private async Task ResetDatabaseAsync()
    {
        _dbContext!.RuleSpecComments.RemoveRange(_dbContext.RuleSpecComments);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }
    [Fact]
    public async Task ReplyToComment_WithValidData_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var parentCommentId = await CreateTestCommentAsync(_testUserId, "Parent comment");
        var handler = _serviceProvider!.GetRequiredService<ReplyToRuleCommentCommandHandler>();
        var command = new ReplyToRuleCommentCommand(
            parentCommentId,
            "This is a reply",
            _otherUserId
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.CommentText.Should().Be("This is a reply");
        result.ParentCommentId.Should().Be(parentCommentId);
        result.UserId.Should().Be(_otherUserId.ToString());

        // Verify database state
        var reply = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == result.Id, TestCancellationToken);
        reply.Should().NotBeNull();
        reply!.ParentCommentId.Should().Be(parentCommentId);
    }

    [Fact]
    public async Task ReplyToComment_InheritsGameAndVersion_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var parentCommentId = await CreateTestCommentAsync(_testUserId, "Parent comment");
        var handler = _serviceProvider!.GetRequiredService<ReplyToRuleCommentCommandHandler>();
        var command = new ReplyToRuleCommentCommand(
            parentCommentId,
            "Reply inherits context",
            _otherUserId
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.GameId.Should().Be(_testGameId.ToString());
        result.Version.Should().Be("1.0.0");
        result.LineNumber.Should().Be(42); // Inherited from parent
    }
    [Fact]
    public async Task ReplyToComment_AtMaxDepth_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create a chain of 5 comments (max depth)
        var level0 = await CreateTestCommentAsync(_testUserId, "Level 0");
        var level1 = await CreateTestCommentAsync(_otherUserId, "Level 1", level0);
        var level2 = await CreateTestCommentAsync(_testUserId, "Level 2", level1);
        var level3 = await CreateTestCommentAsync(_otherUserId, "Level 3", level2);
        var level4 = await CreateTestCommentAsync(_testUserId, "Level 4", level3);

        var handler = _serviceProvider!.GetRequiredService<ReplyToRuleCommentCommandHandler>();
        var command = new ReplyToRuleCommentCommand(
            level4,
            "This should fail - too deep",
            _otherUserId
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Maximum thread depth*");
    }

    [Fact]
    public async Task ReplyToComment_JustUnderMaxDepth_Success()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create a chain of 4 comments (under max depth of 5)
        var level0 = await CreateTestCommentAsync(_testUserId, "Level 0");
        var level1 = await CreateTestCommentAsync(_otherUserId, "Level 1", level0);
        var level2 = await CreateTestCommentAsync(_testUserId, "Level 2", level1);
        var level3 = await CreateTestCommentAsync(_otherUserId, "Level 3", level2);

        var handler = _serviceProvider!.GetRequiredService<ReplyToRuleCommentCommandHandler>();
        var command = new ReplyToRuleCommentCommand(
            level3,
            "This should succeed - just under max",
            _testUserId
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ParentCommentId.Should().Be(level3);
    }
    [Fact]
    public async Task ReplyToNonExistentComment_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();
        var handler = _serviceProvider!.GetRequiredService<ReplyToRuleCommentCommandHandler>();
        var command = new ReplyToRuleCommentCommand(
            nonExistentId,
            "Reply to non-existent",
            _testUserId
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
    [Fact]
    public async Task CreateMultipleReplies_ToSameParent_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var parentCommentId = await CreateTestCommentAsync(_testUserId, "Parent with multiple replies");
        var handler = _serviceProvider!.GetRequiredService<ReplyToRuleCommentCommandHandler>();

        var command1 = new ReplyToRuleCommentCommand(
            parentCommentId,
            "First reply",
            _otherUserId
        );

        var command2 = new ReplyToRuleCommentCommand(
            parentCommentId,
            "Second reply",
            _testUserId
        );

        // Act
        var result1 = await handler.Handle(command1, TestCancellationToken);
        var result2 = await handler.Handle(command2, TestCancellationToken);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.ParentCommentId.Should().Be(parentCommentId);
        result2.ParentCommentId.Should().Be(parentCommentId);

        // Verify database has both replies
        var replies = await _dbContext!.RuleSpecComments
            .Where(c => c.ParentCommentId == parentCommentId)
            .ToListAsync(TestContext.Current.CancellationToken);
        replies.Should().HaveCount(2);
    }
    [Fact]
    public async Task ReplyWithEmptyText_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var parentCommentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<ReplyToRuleCommentCommandHandler>();
        var command = new ReplyToRuleCommentCommand(
            parentCommentId,
            "",
            _otherUserId
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public async Task ReplyExceedingMaxLength_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var parentCommentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<ReplyToRuleCommentCommandHandler>();
        var tooLongText = new string('a', 10001);
        var command = new ReplyToRuleCommentCommand(
            parentCommentId,
            tooLongText,
            _otherUserId
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum length*");
    }
    [Fact]
    public async Task ReplyWithMention_ExtractsMentionedUser()
    {
        // Arrange
        await ResetDatabaseAsync();
        var parentCommentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<ReplyToRuleCommentCommandHandler>();
        var command = new ReplyToRuleCommentCommand(
            parentCommentId,
            "Hey @TestUser, what do you think?",
            _otherUserId
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.MentionedUserIds.Should().Contain(_testUserId.ToString());
    }
}

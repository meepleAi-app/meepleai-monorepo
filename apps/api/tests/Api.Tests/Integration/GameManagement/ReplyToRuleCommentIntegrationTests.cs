using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

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
/// </summary>
[Collection("ReplyToRuleCommentIntegration")]
public sealed class ReplyToRuleCommentIntegrationTests : IAsyncLifetime
{
    #region Test Infrastructure

    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private Guid _testUserId;
    private Guid _otherUserId;
    private Guid _testGameId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // Start PostgreSQL container
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "reply_comment_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);

        // Setup services
        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={postgresPort};Database=reply_comment_test;Username=postgres;Password=postgres;";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register domain event infrastructure
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Register MediatR
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(ReplyToRuleCommentCommandHandler).Assembly));

        // Register TimeProvider
        services.AddSingleton(TimeProvider.System);

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
        _dbContext?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync(TestCancellationToken);
            await _postgresContainer.DisposeAsync();
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
                await context.Database.EnsureCreatedAsync(TestCancellationToken);
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

    #endregion

    #region 1. Happy Path Tests

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

    #endregion

    #region 2. Thread Depth Validation Tests

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

    #endregion

    #region 3. Parent Not Found Tests

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

    #endregion

    #region 4. Nested Replies Tests

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

    #endregion

    #region 5. Validation Error Tests

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

    #endregion

    #region 6. @Mention Extraction Tests

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

    #endregion
}


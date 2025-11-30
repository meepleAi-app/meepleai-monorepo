using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Comprehensive integration tests for DeleteRuleComment workflow (Issue #1691).
/// Tests the complete comment deletion pipeline using Testcontainers for real infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Delete own comment successfully
/// 2. Authorization Errors: Non-owner deletion attempts
/// 3. Admin Override: Admin can delete any comment
/// 4. Not Found: Delete non-existent comment
/// 5. Cascade Delete: Delete comment with replies
/// 6. Concurrent Operations: Race condition handling
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for DeleteRuleCommentCommandHandler
/// Execution Time Target: <60s
/// </summary>
[Collection("DeleteRuleCommentIntegration")]
public sealed class DeleteRuleCommentIntegrationTests : IAsyncLifetime
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
            .WithEnvironment("POSTGRES_DB", "delete_comment_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);

        // Setup services
        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={postgresPort};Database=delete_comment_test;Username=postgres;Password=postgres;";

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
            config.RegisterServicesFromAssembly(typeof(DeleteRuleCommentCommandHandler).Assembly));

        // Register handler explicitly
        services.AddScoped<DeleteRuleCommentCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }

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
            Name = "Test Game for Delete",
            Publisher = "Test Publisher",
            YearPublished = 2024,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<Guid> CreateTestCommentAsync(Guid userId, string text = "Test comment")
    {
        var comment = new RuleSpecCommentEntity
        {
            Id = Guid.NewGuid(),
            GameId = _testGameId,
            Version = "1.0.0",
            CommentText = text,
            UserId = userId,
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
    public async Task DeleteOwnComment_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<DeleteRuleCommentCommandHandler>();
        var command = new DeleteRuleCommentCommand(
            commentId,
            _testUserId,
            false
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeTrue();

        // Verify comment is deleted
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId, CancellationToken.None);
        comment.Should().BeNull();
    }

    #endregion

    #region 2. Authorization Error Tests

    [Fact]
    public async Task DeleteOtherUserComment_AsNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<DeleteRuleCommentCommandHandler>();
        var command = new DeleteRuleCommentCommand(
            commentId,
            _otherUserId, // Different user
            false
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not authorized*");

        // Verify comment still exists
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId, TestCancellationToken);
        comment.Should().NotBeNull();
    }

    #endregion

    #region 3. Admin Override Tests

    [Fact]
    public async Task DeleteOtherUserComment_AsAdmin_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<DeleteRuleCommentCommandHandler>();
        var command = new DeleteRuleCommentCommand(
            commentId,
            _otherUserId, // Different user
            true // But admin
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeTrue();

        // Verify comment is deleted
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId, CancellationToken.None);
        comment.Should().BeNull();
    }

    #endregion

    #region 4. Not Found Tests

    [Fact]
    public async Task DeleteNonExistentComment_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();
        var handler = _serviceProvider!.GetRequiredService<DeleteRuleCommentCommandHandler>();
        var command = new DeleteRuleCommentCommand(
            nonExistentId,
            _testUserId,
            false
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region 5. Cascade Delete Tests

    [Fact]
    public async Task DeleteCommentWithReplies_CascadeDeletes()
    {
        // Arrange
        await ResetDatabaseAsync();
        var parentCommentId = await CreateTestCommentAsync(_testUserId, "Parent comment");

        // Create reply
        var reply = new RuleSpecCommentEntity
        {
            Id = Guid.NewGuid(),
            GameId = _testGameId,
            Version = "1.0.0",
            CommentText = "Reply comment",
            UserId = _otherUserId,
            ParentCommentId = parentCommentId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.RuleSpecComments.Add(reply);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var handler = _serviceProvider!.GetRequiredService<DeleteRuleCommentCommandHandler>();
        var command = new DeleteRuleCommentCommand(
            parentCommentId,
            _testUserId,
            false
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeTrue();

        // Verify parent is deleted
        var parentComment = await _dbContext.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == parentCommentId, TestCancellationToken);
        parentComment.Should().BeNull();

        // Verify reply is also deleted (cascade)
        var replyComment = await _dbContext.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == reply.Id, TestCancellationToken);
        replyComment.Should().BeNull("replies should be cascade deleted");
    }

    #endregion

    #region 6. Concurrent Operations Tests

    [Fact]
    public async Task ConcurrentDeletion_HandlesRaceConditionGracefully()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);

        // Create two independent handlers with separate DbContexts
        var handler1 = CreateIndependentHandler();
        var handler2 = CreateIndependentHandler();

        var command = new DeleteRuleCommentCommand(
            commentId,
            _testUserId,
            false
        );

        // Act
        var task1 = Task.Run(async () =>
        {
            try
            {
                return await handler1.Handle(command, TestCancellationToken);
            }
            catch (InvalidOperationException)
            {
                return false; // Comment already deleted
            }
        });

        var task2 = Task.Run(async () =>
        {
            try
            {
                return await handler2.Handle(command, TestCancellationToken);
            }
            catch (InvalidOperationException)
            {
                return false; // Comment already deleted
            }
        });

        var results = await Task.WhenAll(task1, task2);

        // Assert
        var successCount = results.Count(r => r);

        // At least one should succeed or get "not found"
        successCount.Should().BeGreaterThanOrEqualTo(1, "at least one deletion should succeed");

        // Verify final database state
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId, TestCancellationToken);
        comment.Should().BeNull("comment should be deleted");
    }

    private DeleteRuleCommentCommandHandler CreateIndependentHandler()
    {
        var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeleteRuleCommentCommandHandler>>();

        return new DeleteRuleCommentCommandHandler(dbContext, logger);
    }

    #endregion
}


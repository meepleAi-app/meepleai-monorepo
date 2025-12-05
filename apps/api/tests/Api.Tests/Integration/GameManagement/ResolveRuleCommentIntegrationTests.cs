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
/// Comprehensive integration tests for ResolveRuleComment workflow (Issue #1691).
/// Tests the complete comment resolution pipeline using Testcontainers for real infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Resolve comment successfully
/// 2. Authorization Errors: Non-owner resolution attempts
/// 3. Admin Override: Admin can resolve any comment
/// 4. Recursive Resolution: Resolve with replies
/// 5. Nested Replies Handling: Multi-level resolution
/// 6. Already Resolved: Re-resolution handling
/// 7. Circular Reference Protection: Prevent infinite loops
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for ResolveRuleCommentCommandHandler
/// Execution Time Target: <60s
/// </summary>
[Collection("ResolveRuleCommentIntegration")]
public sealed class ResolveRuleCommentIntegrationTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private Guid _testUserId;
    private Guid _otherUserId;
    private Guid _testGameId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var externalConn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNSTRING");
        string connectionString;

        if (!string.IsNullOrWhiteSpace(externalConn))
        {
            var builder = new NpgsqlConnectionStringBuilder(externalConn)
            {
                Database = "resolve_comment_test",
                SslMode = SslMode.Disable,
                KeepAlive = 30,
                Pooling = false
            };
            connectionString = builder.ConnectionString;
        }
        else
        {
            // Start PostgreSQL container
            _postgresContainer = new ContainerBuilder()
                .WithImage("postgres:16-alpine")
                .WithEnvironment("POSTGRES_USER", "postgres")
                .WithEnvironment("POSTGRES_PASSWORD", "postgres")
                .WithEnvironment("POSTGRES_DB", "resolve_comment_test")
                .WithPortBinding(5432, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
                .Build();

            await _postgresContainer.StartAsync(TestCancellationToken);

            var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
            connectionString = $"Host=localhost;Port={postgresPort};Database=resolve_comment_test;Username=postgres;Password=postgres;Ssl Mode=Disable;Trust Server Certificate=true;KeepAlive=30;Pooling=false;";
        }

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
            config.RegisterServicesFromAssembly(typeof(ResolveRuleCommentCommandHandler).Assembly));

        // Register TimeProvider
        services.AddSingleton(TimeProvider.System);

        // Register handler explicitly
        services.AddScoped<ResolveRuleCommentCommandHandler>();

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
            Name = "Test Game for Resolution",
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
            catch (Exception ex) when ((ex is Npgsql.NpgsqlException or InvalidOperationException) && attempt < maxAttempts)
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
    public async Task ResolveOwnComment_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<ResolveRuleCommentCommandHandler>();
        var command = new ResolveRuleCommentCommand(
            commentId,
            _testUserId,
            false,
            false
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeTrue();
        result.ResolvedByUserId.Should().Be(_testUserId.ToString());
        result.ResolvedAt.Should().NotBeNull();

        // Verify database state
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId, TestCancellationToken);
        comment.Should().NotBeNull();
        comment!.IsResolved.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveComment_WithoutReplies_OnlyResolvesComment()
    {
        // Arrange
        await ResetDatabaseAsync();
        var parentId = await CreateTestCommentAsync(_testUserId, "Parent");
        var replyId = await CreateTestCommentAsync(_otherUserId, "Reply", parentId);

        var handler = _serviceProvider!.GetRequiredService<ResolveRuleCommentCommandHandler>();
        var command = new ResolveRuleCommentCommand(
            parentId,
            _testUserId,
            false,
            false
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeTrue();

        // Verify reply is NOT resolved
        var reply = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == replyId, TestCancellationToken);
        reply.Should().NotBeNull();
        reply!.IsResolved.Should().BeFalse("replies should not be resolved");
    }
    [Fact]
    public async Task ResolveOtherUserComment_AsNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<ResolveRuleCommentCommandHandler>();
        var command = new ResolveRuleCommentCommand(
            commentId,
            _otherUserId, // Different user
            false,
            false
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not authorized*");

        // Verify comment is still unresolved
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId, TestCancellationToken);
        comment.Should().NotBeNull();
        comment!.IsResolved.Should().BeFalse();
    }
    [Fact]
    public async Task ResolveOtherUserComment_AsAdmin_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<ResolveRuleCommentCommandHandler>();
        var command = new ResolveRuleCommentCommand(
            commentId,
            _otherUserId, // Different user
            true, // But admin
            false
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeTrue();
        result.ResolvedByUserId.Should().Be(_otherUserId.ToString());
    }
    [Fact]
    public async Task ResolveComment_WithReplies_ResolvesAll()
    {
        // Arrange
        await ResetDatabaseAsync();
        var parentId = await CreateTestCommentAsync(_testUserId, "Parent");
        var reply1Id = await CreateTestCommentAsync(_otherUserId, "Reply 1", parentId);
        var reply2Id = await CreateTestCommentAsync(_testUserId, "Reply 2", parentId);

        var handler = _serviceProvider!.GetRequiredService<ResolveRuleCommentCommandHandler>();
        var command = new ResolveRuleCommentCommand(
            parentId,
            _testUserId,
            false,
            true
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeTrue();

        // Verify all replies are resolved
        var reply1 = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == reply1Id, TestCancellationToken);
        var reply2 = await _dbContext.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == reply2Id, TestCancellationToken);

        reply1.Should().NotBeNull();
        reply1!.IsResolved.Should().BeTrue("reply 1 should be resolved");
        reply2.Should().NotBeNull();
        reply2!.IsResolved.Should().BeTrue("reply 2 should be resolved");
    }
    [Fact]
    public async Task ResolveComment_WithNestedReplies_ResolvesAllLevels()
    {
        // Arrange
        await ResetDatabaseAsync();
        var level0 = await CreateTestCommentAsync(_testUserId, "Level 0");
        var level1 = await CreateTestCommentAsync(_otherUserId, "Level 1", level0);
        var level2 = await CreateTestCommentAsync(_testUserId, "Level 2", level1);

        var handler = _serviceProvider!.GetRequiredService<ResolveRuleCommentCommandHandler>();
        var command = new ResolveRuleCommentCommand(
            level0,
            _testUserId,
            false,
            true
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeTrue();

        // Verify all levels are resolved
        var l0 = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == level0, TestCancellationToken);
        var l1 = await _dbContext.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == level1, TestCancellationToken);
        var l2 = await _dbContext.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == level2, TestCancellationToken);

        l0.Should().NotBeNull();
        l0!.IsResolved.Should().BeTrue();
        l1.Should().NotBeNull();
        l1!.IsResolved.Should().BeTrue();
        l2.Should().NotBeNull();
        l2!.IsResolved.Should().BeTrue();
    }
    [Fact]
    public async Task ResolveAlreadyResolvedComment_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);

        // First resolution
        var handler = _serviceProvider!.GetRequiredService<ResolveRuleCommentCommandHandler>();
        var firstCommand = new ResolveRuleCommentCommand(
            commentId,
            _testUserId,
            false,
            false
        );
        await handler.Handle(firstCommand, TestCancellationToken);

        // Second resolution
        var secondCommand = new ResolveRuleCommentCommand(
            commentId,
            _testUserId,
            false,
            false
        );

        // Act
        var result = await handler.Handle(secondCommand, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeTrue();
    }
    [Fact]
    public async Task ResolveNonExistentComment_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();
        var handler = _serviceProvider!.GetRequiredService<ResolveRuleCommentCommandHandler>();
        var command = new ResolveRuleCommentCommand(
            nonExistentId,
            _testUserId,
            false,
            false
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}


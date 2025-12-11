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
using Api.Tests.Constants;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Comprehensive integration tests for UnresolveRuleComment workflow (Issue #1691).
/// Tests the complete comment unresolve pipeline using Testcontainers for real infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Unresolve comment successfully
/// 2. Authorization Errors: Non-owner unresolve attempts
/// 3. Admin Override: Admin can unresolve any comment
/// 4. Parent Unresolve: Cascade unresolve to parent
/// 5. Already Unresolved: Re-unresolve handling
/// 6. Not Found: Unresolve non-existent comment
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for UnresolveRuleCommentCommandHandler
/// Execution Time Target: <60s
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class UnresolveRuleCommentIntegrationTests : IAsyncLifetime
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
        // Start PostgreSQL container
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "unresolve_comment_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);

        // Setup services
        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={postgresPort};Database=unresolve_comment_test;Username=postgres;Password=postgres;";

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
            config.RegisterServicesFromAssembly(typeof(UnresolveRuleCommentCommandHandler).Assembly));

        // Register TimeProvider
        services.AddSingleton(TimeProvider.System);

        // Register handler explicitly
        services.AddScoped<UnresolveRuleCommentCommandHandler>();

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
            Name = "Test Game for Unresolve",
            Publisher = "Test Publisher",
            YearPublished = 2024,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<Guid> CreateResolvedCommentAsync(Guid userId, Guid? parentId = null)
    {
        var comment = new RuleSpecCommentEntity
        {
            Id = Guid.NewGuid(),
            GameId = _testGameId,
            Version = "1.0.0",
            CommentText = "Resolved comment",
            UserId = userId,
            ParentCommentId = parentId,
            IsResolved = true,
            ResolvedByUserId = userId,
            ResolvedAt = DateTime.UtcNow,
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
    public async Task UnresolveOwnComment_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateResolvedCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<UnresolveRuleCommentCommandHandler>();
        var command = new UnresolveRuleCommentCommand(
            commentId,
            _testUserId,
            false,
            false
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeFalse();
        result.ResolvedByUserId.Should().BeNull();
        result.ResolvedAt.Should().BeNull();

        // Verify database state
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId, TestCancellationToken);
        comment.Should().NotBeNull();
        comment!.IsResolved.Should().BeFalse();
        comment.ResolvedByUserId.Should().BeNull();
        comment.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task UnresolveComment_WithoutParent_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateResolvedCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<UnresolveRuleCommentCommandHandler>();
        var command = new UnresolveRuleCommentCommand(
            commentId,
            _testUserId,
            false,
            true // No parent to unresolve
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeFalse();
    }
    [Fact]
    public async Task UnresolveOtherUserComment_AsNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateResolvedCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<UnresolveRuleCommentCommandHandler>();
        var command = new UnresolveRuleCommentCommand(
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

        // Verify comment is still resolved
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId, TestCancellationToken);
        comment.Should().NotBeNull();
        comment!.IsResolved.Should().BeTrue();
    }
    [Fact]
    public async Task UnresolveOtherUserComment_AsAdmin_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateResolvedCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<UnresolveRuleCommentCommandHandler>();
        var command = new UnresolveRuleCommentCommand(
            commentId,
            _otherUserId, // Different user
            true, // But admin
            false
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeFalse();
    }
    [Fact]
    public async Task UnresolveComment_WithResolvedParent_UnresolvesParent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var parentId = await CreateResolvedCommentAsync(_testUserId);
        var childId = await CreateResolvedCommentAsync(_otherUserId, parentId);

        var handler = _serviceProvider!.GetRequiredService<UnresolveRuleCommentCommandHandler>();
        var command = new UnresolveRuleCommentCommand(
            childId,
            _otherUserId,
            false,
            true
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeFalse();

        // Verify parent is also unresolved
        var parent = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == parentId, TestCancellationToken);
        parent.Should().NotBeNull();
        parent!.IsResolved.Should().BeFalse("parent should be unresolved");
        parent.ResolvedByUserId.Should().BeNull();
        parent.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task UnresolveComment_WithUnresolvedParent_OnlyUnresolvesComment()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create unresolved parent
        var parentComment = new RuleSpecCommentEntity
        {
            Id = Guid.NewGuid(),
            GameId = _testGameId,
            Version = "1.0.0",
            CommentText = "Unresolved parent",
            UserId = _testUserId,
            IsResolved = false,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.RuleSpecComments.Add(parentComment);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var childId = await CreateResolvedCommentAsync(_otherUserId, parentComment.Id);

        var handler = _serviceProvider!.GetRequiredService<UnresolveRuleCommentCommandHandler>();
        var command = new UnresolveRuleCommentCommand(
            childId,
            _otherUserId,
            false,
            true
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeFalse();

        // Verify parent remains unresolved (no change)
        var parent = await _dbContext.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == parentComment.Id, TestCancellationToken);
        parent.Should().NotBeNull();
        parent!.IsResolved.Should().BeFalse();
    }
    [Fact]
    public async Task UnresolveAlreadyUnresolvedComment_Success()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create unresolved comment
        var comment = new RuleSpecCommentEntity
        {
            Id = Guid.NewGuid(),
            GameId = _testGameId,
            Version = "1.0.0",
            CommentText = "Already unresolved",
            UserId = _testUserId,
            IsResolved = false,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var handler = _serviceProvider!.GetRequiredService<UnresolveRuleCommentCommandHandler>();
        var command = new UnresolveRuleCommentCommand(
            comment.Id,
            _testUserId,
            false,
            false
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeFalse();
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
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }
    [Fact]
    public async Task UnresolveNonExistentComment_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();
        var handler = _serviceProvider!.GetRequiredService<UnresolveRuleCommentCommandHandler>();
        var command = new UnresolveRuleCommentCommand(
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

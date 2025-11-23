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
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Comprehensive integration tests for UpdateRuleComment workflow (Issue #1691).
/// Tests the complete comment update pipeline using Testcontainers for real infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Update own comment successfully
/// 2. Authorization Errors: Non-owner update attempts
/// 3. Validation Errors: Invalid input handling
/// 4. Not Found: Update non-existent comment
/// 5. Long Comments: Maximum length handling
/// 6. Concurrent Operations: Race condition handling
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for UpdateRuleCommentCommandHandler
/// Execution Time Target: <60s
/// </summary>
[Collection("UpdateRuleCommentIntegration")]
public sealed class UpdateRuleCommentIntegrationTests : IAsyncLifetime
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
            .WithEnvironment("POSTGRES_DB", "update_comment_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);

        // Setup services
        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={postgresPort};Database=update_comment_test;Username=postgres;Password=postgres;";

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
            config.RegisterServicesFromAssembly(typeof(UpdateRuleCommentCommandHandler).Assembly));

        // Register TimeProvider
        services.AddSingleton(TimeProvider.System);

        // Register handler explicitly
        services.AddScoped<UpdateRuleCommentCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);

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
            Name = "Test Game for Update",
            Publisher = "Test Publisher",
            YearPublished = 2024,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<Guid> CreateTestCommentAsync(Guid userId, string text = "Original text")
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
    public async Task UpdateOwnComment_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId, "Original comment");
        var handler = _serviceProvider!.GetRequiredService<UpdateRuleCommentCommandHandler>();
        var command = new UpdateRuleCommentCommand(commentId, "Updated comment text", _testUserId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.CommentText.Should().Be("Updated comment text");
        result.UpdatedAt.Should().NotBeNull();

        // Verify database state
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId);
        comment.Should().NotBeNull();
        comment!.CommentText.Should().Be("Updated comment text");
        comment.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateComment_PreservesOtherFields()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId, "Original");
        var originalComment = await _dbContext!.RuleSpecComments.FirstAsync(c => c.Id == commentId);
        var originalCreatedAt = originalComment.CreatedAt;
        var originalGameId = originalComment.GameId;
        var originalVersion = originalComment.Version;

        var handler = _serviceProvider!.GetRequiredService<UpdateRuleCommentCommandHandler>();
        var command = new UpdateRuleCommentCommand(commentId, "Modified text", _testUserId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.CreatedAt.Should().BeCloseTo(originalCreatedAt, TimeSpan.FromSeconds(1));
        result.GameId.Should().Be(originalGameId.ToString());
        result.Version.Should().Be(originalVersion);
    }

    #endregion

    #region 2. Authorization Error Tests

    [Fact]
    public async Task UpdateOtherUserComment_ThrowsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId, "Original");
        var handler = _serviceProvider!.GetRequiredService<UpdateRuleCommentCommandHandler>();
        var command = new UpdateRuleCommentCommand(commentId, "Attempted update", _otherUserId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not authorized*");

        // Verify comment is unchanged
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId);
        comment.Should().NotBeNull();
        comment!.CommentText.Should().Be("Original");
    }

    #endregion

    #region 3. Validation Error Tests

    [Fact]
    public async Task UpdateComment_WithEmptyText_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<UpdateRuleCommentCommandHandler>();
        var command = new UpdateRuleCommentCommand(commentId, "", _testUserId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public async Task UpdateComment_WithWhitespaceText_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<UpdateRuleCommentCommandHandler>();
        var command = new UpdateRuleCommentCommand(commentId, "   ", _testUserId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public async Task UpdateComment_ExceedingMaxLength_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);
        var handler = _serviceProvider!.GetRequiredService<UpdateRuleCommentCommandHandler>();
        var tooLongText = new string('a', 2001);
        var command = new UpdateRuleCommentCommand(commentId, tooLongText, _testUserId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum length*");
    }

    #endregion

    #region 4. Not Found Tests

    [Fact]
    public async Task UpdateNonExistentComment_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();
        var handler = _serviceProvider!.GetRequiredService<UpdateRuleCommentCommandHandler>();
        var command = new UpdateRuleCommentCommand(nonExistentId, "Updated text", _testUserId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region 5. Long Comment Tests

    [Fact]
    public async Task UpdateComment_WithMaxLength_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId, "Short");
        var handler = _serviceProvider!.GetRequiredService<UpdateRuleCommentCommandHandler>();
        var maxLengthText = new string('b', 2000); // Max length
        var command = new UpdateRuleCommentCommand(commentId, maxLengthText, _testUserId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.CommentText.Should().HaveLength(2000);
    }

    #endregion

    #region 6. Concurrent Operations Tests

    [Fact]
    public async Task ConcurrentUpdate_LastWriteWins()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId, "Original");

        // Create two independent handlers with separate DbContexts
        var handler1 = CreateIndependentHandler();
        var handler2 = CreateIndependentHandler();

        var command1 = new UpdateRuleCommentCommand(commentId, "Update from handler 1", _testUserId);

        var command2 = new UpdateRuleCommentCommand(commentId, "Update from handler 2", _testUserId);

        // Act
        var task1 = Task.Run(async () => await handler1.Handle(command1, TestCancellationToken));
        var task2 = Task.Run(async () => await handler2.Handle(command2, TestCancellationToken));

        await Task.WhenAll(task1, task2);

        // Assert - clear change tracker to avoid stale cache
        _dbContext!.ChangeTracker.Clear();
        var finalComment = await _dbContext.RuleSpecComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId);
        finalComment.Should().NotBeNull();

        // Either update should succeed (last write wins)
        var possibleTexts = new[] { "Update from handler 1", "Update from handler 2" };
        possibleTexts.Should().Contain(finalComment!.CommentText);
    }

    private UpdateRuleCommentCommandHandler CreateIndependentHandler()
    {
        var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UpdateRuleCommentCommandHandler>>();

        return new UpdateRuleCommentCommandHandler(dbContext, timeProvider, logger);
    }

    #endregion
}

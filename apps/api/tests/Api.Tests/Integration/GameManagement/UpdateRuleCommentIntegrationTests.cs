using Api.Tests.Infrastructure;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using Api.Tests.Constants;

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
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Issue", "2031")]
[Trait("Category", TestCategories.Integration)]
public sealed class UpdateRuleCommentIntegrationTests : IAsyncLifetime
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

    public UpdateRuleCommentIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        _databaseName = $"test_updatecomment_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
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

        await EnsureCreatedWithRetry(_dbContext);

        // Seed test data
        await SeedTestDataAsync();
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
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
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
        result.CreatedAt.Should().BeCloseTo(originalCreatedAt, TestConstants.Timing.VeryShortTimeout);
        result.GameId.Should().Be(originalGameId.ToString());
        result.Version.Should().Be(originalVersion);
    }
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
}

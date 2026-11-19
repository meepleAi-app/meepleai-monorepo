using Api.Tests.Infrastructure;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Xunit;
using Api.Tests.Constants;

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
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Issue", "2031")]
[Trait("Category", TestCategories.Integration)]
public sealed class DeleteRuleCommentIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private Guid _testUserId;
    private Guid _otherUserId;
    private Guid _testGameId;

    // Scopes and contexts created by CreateHandlerPausedBeforeSave, released in DisposeAsync.
    private readonly List<IAsyncDisposable> _owned = new();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public DeleteRuleCommentIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        _databaseName = $"test_deletecomment_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Register handler explicitly
        services.AddScoped<DeleteRuleCommentCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var owned in _owned)
            await owned.DisposeAsync();

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
        var game = new SharedGameEntity
        {
            Id = _testGameId,
            Title = "Test Game for Delete",
            YearPublished = 2024,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(game);

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
        // Issue #3866: production starts every request with an empty change tracker. Seeding
        // through the same DbContext the handler uses leaves the comment tracked, and the handler —
        // which reads with the production NoTracking default and then Remove()s what it read —
        // attaches a SECOND instance of the same row and throws an identity conflict that no real
        // scope can produce.
        _dbContext.ChangeTracker.Clear();
        return comment.Id;
    }

    private async Task ResetDatabaseAsync()
    {
        _dbContext!.RuleSpecComments.RemoveRange(_dbContext.RuleSpecComments);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }
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
        await act.Should().ThrowAsync<AccessDeniedException>()
            .WithMessage("*not authorized*");

        // Verify comment still exists
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId, TestCancellationToken);
        comment.Should().NotBeNull();
    }
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
    [Fact]
    [Trait("Issue", "5048")]
    public async Task DeleteNonExistentComment_ThrowsNotFound()
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

        // Assert — #2568: NotFoundException (404), not an InvalidOperationException that reached
        // 404 only because the middleware matches "not found" in its message.
        await act.Should().ThrowAsync<NotFoundException>();
    }
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
        _dbContext.ChangeTracker.Clear(); // #3866: fine arrange — vedi CreateTestCommentAsync

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
    /// <summary>
    /// Issue #5048: both requests read the comment before either deletes it — the interleaving
    /// that <c>Task.Run</c> produced only sometimes, and that made the old version of this test
    /// intermittent. The loser's DELETE touches 0 rows, which is the same situation as deleting an
    /// absent comment, seen later: it gets the same <see cref="NotFoundException"/>, not the
    /// <see cref="DbUpdateConcurrencyException"/> the middleware turns into 409 "reload and retry".
    /// </summary>
    [Fact]
    [Trait("Issue", "5048")]
    public async Task ConcurrentDeletion_BothReadBeforeEitherSaves_LoserGetsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentId = await CreateTestCommentAsync(_testUserId);

        var arrived = 0;
        var bothHaveRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task WaitForTheOther(CancellationToken ct)
        {
            if (Interlocked.Increment(ref arrived) == 2)
            {
                bothHaveRead.TrySetResult();
            }

            return bothHaveRead.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }

        var (handler1, db1) = CreateHandlerPausedBeforeSave(WaitForTheOther);
        var (handler2, db2) = CreateHandlerPausedBeforeSave(WaitForTheOther);
        var command = new DeleteRuleCommentCommand(commentId, _testUserId, false);

        // Act
        var outcomes = await Task.WhenAll(
            RunAsync(handler1, db1, command),
            RunAsync(handler2, db2, command));

        // Assert
        outcomes.Should().ContainSingle(o => o.Error == null && o.Deleted, "exactly one request deletes the comment");
        var loser = outcomes.Single(o => o.Error != null);
        loser.Error.Should().BeOfType<NotFoundException>("the comment is gone, as if it had never been there");

        // The loser's context is the one the audit behaviour saves its Error row with: a leftover
        // Deleted entry would resend the DELETE, fail again, and the audit row would be lost.
        loser.Db.ChangeTracker.Entries().Should().BeEmpty();
        var resave = async () => await loser.Db.SaveChangesAsync(TestCancellationToken);
        await resave.Should().NotThrowAsync();

        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId, TestCancellationToken);
        comment.Should().BeNull("comment should be deleted");
    }

    /// <summary>
    /// Issue #5048, the other direction: the 0-row DELETE is a REPLY removed under the cascade while
    /// the root is still there. That is a real conflict — a retry would succeed — so it must stay a
    /// <see cref="DbUpdateConcurrencyException"/> (409) and leave the root in place, rather than
    /// answer 404 for a comment that exists.
    /// </summary>
    [Fact]
    [Trait("Issue", "5048")]
    public async Task Deletion_ReplyRemovedBetweenReadAndSave_StaysConflictAndKeepsRoot()
    {
        // Arrange
        await ResetDatabaseAsync();
        var rootId = await CreateTestCommentAsync(_testUserId, "Parent comment");
        var replyId = Guid.NewGuid();
        _dbContext!.RuleSpecComments.Add(new RuleSpecCommentEntity
        {
            Id = replyId,
            GameId = _testGameId,
            Version = "1.0.0",
            CommentText = "Reply comment",
            UserId = _otherUserId,
            ParentCommentId = rootId,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear(); // #3866: fine arrange — vedi CreateTestCommentAsync

        // Another request deletes the reply after this handler has read the tree.
        var (handler, db) = CreateHandlerPausedBeforeSave(ct => _dbContext.RuleSpecComments
            .Where(c => c.Id == replyId)
            .ExecuteDeleteAsync(ct));

        // Act
        var outcome = await RunAsync(handler, db, new DeleteRuleCommentCommand(rootId, _testUserId, false));

        // Assert
        outcome.Error.Should().BeOfType<DbUpdateConcurrencyException>();
        db.ChangeTracker.Entries().Should().BeEmpty();
        var root = await _dbContext.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == rootId, TestCancellationToken);
        root.Should().NotBeNull("the failed save rolled back, and the root was never gone");
    }

    /// <summary>
    /// Issue #5048, a failed save that is not a 0-row DELETE: a reply inserted under the root between
    /// the read and the write makes the root DELETE fail on the foreign key. The outcome is unchanged
    /// (the error propagates), but the tracker must be clean for the same reason as above: the audit
    /// behaviour saves its Error row on this context.
    /// </summary>
    [Fact]
    [Trait("Issue", "5048")]
    public async Task Deletion_ReplyAddedBetweenReadAndSave_FailsWithACleanTracker()
    {
        // Arrange
        await ResetDatabaseAsync();
        var rootId = await CreateTestCommentAsync(_testUserId, "Parent comment");

        // Another request replies to the root after this handler has read it, with no replies.
        var (handler, db) = CreateHandlerPausedBeforeSave(async ct =>
        {
            _dbContext!.RuleSpecComments.Add(new RuleSpecCommentEntity
            {
                Id = Guid.NewGuid(),
                GameId = _testGameId,
                Version = "1.0.0",
                CommentText = "Late reply",
                UserId = _otherUserId,
                ParentCommentId = rootId,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(ct);
            _dbContext.ChangeTracker.Clear();
        });

        // Act
        var outcome = await RunAsync(handler, db, new DeleteRuleCommentCommand(rootId, _testUserId, false));

        // Assert
        outcome.Error.Should().BeOfType<DbUpdateException>();
        db.ChangeTracker.Entries().Should().BeEmpty();
        var resave = async () => await db.SaveChangesAsync(TestCancellationToken);
        await resave.Should().NotThrowAsync();
        var root = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == rootId, TestCancellationToken);
        root.Should().NotBeNull();
    }

    private static async Task<(bool Deleted, Exception? Error, MeepleAiDbContext Db)> RunAsync(
        DeleteRuleCommentCommandHandler handler, MeepleAiDbContext db, DeleteRuleCommentCommand command)
    {
        try
        {
            return (await handler.Handle(command, TestCancellationToken), null, db);
        }
        catch (Exception ex)
        {
            return (false, ex, db);
        }
    }

    /// <summary>
    /// A handler on its own context, whose first save waits for <paramref name="beforeSave"/>: what
    /// lets a test place another request exactly between this handler's read and its write.
    /// </summary>
    private (DeleteRuleCommentCommandHandler Handler, MeepleAiDbContext Db) CreateHandlerPausedBeforeSave(
        Func<CancellationToken, Task> beforeSave)
    {
        var scope = _serviceProvider!.CreateAsyncScope();
        _owned.Add(scope);
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_isolatedDbConnectionString, o => o.UseVector())
            .AddInterceptors(new BeforeFirstSaveInterceptor(beforeSave))
            .Options;
        var db = new MeepleAiDbContext(
            options,
            scope.ServiceProvider.GetRequiredService<IMediator>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventCollector>());
        _owned.Add(db);
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeleteRuleCommentCommandHandler>>();
        return (new DeleteRuleCommentCommandHandler(db, logger), db);
    }

    private sealed class BeforeFirstSaveInterceptor(Func<CancellationToken, Task> beforeSave) : SaveChangesInterceptor
    {
        private int _saves;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _saves) == 1)
            {
                await beforeSave(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
    }
}

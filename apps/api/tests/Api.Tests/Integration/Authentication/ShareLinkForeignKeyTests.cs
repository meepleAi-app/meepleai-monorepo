using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for ShareLink FK constraints.
/// Tests DeleteBehavior.Cascade for ChatThread, DeleteBehavior.Restrict for Creator.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection("Integration-GroupB")]
public class ShareLinkForeignKeyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_sharelink_fk_{Guid.NewGuid():N}";

    public ShareLinkForeignKeyTests(SharedTestcontainersFixture fixture)
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
    public async Task DeleteChatThread_WithShareLinks_CascadesDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "creator@test.com", DisplayName = "Creator", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "7 Wonders" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var threadId = Guid.NewGuid();
        var thread = new ChatThreadEntity
        {
            Id = threadId,

            UserId = userId,
            GameId = gameId,
            Title = "Shared Chat",

            CreatedAt = DateTime.UtcNow,

        };
        _dbContext.ChatThreads.Add(thread);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var shareLinkId = Guid.NewGuid();
        var shareLink = new ShareLinkEntity
        {
            Id = shareLinkId,
            ThreadId = threadId,
            CreatorId = userId,
            Role = Api.BoundedContexts.Authentication.Domain.ValueObjects.ShareLinkRole.View,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            AccessCount = 0
        };
        _dbContext.ShareLinks.Add(shareLink);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete chat thread (cascades to share links)
        _dbContext.ChatThreads.Remove(thread);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Share link deleted via cascade
        var deletedLink = await _dbContext.ShareLinks.FindAsync(shareLinkId);
        deletedLink.Should().BeNull();
    }

    [Fact]
    public async Task DeleteUser_WithActiveShareLinks_ThrowsDbUpdateException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "creator@test.com", DisplayName = "Creator", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Catan" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var threadId = Guid.NewGuid();
        var thread = new ChatThreadEntity { Id = threadId, UserId = userId, GameId = gameId, Title = "Chat", };
        _dbContext.ChatThreads.Add(thread);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var shareLink = new ShareLinkEntity
        {
            Id = Guid.NewGuid(),
            ThreadId = threadId,
            CreatorId = userId,
            Role = Api.BoundedContexts.Authentication.Domain.ValueObjects.ShareLinkRole.Comment,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            AccessCount = 5,
            Label = "Team Share"
        };
        _dbContext.ShareLinks.Add(shareLink);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - FK Restrict prevents user deletion with active share links
        // EF Core throws InvalidOperationException immediately on Remove() when FK is severed
        var act = () =>
            _dbContext.Users.Remove(user);
        var exception = act.Should().Throw<InvalidOperationException>().Which;

        exception.Should().NotBeNull();
        exception.Message.Should().Contain("has been severed");
    }

    [Fact]
    public async Task RevokeShareLink_ThenDeleteUser_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "creator@test.com", DisplayName = "Creator", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Pandemic" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var threadId = Guid.NewGuid();
        var thread = new ChatThreadEntity { Id = threadId, UserId = userId, GameId = gameId, Title = "Chat", };
        _dbContext.ChatThreads.Add(thread);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var shareLinkId = Guid.NewGuid();
        var shareLink = new ShareLinkEntity
        {
            Id = shareLinkId,
            ThreadId = threadId,
            CreatorId = userId,
            Role = Api.BoundedContexts.Authentication.Domain.ValueObjects.ShareLinkRole.View,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            AccessCount = 0
        };
        _dbContext.ShareLinks.Add(shareLink);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete share link first, then user
        _dbContext.ShareLinks.Remove(shareLink);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Both deleted successfully
        var deletedLink = await _dbContext.ShareLinks.FindAsync(shareLinkId);
        deletedLink.Should().BeNull();

        var deletedUser = await _dbContext.Users.FindAsync(userId);
        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task DeleteShareLink_LeavesUserAndThreadIntact()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "creator@test.com", DisplayName = "Creator", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Azul" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var threadId = Guid.NewGuid();
        var thread = new ChatThreadEntity { Id = threadId, UserId = userId, GameId = gameId, Title = "Chat", };
        _dbContext.ChatThreads.Add(thread);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var shareLinkId = Guid.NewGuid();
        var shareLink = new ShareLinkEntity
        {
            Id = shareLinkId,
            ThreadId = threadId,
            CreatorId = userId,
            Role = Api.BoundedContexts.Authentication.Domain.ValueObjects.ShareLinkRole.View,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            AccessCount = 0
        };
        _dbContext.ShareLinks.Add(shareLink);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete share link
        _dbContext.ShareLinks.Remove(shareLink);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - User and thread remain
        var remainingUser = await _dbContext.Users.FindAsync(userId);
        remainingUser.Should().NotBeNull();

        var remainingThread = await _dbContext.ChatThreads.FindAsync(threadId);
        remainingThread.Should().NotBeNull();
    }
}

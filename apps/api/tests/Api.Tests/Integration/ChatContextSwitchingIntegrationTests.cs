using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for CHAT-03: Multi-document context switching in chat interface
///
/// Feature: Multi-game chat context management
/// As a user discussing multiple games
/// I want to switch between rulebooks without losing my conversation history
/// So that I can compare rules across games and maintain context for each game
///
/// Epic: EPIC-03 - Chat Interface Enhancement
/// Issue: #403
/// </summary>
public class ChatContextSwitchingIntegrationTests
{
    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        // Enable foreign keys for SQLite
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static async Task<(UserEntity user, GameEntity chess, GameEntity checkers, AgentEntity chessAgent, AgentEntity checkersAgent)> SeedTestDataAsync(MeepleAiDbContext dbContext)
    {
        var user = new UserEntity
        {
            Id = "user-123",
            Email = "test@meepleai.dev",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var chess = new GameEntity { Id = "chess", Name = "Chess" };
        var checkers = new GameEntity { Id = "checkers", Name = "Checkers" };

        var chessAgent = new AgentEntity
        {
            Id = "chess-qa",
            GameId = "chess",
            Name = "Chess Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };

        var checkersAgent = new AgentEntity
        {
            Id = "checkers-qa",
            GameId = "checkers",
            Name = "Checkers Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.Games.AddRange(chess, checkers);
        dbContext.Agents.AddRange(chessAgent, checkersAgent);
        await dbContext.SaveChangesAsync();

        return (user, chess, checkers, chessAgent, checkersAgent);
    }

    /// <summary>
    /// Scenario: User creates separate chat sessions for different games
    ///   Given user has two games (Chess, Checkers)
    ///   When user creates chat for Chess
    ///   And user creates chat for Checkers
    ///   Then each chat is associated with correct game
    ///   And chats can be retrieved independently
    /// </summary>
    [Fact]
    public async Task CreateChatsForDifferentGames_EachChatMaintainsGameAssociation()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var (user, chess, checkers, chessAgent, checkersAgent) = await SeedTestDataAsync(dbContext);
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        // Act: Create chat for Chess
        var chessChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);

        // Act: Create chat for Checkers
        var checkersChat = await service.CreateChatAsync(user.Id, checkers.Id, checkersAgent.Id);

        // Assert: Each chat has correct game association
        Assert.NotEqual(Guid.Empty, chessChat.Id);
        Assert.Equal(chess.Id, chessChat.GameId);

        Assert.NotEqual(Guid.Empty, checkersChat.Id);
        Assert.Equal(checkers.Id, checkersChat.GameId);

        Assert.NotEqual(chessChat.Id, checkersChat.Id);
    }

    /// <summary>
    /// Scenario: User filters chats by game ID
    ///   Given user has chats for Chess and Checkers
    ///   When user requests chats filtered by Chess game ID
    ///   Then only Chess chats are returned
    ///   And Checkers chats are not included
    /// </summary>
    [Fact]
    public async Task GetUserChatsByGameAsync_FiltersCorrectly()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var (user, chess, checkers, chessAgent, checkersAgent) = await SeedTestDataAsync(dbContext);
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var chessChat1 = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        var chessChat2 = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        var checkersChat = await service.CreateChatAsync(user.Id, checkers.Id, checkersAgent.Id);

        // Act: Get chats for Chess only
        var chessChats = await service.GetUserChatsByGameAsync(user.Id, chess.Id);

        // Assert: Only Chess chats returned
        Assert.Equal(2, chessChats.Count);
        Assert.All(chessChats, chat => Assert.Equal(chess.Id, chat.GameId));
        Assert.Contains(chessChats, c => c.Id == chessChat1.Id);
        Assert.Contains(chessChats, c => c.Id == chessChat2.Id);
        Assert.DoesNotContain(chessChats, c => c.Id == checkersChat.Id);
    }

    /// <summary>
    /// Scenario: User maintains separate conversation history per game
    ///   Given user has chat for Chess with messages about castling
    ///   And user has chat for Checkers with messages about kings
    ///   When user retrieves Chess chat
    ///   Then only Chess messages are returned
    ///   When user retrieves Checkers chat
    ///   Then only Checkers messages are returned
    /// </summary>
    [Fact]
    public async Task SwitchBetweenGames_MaintainsSeparateMessageHistory()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var (user, chess, checkers, chessAgent, checkersAgent) = await SeedTestDataAsync(dbContext);
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var chessChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        var checkersChat = await service.CreateChatAsync(user.Id, checkers.Id, checkersAgent.Id);

        // Add messages to Chess chat
        await service.AddMessageAsync(chessChat.Id, user.Id, "user", "How does castling work?");
        await service.AddMessageAsync(chessChat.Id, user.Id, "assistant", "Castling is a special move...");

        // Add messages to Checkers chat
        await service.AddMessageAsync(checkersChat.Id, user.Id, "user", "Can pieces move backwards?");
        await service.AddMessageAsync(checkersChat.Id, user.Id, "assistant", "Only kings can move backwards...");

        // Act: Retrieve Chess chat history
        var chessHistory = await service.GetChatHistoryAsync(chessChat.Id, user.Id);

        // Assert: Chess chat has only Chess messages
        Assert.Equal(2, chessHistory.Count);
        Assert.Contains(chessHistory, msg => msg.Message.Contains("castling"));
        Assert.DoesNotContain(chessHistory, msg => msg.Message.Contains("backwards"));

        // Act: Retrieve Checkers chat history
        var checkersHistory = await service.GetChatHistoryAsync(checkersChat.Id, user.Id);

        // Assert: Checkers chat has only Checkers messages
        Assert.Equal(2, checkersHistory.Count);
        Assert.Contains(checkersHistory, msg => msg.Message.Contains("backwards"));
        Assert.DoesNotContain(checkersHistory, msg => msg.Message.Contains("castling"));
    }

    /// <summary>
    /// Scenario: User switches between games multiple times
    ///   Given user has ongoing conversations for Chess and Checkers
    ///   When user adds message to Chess
    ///   Then switches to Checkers and adds message
    ///   Then switches back to Chess and adds another message
    ///   Then each game's chat preserves complete history
    /// </summary>
    [Fact]
    public async Task MultipleSwitches_PreservesHistoryForEachGame()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var (user, chess, checkers, chessAgent, checkersAgent) = await SeedTestDataAsync(dbContext);
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var chessChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        var checkersChat = await service.CreateChatAsync(user.Id, checkers.Id, checkersAgent.Id);

        // Act: Simulate user switching between games
        // 1. Chess conversation
        await service.AddMessageAsync(chessChat.Id, user.Id, "user", "Question 1 about chess");
        await service.AddMessageAsync(chessChat.Id, user.Id, "assistant", "Answer 1 about chess");

        // 2. Switch to Checkers
        await service.AddMessageAsync(checkersChat.Id, user.Id, "user", "Question 1 about checkers");
        await service.AddMessageAsync(checkersChat.Id, user.Id, "assistant", "Answer 1 about checkers");

        // 3. Switch back to Chess
        await service.AddMessageAsync(chessChat.Id, user.Id, "user", "Question 2 about chess");
        await service.AddMessageAsync(chessChat.Id, user.Id, "assistant", "Answer 2 about chess");

        // 4. Switch back to Checkers
        await service.AddMessageAsync(checkersChat.Id, user.Id, "user", "Question 2 about checkers");

        // Assert: Chess chat has complete Chess history
        var chessHistory = await service.GetChatHistoryAsync(chessChat.Id, user.Id);
        Assert.Equal(4, chessHistory.Count);
        Assert.All(chessHistory, msg => Assert.Contains("chess", msg.Message.ToLower()));

        // Assert: Checkers chat has complete Checkers history
        var checkersHistory = await service.GetChatHistoryAsync(checkersChat.Id, user.Id);
        Assert.Equal(3, checkersHistory.Count);
        Assert.All(checkersHistory, msg => Assert.Contains("checkers", msg.Message.ToLower()));
    }

    /// <summary>
    /// Scenario: User has multiple concurrent chats per game
    ///   Given user can create multiple chats for same game
    ///   When user creates 2 chats for Chess
    ///   Then both chats are independent
    ///   And filtering by gameId returns both
    /// </summary>
    [Fact]
    public async Task MultipleConcurrentChatsPerGame_MaintainsIndependence()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var (user, chess, checkers, chessAgent, checkersAgent) = await SeedTestDataAsync(dbContext);
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        // Act: Create 2 chats for Chess with different conversations
        var chessChat1 = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        await service.AddMessageAsync(chessChat1.Id, user.Id, "user", "Opening strategy?");

        var chessChat2 = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        await service.AddMessageAsync(chessChat2.Id, user.Id, "user", "Endgame tactics?");

        // Assert: Both chats exist and are independent
        var chessChats = await service.GetUserChatsByGameAsync(user.Id, chess.Id);
        Assert.Equal(2, chessChats.Count);

        var history1 = await service.GetChatHistoryAsync(chessChat1.Id, user.Id);
        var history2 = await service.GetChatHistoryAsync(chessChat2.Id, user.Id);

        Assert.Single(history1);
        Assert.Contains("Opening", history1[0].Message);

        Assert.Single(history2);
        Assert.Contains("Endgame", history2[0].Message);
    }

    /// <summary>
    /// Scenario: LastMessageAt timestamp updates correctly per game
    ///   Given user has chats for Chess and Checkers
    ///   When user adds message to Chess chat
    ///   Then Chess chat's LastMessageAt updates
    ///   And Checkers chat's LastMessageAt remains unchanged
    /// </summary>
    [Fact]
    public async Task LastMessageAt_UpdatesIndependentlyPerChat()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var (user, chess, checkers, chessAgent, checkersAgent) = await SeedTestDataAsync(dbContext);
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var chessChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        var checkersChat = await service.CreateChatAsync(user.Id, checkers.Id, checkersAgent.Id);

        // Initial state: both have null LastMessageAt
        Assert.Null(chessChat.LastMessageAt);
        Assert.Null(checkersChat.LastMessageAt);

        // Act: Add message to Chess chat only
        await Task.Delay(10); // Ensure timestamp difference
        await service.AddMessageAsync(chessChat.Id, user.Id, "user", "Chess question");

        // Assert: Reload chats from DB
        var updatedChessChat = await dbContext.Chats.FindAsync(chessChat.Id);
        var updatedCheckersChat = await dbContext.Chats.FindAsync(checkersChat.Id);

        Assert.NotNull(updatedChessChat!.LastMessageAt);
        Assert.Null(updatedCheckersChat!.LastMessageAt);
    }

    /// <summary>
    /// Scenario: Chat ordering by recency works independently per game
    ///   Given user has multiple chats for Chess with different timestamps
    ///   When user requests chats filtered by Chess
    ///   Then chats are ordered by most recent activity (LastMessageAt)
    /// </summary>
    [Fact]
    public async Task ChatOrdering_WorksCorrectlyPerGame()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var (user, chess, checkers, chessAgent, checkersAgent) = await SeedTestDataAsync(dbContext);
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        // Create 3 Chess chats with different activity times
        var oldChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        await Task.Delay(10);

        var midChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        await Task.Delay(10);

        var recentChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        await Task.Delay(10);

        // Add messages to establish recency order (reverse chronological)
        await service.AddMessageAsync(recentChat.Id, user.Id, "user", "Most recent");
        await Task.Delay(10);
        await service.AddMessageAsync(midChat.Id, user.Id, "user", "Middle");
        await Task.Delay(10);
        await service.AddMessageAsync(oldChat.Id, user.Id, "user", "Oldest");

        // Act: Get chats for Chess
        var chessChats = await service.GetUserChatsByGameAsync(user.Id, chess.Id);

        // Assert: Chats ordered by LastMessageAt descending (most recent first)
        Assert.Equal(3, chessChats.Count);
        Assert.Equal(oldChat.Id, chessChats[0].Id); // Added message last, so most recent
        Assert.Equal(midChat.Id, chessChats[1].Id);
        Assert.Equal(recentChat.Id, chessChats[2].Id);
    }

    /// <summary>
    /// Scenario: Edge case - user has no chats for selected game
    ///   Given user has chat for Chess but not Checkers
    ///   When user requests chats for Checkers
    ///   Then empty list is returned
    /// </summary>
    [Fact]
    public async Task GetChatsByGame_WhenNoChatsExist_ReturnsEmptyList()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var (user, chess, checkers, chessAgent, checkersAgent) = await SeedTestDataAsync(dbContext);
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        // Only create chat for Chess
        await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);

        // Act: Request chats for Checkers (none exist)
        var checkersChats = await service.GetUserChatsByGameAsync(user.Id, checkers.Id);

        // Assert: Empty list returned
        Assert.Empty(checkersChats);
    }

    /// <summary>
    /// Scenario: Deleting chat for one game doesn't affect other game's chats
    ///   Given user has chats for Chess and Checkers
    ///   When user deletes Chess chat
    ///   Then Chess chat is removed
    ///   And Checkers chat remains intact
    /// </summary>
    [Fact]
    public async Task DeleteChat_OnlyAffectsTargetGameChat()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var (user, chess, checkers, chessAgent, checkersAgent) = await SeedTestDataAsync(dbContext);
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var chessChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        var checkersChat = await service.CreateChatAsync(user.Id, checkers.Id, checkersAgent.Id);

        // Act: Delete Chess chat
        var deleted = await service.DeleteChatAsync(chessChat.Id, user.Id);

        // Assert: Chess chat deleted
        Assert.True(deleted);
        var chessChats = await service.GetUserChatsByGameAsync(user.Id, chess.Id);
        Assert.Empty(chessChats);

        // Assert: Checkers chat still exists
        var checkersChats = await service.GetUserChatsByGameAsync(user.Id, checkers.Id);
        Assert.Single(checkersChats);
        Assert.Equal(checkersChat.Id, checkersChats[0].Id);
    }
}

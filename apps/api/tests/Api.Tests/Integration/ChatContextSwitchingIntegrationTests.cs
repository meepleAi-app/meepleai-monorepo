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
using FluentAssertions;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

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

    private static ChatService CreateChatService(MeepleAiDbContext dbContext)
    {
        var auditService = new AuditService(dbContext, NullLogger<AuditService>.Instance);
        return new ChatService(dbContext, NullLogger<ChatService>.Instance, auditService);
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
        var service = CreateChatService(dbContext);

        // Act: Create chat for Chess
        var chessChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);

        // Act: Create chat for Checkers
        var checkersChat = await service.CreateChatAsync(user.Id, checkers.Id, checkersAgent.Id);

        // Assert: Each chat has correct game association
        chessChat.Id.Should().NotBe(Guid.Empty);
        chessChat.GameId.Should().Be(chess.Id);

        checkersChat.Id.Should().NotBe(Guid.Empty);
        checkersChat.GameId.Should().Be(checkers.Id);

        checkersChat.Id.Should().NotBe(chessChat.Id);
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
        var service = CreateChatService(dbContext);

        var chessChat1 = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        var chessChat2 = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        var checkersChat = await service.CreateChatAsync(user.Id, checkers.Id, checkersAgent.Id);

        // Act: Get chats for Chess only
        var chessChats = await service.GetUserChatsByGameAsync(user.Id, chess.Id);

        // Assert: Only Chess chats returned
        chessChats.Count.Should().Be(2);
        chessChats.Should().OnlyContain(chat => chat.GameId == chess.Id);
        c => c.Id == chessChat1.Id.Should().Contain(chessChats);
        c => c.Id == chessChat2.Id.Should().Contain(chessChats);
        c => c.Id == checkersChat.Id.Should().NotContain(chessChats);
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
        var service = CreateChatService(dbContext);

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
        chessHistory.Count.Should().Be(2);
        msg => msg.Message.Contains("castling").Should().Contain(chessHistory);
        msg => msg.Message.Contains("backwards").Should().NotContain(chessHistory);

        // Act: Retrieve Checkers chat history
        var checkersHistory = await service.GetChatHistoryAsync(checkersChat.Id, user.Id);

        // Assert: Checkers chat has only Checkers messages
        checkersHistory.Count.Should().Be(2);
        msg => msg.Message.Contains("backwards").Should().Contain(checkersHistory);
        msg => msg.Message.Contains("castling").Should().NotContain(checkersHistory);
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
        var service = CreateChatService(dbContext);

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
        chessHistory.Count.Should().Be(4);
        chessHistory.Should().OnlyContain(msg => msg.Message.ToLower()).Should().Contain("chess");

        // Assert: Checkers chat has complete Checkers history
        var checkersHistory = await service.GetChatHistoryAsync(checkersChat.Id, user.Id);
        checkersHistory.Count.Should().Be(3);
        checkersHistory.Should().OnlyContain(msg => msg.Message.ToLower()).Should().Contain("checkers");
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
        var service = CreateChatService(dbContext);

        // Act: Create 2 chats for Chess with different conversations
        var chessChat1 = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        await service.AddMessageAsync(chessChat1.Id, user.Id, "user", "Opening strategy?");

        var chessChat2 = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        await service.AddMessageAsync(chessChat2.Id, user.Id, "user", "Endgame tactics?");

        // Assert: Both chats exist and are independent
        var chessChats = await service.GetUserChatsByGameAsync(user.Id, chess.Id);
        chessChats.Count.Should().Be(2);

        var history1 = await service.GetChatHistoryAsync(chessChat1.Id, user.Id);
        var history2 = await service.GetChatHistoryAsync(chessChat2.Id, user.Id);

        history1.Should().ContainSingle();
        history1[0].Message.Should().Contain("Opening");

        history2.Should().ContainSingle();
        history2[0].Message.Should().Contain("Endgame");
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
        var service = CreateChatService(dbContext);

        var chessChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        var checkersChat = await service.CreateChatAsync(user.Id, checkers.Id, checkersAgent.Id);

        // Initial state: both have null LastMessageAt
        chessChat.LastMessageAt.Should().BeNull();
        checkersChat.LastMessageAt.Should().BeNull();

        // Act: Add message to Chess chat only
        await Task.Delay(10); // Ensure timestamp difference
        await service.AddMessageAsync(chessChat.Id, user.Id, "user", "Chess question");

        // Assert: Reload chats from DB
        var updatedChessChat = await dbContext.Chats.FindAsync(chessChat.Id);
        var updatedCheckersChat = await dbContext.Chats.FindAsync(checkersChat.Id);

        updatedChessChat!.LastMessageAt.Should().NotBeNull();
        updatedCheckersChat!.LastMessageAt.Should().BeNull();
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
        var service = CreateChatService(dbContext);

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
        chessChats.Count.Should().Be(3);
        chessChats[0].Id.Should().Be(oldChat.Id); // Added message last, so most recent
        chessChats[1].Id.Should().Be(midChat.Id);
        chessChats[2].Id.Should().Be(recentChat.Id);
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
        var service = CreateChatService(dbContext);

        // Only create chat for Chess
        await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);

        // Act: Request chats for Checkers (none exist)
        var checkersChats = await service.GetUserChatsByGameAsync(user.Id, checkers.Id);

        // Assert: Empty list returned
        checkersChats.Should().BeEmpty();
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
        var service = CreateChatService(dbContext);

        var chessChat = await service.CreateChatAsync(user.Id, chess.Id, chessAgent.Id);
        var checkersChat = await service.CreateChatAsync(user.Id, checkers.Id, checkersAgent.Id);

        // Act: Delete Chess chat
        var deleted = await service.DeleteChatAsync(chessChat.Id, user.Id);

        // Assert: Chess chat deleted
        deleted.Should().BeTrue();
        var chessChats = await service.GetUserChatsByGameAsync(user.Id, chess.Id);
        chessChats.Should().BeEmpty();

        // Assert: Checkers chat still exists
        var checkersChats = await service.GetUserChatsByGameAsync(user.Id, checkers.Id);
        checkersChats.Should().ContainSingle();
        checkersChats[0].Id.Should().Be(checkersChat.Id);
    }
}

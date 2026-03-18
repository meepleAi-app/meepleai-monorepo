using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Domain.Entities;

/// <summary>
/// Unit tests for the PlayerMemory aggregate root entity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class PlayerMemoryTests
{
    #region CreateForUser Tests

    [Fact]
    public void CreateForUser_WithValidUserId_ReturnsPlayerMemory()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        // Act
        var player = PlayerMemory.CreateForUser(userId, groupId);

        // Assert
        player.Should().NotBeNull();
        player.Id.Should().NotBe(Guid.Empty);
        player.UserId.Should().Be(userId);
        player.GuestName.Should().BeNull();
        player.GroupId.Should().Be(groupId);
        player.GameStats.Should().BeEmpty();
        player.ClaimedAt.Should().BeNull();
        player.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateForUser_WithoutGroupId_CreatesWithNullGroupId()
    {
        var player = PlayerMemory.CreateForUser(Guid.NewGuid());
        player.GroupId.Should().BeNull();
    }

    [Fact]
    public void CreateForUser_WithEmptyUserId_ThrowsArgumentException()
    {
        var act = () => PlayerMemory.CreateForUser(Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    #endregion

    #region CreateForGuest Tests

    [Fact]
    public void CreateForGuest_WithValidName_ReturnsPlayerMemory()
    {
        // Arrange
        var groupId = Guid.NewGuid();

        // Act
        var player = PlayerMemory.CreateForGuest("Bob", groupId);

        // Assert
        player.Should().NotBeNull();
        player.UserId.Should().BeNull();
        player.GuestName.Should().Be("Bob");
        player.GroupId.Should().Be(groupId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateForGuest_WithEmptyName_ThrowsArgumentException(string? name)
    {
        var act = () => PlayerMemory.CreateForGuest(name!);
        act.Should().Throw<ArgumentException>().WithParameterName("guestName");
    }

    #endregion

    #region ClaimByUser Tests

    [Fact]
    public void ClaimByUser_GuestPlayer_SetsUserIdAndClaimedAt()
    {
        // Arrange
        var player = PlayerMemory.CreateForGuest("Alice");
        var userId = Guid.NewGuid();

        // Act
        player.ClaimByUser(userId);

        // Assert
        player.UserId.Should().Be(userId);
        player.ClaimedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ClaimByUser_AlreadyClaimed_ThrowsInvalidOperationException()
    {
        // Arrange
        var player = PlayerMemory.CreateForUser(Guid.NewGuid());

        // Act
        var act = () => player.ClaimByUser(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Already claimed*");
    }

    [Fact]
    public void ClaimByUser_GuestClaimedTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        var player = PlayerMemory.CreateForGuest("Alice");
        player.ClaimByUser(Guid.NewGuid());

        // Act
        var act = () => player.ClaimByUser(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ClaimByUser_WithEmptyUserId_ThrowsArgumentException()
    {
        var player = PlayerMemory.CreateForGuest("Alice");
        var act = () => player.ClaimByUser(Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    #endregion

    #region UpdateGameStats Tests

    [Fact]
    public void UpdateGameStats_NewGame_Win_CreatesNewEntry()
    {
        // Arrange
        var player = PlayerMemory.CreateForUser(Guid.NewGuid());
        var gameId = Guid.NewGuid();

        // Act
        player.UpdateGameStats(gameId, won: true, score: 100);

        // Assert
        player.GameStats.Should().HaveCount(1);
        player.GameStats[0].GameId.Should().Be(gameId);
        player.GameStats[0].Wins.Should().Be(1);
        player.GameStats[0].Losses.Should().Be(0);
        player.GameStats[0].TotalPlayed.Should().Be(1);
        player.GameStats[0].BestScore.Should().Be(100);
    }

    [Fact]
    public void UpdateGameStats_NewGame_Loss_CreatesNewEntry()
    {
        var player = PlayerMemory.CreateForUser(Guid.NewGuid());
        var gameId = Guid.NewGuid();

        player.UpdateGameStats(gameId, won: false);

        player.GameStats[0].Wins.Should().Be(0);
        player.GameStats[0].Losses.Should().Be(1);
        player.GameStats[0].TotalPlayed.Should().Be(1);
        player.GameStats[0].BestScore.Should().BeNull();
    }

    [Fact]
    public void UpdateGameStats_ExistingGame_IncrementsCounters()
    {
        // Arrange
        var player = PlayerMemory.CreateForUser(Guid.NewGuid());
        var gameId = Guid.NewGuid();
        player.UpdateGameStats(gameId, won: true, score: 50);

        // Act
        player.UpdateGameStats(gameId, won: false, score: 80);

        // Assert
        player.GameStats.Should().HaveCount(1);
        player.GameStats[0].TotalPlayed.Should().Be(2);
        player.GameStats[0].Wins.Should().Be(1);
        player.GameStats[0].Losses.Should().Be(1);
        player.GameStats[0].BestScore.Should().Be(80); // higher score replaces
    }

    [Fact]
    public void UpdateGameStats_ExistingGame_LowerScore_DoesNotReplaceBest()
    {
        var player = PlayerMemory.CreateForUser(Guid.NewGuid());
        var gameId = Guid.NewGuid();
        player.UpdateGameStats(gameId, won: true, score: 100);

        player.UpdateGameStats(gameId, won: true, score: 50);

        player.GameStats[0].BestScore.Should().Be(100);
    }

    [Fact]
    public void UpdateGameStats_WithEmptyGameId_ThrowsArgumentException()
    {
        var player = PlayerMemory.CreateForUser(Guid.NewGuid());
        var act = () => player.UpdateGameStats(Guid.Empty, won: true);
        act.Should().Throw<ArgumentException>().WithParameterName("gameId");
    }

    #endregion
}

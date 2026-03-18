using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Domain.Entities;

/// <summary>
/// Unit tests for the GroupMemory aggregate root entity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class GroupMemoryTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsGroupMemoryWithAllPropertiesSet()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var name = "Friday Night Group";

        // Act
        var group = GroupMemory.Create(creatorId, name);

        // Assert
        group.Should().NotBeNull();
        group.Id.Should().NotBe(Guid.Empty);
        group.CreatorId.Should().Be(creatorId);
        group.Name.Should().Be(name);
        group.Members.Should().BeEmpty();
        group.Preferences.Should().NotBeNull();
        group.Stats.Should().NotBeNull();
        group.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptyCreatorId_ThrowsArgumentException()
    {
        var act = () => GroupMemory.Create(Guid.Empty, "Test");
        act.Should().Throw<ArgumentException>().WithParameterName("creatorId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ThrowsArgumentException(string? name)
    {
        var act = () => GroupMemory.Create(Guid.NewGuid(), name!);
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    #endregion

    #region AddMember Tests

    [Fact]
    public void AddMember_WithValidUserId_AddsMember()
    {
        // Arrange
        var group = GroupMemory.Create(Guid.NewGuid(), "Test Group");
        var userId = Guid.NewGuid();

        // Act
        group.AddMember(userId);

        // Assert
        group.Members.Should().HaveCount(1);
        group.Members[0].UserId.Should().Be(userId);
        group.Members[0].GuestName.Should().BeNull();
        group.Members[0].JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddMember_WithEmptyUserId_ThrowsArgumentException()
    {
        var group = GroupMemory.Create(Guid.NewGuid(), "Test");
        var act = () => group.AddMember(Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    #endregion

    #region AddGuestMember Tests

    [Fact]
    public void AddGuestMember_WithValidName_AddsGuestMember()
    {
        // Arrange
        var group = GroupMemory.Create(Guid.NewGuid(), "Test Group");

        // Act
        group.AddGuestMember("Alice");

        // Assert
        group.Members.Should().HaveCount(1);
        group.Members[0].UserId.Should().BeNull();
        group.Members[0].GuestName.Should().Be("Alice");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddGuestMember_WithEmptyName_ThrowsArgumentException(string? name)
    {
        var group = GroupMemory.Create(Guid.NewGuid(), "Test");
        var act = () => group.AddGuestMember(name!);
        act.Should().Throw<ArgumentException>().WithParameterName("guestName");
    }

    #endregion

    #region UpdatePreferences Tests

    [Fact]
    public void UpdatePreferences_WithValidPreferences_UpdatesPreferences()
    {
        // Arrange
        var group = GroupMemory.Create(Guid.NewGuid(), "Test Group");
        var prefs = new GroupPreferences
        {
            MaxDuration = TimeSpan.FromHours(2),
            PreferredComplexity = PreferredComplexity.Medium,
            CustomNotes = "No games over 3 hours"
        };

        // Act
        group.UpdatePreferences(prefs);

        // Assert
        group.Preferences.MaxDuration.Should().Be(TimeSpan.FromHours(2));
        group.Preferences.PreferredComplexity.Should().Be(PreferredComplexity.Medium);
        group.Preferences.CustomNotes.Should().Be("No games over 3 hours");
    }

    [Fact]
    public void UpdatePreferences_WithNull_ThrowsArgumentNullException()
    {
        var group = GroupMemory.Create(Guid.NewGuid(), "Test");
        var act = () => group.UpdatePreferences(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UpdateStats Tests

    [Fact]
    public void UpdateStats_WithValidStats_UpdatesStats()
    {
        // Arrange
        var group = GroupMemory.Create(Guid.NewGuid(), "Test Group");
        var gameId = Guid.NewGuid();
        var stats = new GroupStats
        {
            TotalSessions = 10,
            GamePlayCounts = new Dictionary<Guid, int> { { gameId, 5 } },
            LastPlayedAt = DateTime.UtcNow
        };

        // Act
        group.UpdateStats(stats);

        // Assert
        group.Stats.TotalSessions.Should().Be(10);
        group.Stats.GamePlayCounts.Should().ContainKey(gameId);
        group.Stats.LastPlayedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateStats_WithNull_ThrowsArgumentNullException()
    {
        var group = GroupMemory.Create(Guid.NewGuid(), "Test");
        var act = () => group.UpdateStats(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}

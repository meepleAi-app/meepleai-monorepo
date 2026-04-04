using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Domain.Entities;

/// <summary>
/// Unit tests for the GameMemory aggregate root entity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class GameMemoryTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsGameMemoryWithAllPropertiesSet()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        // Act
        var memory = GameMemory.Create(gameId, ownerId);

        // Assert
        memory.Should().NotBeNull();
        memory.Id.Should().NotBe(Guid.Empty);
        memory.GameId.Should().Be(gameId);
        memory.OwnerId.Should().Be(ownerId);
        memory.HouseRules.Should().BeEmpty();
        memory.Notes.Should().BeEmpty();
        memory.CustomSetup.Should().BeNull();
        memory.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptyGameId_ThrowsArgumentException()
    {
        var act = () => GameMemory.Create(Guid.Empty, Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithParameterName("gameId");
    }

    [Fact]
    public void Create_WithEmptyOwnerId_ThrowsArgumentException()
    {
        var act = () => GameMemory.Create(Guid.NewGuid(), Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("ownerId");
    }

    #endregion

    #region AddHouseRule Tests

    [Fact]
    public void AddHouseRule_UserAdded_AddsRuleWithCorrectSource()
    {
        // Arrange
        var memory = GameMemory.Create(Guid.NewGuid(), Guid.NewGuid());
        var description = "Roll dice to determine first player";

        // Act
        memory.AddHouseRule(description, HouseRuleSource.UserAdded);

        // Assert
        memory.HouseRules.Should().HaveCount(1);
        memory.HouseRules[0].Description.Should().Be(description);
        memory.HouseRules[0].Source.Should().Be(HouseRuleSource.UserAdded);
        memory.HouseRules[0].AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddHouseRule_DisputeOverride_AddsRuleWithCorrectSource()
    {
        // Arrange
        var memory = GameMemory.Create(Guid.NewGuid(), Guid.NewGuid());
        var description = "Tie goes to the youngest player";

        // Act
        memory.AddHouseRule(description, HouseRuleSource.DisputeOverride);

        // Assert
        memory.HouseRules.Should().HaveCount(1);
        memory.HouseRules[0].Source.Should().Be(HouseRuleSource.DisputeOverride);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddHouseRule_WithEmptyDescription_ThrowsArgumentException(string? description)
    {
        var memory = GameMemory.Create(Guid.NewGuid(), Guid.NewGuid());
        var act = () => memory.AddHouseRule(description!, HouseRuleSource.UserAdded);
        act.Should().Throw<ArgumentException>().WithParameterName("description");
    }

    #endregion

    #region AddNote Tests

    [Fact]
    public void AddNote_WithValidContent_AddsNote()
    {
        // Arrange
        var memory = GameMemory.Create(Guid.NewGuid(), Guid.NewGuid());
        var userId = Guid.NewGuid();

        // Act
        memory.AddNote("Great for 4 players", userId);

        // Assert
        memory.Notes.Should().HaveCount(1);
        memory.Notes[0].Content.Should().Be("Great for 4 players");
        memory.Notes[0].AddedByUserId.Should().Be(userId);
        memory.Notes[0].AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddNote_WithNullUserId_AddsNoteWithoutUser()
    {
        var memory = GameMemory.Create(Guid.NewGuid(), Guid.NewGuid());

        memory.AddNote("Anonymous note", null);

        memory.Notes.Should().HaveCount(1);
        memory.Notes[0].AddedByUserId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddNote_WithEmptyContent_ThrowsArgumentException(string? content)
    {
        var memory = GameMemory.Create(Guid.NewGuid(), Guid.NewGuid());
        var act = () => memory.AddNote(content!, Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    #endregion

    #region SetCustomSetup Tests

    [Fact]
    public void SetCustomSetup_WithValidData_SetsSetup()
    {
        // Arrange
        var memory = GameMemory.Create(Guid.NewGuid(), Guid.NewGuid());
        var setup = new SetupChecklistData(
            4,
            new List<SetupComponent> { new("Board", 1) },
            new List<SetupStep> { new(1, "Shuffle cards") });

        // Act
        memory.SetCustomSetup(setup);

        // Assert
        memory.CustomSetup.Should().NotBeNull();
        memory.CustomSetup!.PlayerCount.Should().Be(4);
    }

    [Fact]
    public void SetCustomSetup_WithNull_ThrowsArgumentNullException()
    {
        var memory = GameMemory.Create(Guid.NewGuid(), Guid.NewGuid());
        var act = () => memory.SetCustomSetup(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}

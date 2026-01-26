using Api.BoundedContexts.UserLibrary.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Tests for the GameChecklist entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 20
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameChecklistTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_CreatesChecklist()
    {
        // Arrange
        var entryId = Guid.NewGuid();

        // Act
        var checklist = GameChecklist.Create(
            userLibraryEntryId: entryId,
            description: "Shuffle the deck",
            displayOrder: 0,
            additionalInfo: "Make sure to shuffle thoroughly");

        // Assert
        checklist.Id.Should().NotBe(Guid.Empty);
        checklist.UserLibraryEntryId.Should().Be(entryId);
        checklist.Description.Should().Be("Shuffle the deck");
        checklist.DisplayOrder.Should().Be(0);
        checklist.AdditionalInfo.Should().Be("Make sure to shuffle thoroughly");
        checklist.IsCompleted.Should().BeFalse();
        checklist.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        checklist.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithoutAdditionalInfo_CreatesChecklistWithNullInfo()
    {
        // Act
        var checklist = GameChecklist.Create(
            userLibraryEntryId: Guid.NewGuid(),
            description: "Deal cards",
            displayOrder: 1);

        // Assert
        checklist.AdditionalInfo.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyUserLibraryEntryId_ThrowsArgumentException()
    {
        // Act
        var action = () => GameChecklist.Create(
            userLibraryEntryId: Guid.Empty,
            description: "Test step",
            displayOrder: 0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("userLibraryEntryId")
            .WithMessage("*UserLibraryEntryId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyDescription_ThrowsArgumentException()
    {
        // Act
        var action = () => GameChecklist.Create(
            userLibraryEntryId: Guid.NewGuid(),
            description: "",
            displayOrder: 0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceDescription_ThrowsArgumentException()
    {
        // Act
        var action = () => GameChecklist.Create(
            userLibraryEntryId: Guid.NewGuid(),
            description: "   ",
            displayOrder: 0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Description cannot be empty*");
    }

    [Fact]
    public void Create_WithNegativeDisplayOrder_ThrowsArgumentException()
    {
        // Act
        var action = () => GameChecklist.Create(
            userLibraryEntryId: Guid.NewGuid(),
            description: "Test step",
            displayOrder: -1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("displayOrder")
            .WithMessage("*DisplayOrder cannot be negative*");
    }

    [Fact]
    public void Create_WithZeroDisplayOrder_Succeeds()
    {
        // Act
        var checklist = GameChecklist.Create(
            userLibraryEntryId: Guid.NewGuid(),
            description: "First step",
            displayOrder: 0);

        // Assert
        checklist.DisplayOrder.Should().Be(0);
    }

    [Fact]
    public void Create_TrimsDescription()
    {
        // Act
        var checklist = GameChecklist.Create(
            userLibraryEntryId: Guid.NewGuid(),
            description: "  Shuffle cards  ",
            displayOrder: 0);

        // Assert
        checklist.Description.Should().Be("Shuffle cards");
    }

    [Fact]
    public void Create_TrimsAdditionalInfo()
    {
        // Act
        var checklist = GameChecklist.Create(
            userLibraryEntryId: Guid.NewGuid(),
            description: "Shuffle cards",
            displayOrder: 0,
            additionalInfo: "  Extra info  ");

        // Assert
        checklist.AdditionalInfo.Should().Be("Extra info");
    }

    #endregion

    #region MarkAsCompleted Tests

    [Fact]
    public void MarkAsCompleted_WhenNotCompleted_SetsIsCompletedTrue()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);

        // Act
        checklist.MarkAsCompleted();

        // Assert
        checklist.IsCompleted.Should().BeTrue();
        checklist.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkAsCompleted_WhenAlreadyCompleted_DoesNotChangeUpdatedAt()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);
        checklist.MarkAsCompleted();
        var originalUpdatedAt = checklist.UpdatedAt;

        // Wait a bit to ensure time difference
        Thread.Sleep(10);

        // Act
        checklist.MarkAsCompleted();

        // Assert
        checklist.IsCompleted.Should().BeTrue();
        checklist.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    #endregion

    #region MarkAsIncomplete Tests

    [Fact]
    public void MarkAsIncomplete_WhenCompleted_SetsIsCompletedFalse()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);
        checklist.MarkAsCompleted();

        // Act
        checklist.MarkAsIncomplete();

        // Assert
        checklist.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void MarkAsIncomplete_WhenAlreadyIncomplete_DoesNotChangeUpdatedAt()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);

        // Act
        checklist.MarkAsIncomplete();

        // Assert
        checklist.IsCompleted.Should().BeFalse();
        checklist.UpdatedAt.Should().BeNull(); // Never updated
    }

    #endregion

    #region Toggle Tests

    [Fact]
    public void Toggle_WhenNotCompleted_SetsIsCompletedTrue()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);

        // Act
        checklist.Toggle();

        // Assert
        checklist.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void Toggle_WhenCompleted_SetsIsCompletedFalse()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);
        checklist.MarkAsCompleted();

        // Act
        checklist.Toggle();

        // Assert
        checklist.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void Toggle_AlwaysUpdatesUpdatedAt()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);

        // Act
        checklist.Toggle();

        // Assert
        checklist.UpdatedAt.Should().NotBeNull();
    }

    #endregion

    #region UpdateDescription Tests

    [Fact]
    public void UpdateDescription_WithValidDescription_UpdatesDescription()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Original step", 0);

        // Act
        checklist.UpdateDescription("Updated step");

        // Assert
        checklist.Description.Should().Be("Updated step");
        checklist.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateDescription_WithEmptyDescription_ThrowsArgumentException()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Original step", 0);

        // Act
        var action = () => checklist.UpdateDescription("");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description cannot be empty*");
    }

    [Fact]
    public void UpdateDescription_TrimsDescription()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Original step", 0);

        // Act
        checklist.UpdateDescription("  Updated step  ");

        // Assert
        checklist.Description.Should().Be("Updated step");
    }

    #endregion

    #region UpdateOrder Tests

    [Fact]
    public void UpdateOrder_WithValidOrder_UpdatesDisplayOrder()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);

        // Act
        checklist.UpdateOrder(5);

        // Assert
        checklist.DisplayOrder.Should().Be(5);
        checklist.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateOrder_WithNegativeOrder_ThrowsArgumentException()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);

        // Act
        var action = () => checklist.UpdateOrder(-1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("displayOrder")
            .WithMessage("*DisplayOrder cannot be negative*");
    }

    #endregion

    #region UpdateAdditionalInfo Tests

    [Fact]
    public void UpdateAdditionalInfo_WithValue_UpdatesInfo()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);

        // Act
        checklist.UpdateAdditionalInfo("New info");

        // Assert
        checklist.AdditionalInfo.Should().Be("New info");
        checklist.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateAdditionalInfo_WithNull_ClearsInfo()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0, "Original info");

        // Act
        checklist.UpdateAdditionalInfo(null);

        // Assert
        checklist.AdditionalInfo.Should().BeNull();
    }

    [Fact]
    public void UpdateAdditionalInfo_TrimsValue()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);

        // Act
        checklist.UpdateAdditionalInfo("  Trimmed info  ");

        // Assert
        checklist.AdditionalInfo.Should().Be("Trimmed info");
    }

    #endregion

    #region ResetCompletion Tests

    [Fact]
    public void ResetCompletion_SetsIsCompletedFalse()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);
        checklist.MarkAsCompleted();

        // Act
        checklist.ResetCompletion();

        // Assert
        checklist.IsCompleted.Should().BeFalse();
        checklist.UpdatedAt.Should().NotBeNull();
    }

    #endregion

    #region MoveUp Tests

    [Fact]
    public void MoveUp_WhenDisplayOrderGreaterThanZero_DecrementsOrder()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 5);

        // Act
        checklist.MoveUp();

        // Assert
        checklist.DisplayOrder.Should().Be(4);
        checklist.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MoveUp_WhenDisplayOrderIsZero_DoesNotDecrement()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);

        // Act
        checklist.MoveUp();

        // Assert
        checklist.DisplayOrder.Should().Be(0);
        checklist.UpdatedAt.Should().BeNull(); // Not updated because no change
    }

    #endregion

    #region MoveDown Tests

    [Fact]
    public void MoveDown_IncrementsDisplayOrder()
    {
        // Arrange
        var checklist = GameChecklist.Create(
            Guid.NewGuid(), "Test step", 0);

        // Act
        checklist.MoveDown();

        // Assert
        checklist.DisplayOrder.Should().Be(1);
        checklist.UpdatedAt.Should().NotBeNull();
    }

    #endregion
}

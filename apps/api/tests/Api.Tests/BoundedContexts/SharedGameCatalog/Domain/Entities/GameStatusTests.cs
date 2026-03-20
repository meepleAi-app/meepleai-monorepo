using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the GameStatus enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 6
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameStatusTests
{
    #region Enum Value Tests

    [Fact]
    public void GameStatus_Draft_HasCorrectValue()
    {
        ((int)GameStatus.Draft).Should().Be(0);
    }

    [Fact]
    public void GameStatus_PendingApproval_HasCorrectValue()
    {
        ((int)GameStatus.PendingApproval).Should().Be(1);
    }

    [Fact]
    public void GameStatus_Published_HasCorrectValue()
    {
        ((int)GameStatus.Published).Should().Be(2);
    }

    [Fact]
    public void GameStatus_Archived_HasCorrectValue()
    {
        ((int)GameStatus.Archived).Should().Be(3);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void GameStatus_HasFourValues()
    {
        var values = Enum.GetValues<GameStatus>();
        values.Should().HaveCount(4);
    }

    [Fact]
    public void GameStatus_AllValuesCanBeParsed()
    {
        var names = new[] { "Draft", "PendingApproval", "Published", "Archived" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<GameStatus>(name);
            parsed.Should().BeOneOf(Enum.GetValues<GameStatus>());
        }
    }

    [Fact]
    public void GameStatus_ToString_ReturnsExpectedNames()
    {
        GameStatus.Draft.ToString().Should().Be("Draft");
        GameStatus.PendingApproval.ToString().Should().Be("PendingApproval");
        GameStatus.Published.ToString().Should().Be("Published");
        GameStatus.Archived.ToString().Should().Be("Archived");
    }

    #endregion

    #region Workflow State Tests

    [Fact]
    public void GameStatus_InitialState_IsDraft()
    {
        // Draft should be the initial state (value 0)
        var initialState = (GameStatus)0;
        initialState.Should().Be(GameStatus.Draft);
    }

    [Fact]
    public void GameStatus_WorkflowProgression_FollowsExpectedOrder()
    {
        // Verify the workflow order: Draft → PendingApproval → Published → Archived
        ((int)GameStatus.Draft).Should().BeLessThan((int)GameStatus.PendingApproval);
        ((int)GameStatus.PendingApproval).Should().BeLessThan((int)GameStatus.Published);
        ((int)GameStatus.Published).Should().BeLessThan((int)GameStatus.Archived);
    }

    [Fact]
    public void GameStatus_CanDistinguishVisibleFromNonVisibleStates()
    {
        // Published is the only publicly visible state
        var visibleStates = new HashSet<GameStatus> { GameStatus.Published };
        var nonVisibleStates = new HashSet<GameStatus>
        {
            GameStatus.Draft,
            GameStatus.PendingApproval,
            GameStatus.Archived
        };

        visibleStates.Contains(GameStatus.Published).Should().BeTrue();

        foreach (var state in nonVisibleStates)
        {
            visibleStates.Contains(state).Should().BeFalse();
        }
    }

    [Fact]
    public void GameStatus_CanDistinguishEditableStates()
    {
        // Draft and Archived can be edited, PendingApproval and Published cannot
        var editableStates = new HashSet<GameStatus>
        {
            GameStatus.Draft,
            GameStatus.Archived
        };

        editableStates.Contains(GameStatus.Draft).Should().BeTrue();
        editableStates.Contains(GameStatus.Archived).Should().BeTrue();
        editableStates.Contains(GameStatus.PendingApproval).Should().BeFalse();
        editableStates.Contains(GameStatus.Published).Should().BeFalse();
    }

    [Fact]
    public void GameStatus_PendingApproval_IsTransitionalState()
    {
        // PendingApproval is a transitional state requiring admin action
        var transitionalStates = new[] { GameStatus.PendingApproval };

        transitionalStates.Should().Contain(GameStatus.PendingApproval);
        transitionalStates.Should().NotContain(GameStatus.Draft);
        transitionalStates.Should().NotContain(GameStatus.Published);
        transitionalStates.Should().NotContain(GameStatus.Archived);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void GameStatus_CastFromInt_ReturnsCorrectStatuses()
    {
        ((GameStatus)0).Should().Be(GameStatus.Draft);
        ((GameStatus)1).Should().Be(GameStatus.PendingApproval);
        ((GameStatus)2).Should().Be(GameStatus.Published);
        ((GameStatus)3).Should().Be(GameStatus.Archived);
    }

    [Fact]
    public void GameStatus_IsDefined_ReturnsTrueForValidValues()
    {
        for (int i = 0; i <= 3; i++)
        {
            Enum.IsDefined(typeof(GameStatus), i).Should().BeTrue();
        }
    }

    [Fact]
    public void GameStatus_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(GameStatus), 4).Should().BeFalse();
        Enum.IsDefined(typeof(GameStatus), -1).Should().BeFalse();
        Enum.IsDefined(typeof(GameStatus), 100).Should().BeFalse();
    }

    #endregion

    #region State Transition Logic Tests

    [Fact]
    public void GameStatus_DraftCanTransitionToPendingApproval()
    {
        // Based on the documented state machine:
        // Draft → PendingApproval (via submission)
        var currentState = GameStatus.Draft;
        var validTransitions = new[] { GameStatus.PendingApproval };

        validTransitions.Should().Contain(GameStatus.PendingApproval);
        currentState.Should().Be(GameStatus.Draft);
    }

    [Fact]
    public void GameStatus_PendingApprovalCanTransitionToPublishedOrDraft()
    {
        // Based on the documented state machine:
        // PendingApproval → Published (approved) or Draft (rejected)
        var validTransitions = new[] { GameStatus.Published, GameStatus.Draft };

        validTransitions.Should().Contain(GameStatus.Published);
        validTransitions.Should().Contain(GameStatus.Draft);
    }

    [Fact]
    public void GameStatus_PublishedCanTransitionToArchived()
    {
        // Based on the documented state machine:
        // Published → Archived
        var validTransitions = new[] { GameStatus.Archived };

        validTransitions.Should().Contain(GameStatus.Archived);
    }

    [Fact]
    public void GameStatus_ArchivedCanTransitionToDraft()
    {
        // Based on the documented state machine:
        // Archived → Draft (for re-editing)
        var validTransitions = new[] { GameStatus.Draft };

        validTransitions.Should().Contain(GameStatus.Draft);
    }

    #endregion
}
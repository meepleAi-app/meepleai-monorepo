using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the ShareRequestStatus enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 5
/// </summary>
[Trait("Category", "Unit")]
public sealed class ShareRequestStatusTests
{
    #region Enum Value Tests

    [Fact]
    public void ShareRequestStatus_Pending_HasCorrectValue()
    {
        ((int)ShareRequestStatus.Pending).Should().Be(0);
    }

    [Fact]
    public void ShareRequestStatus_InReview_HasCorrectValue()
    {
        ((int)ShareRequestStatus.InReview).Should().Be(1);
    }

    [Fact]
    public void ShareRequestStatus_ChangesRequested_HasCorrectValue()
    {
        ((int)ShareRequestStatus.ChangesRequested).Should().Be(2);
    }

    [Fact]
    public void ShareRequestStatus_Approved_HasCorrectValue()
    {
        ((int)ShareRequestStatus.Approved).Should().Be(3);
    }

    [Fact]
    public void ShareRequestStatus_Rejected_HasCorrectValue()
    {
        ((int)ShareRequestStatus.Rejected).Should().Be(4);
    }

    [Fact]
    public void ShareRequestStatus_Withdrawn_HasCorrectValue()
    {
        ((int)ShareRequestStatus.Withdrawn).Should().Be(5);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void ShareRequestStatus_HasSixValues()
    {
        var values = Enum.GetValues<ShareRequestStatus>();
        values.Should().HaveCount(6);
    }

    [Fact]
    public void ShareRequestStatus_AllValuesCanBeParsed()
    {
        var names = new[] { "Pending", "InReview", "ChangesRequested", "Approved", "Rejected", "Withdrawn" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<ShareRequestStatus>(name);
            parsed.Should().BeOneOf(Enum.GetValues<ShareRequestStatus>());
        }
    }

    [Fact]
    public void ShareRequestStatus_ToString_ReturnsExpectedNames()
    {
        ShareRequestStatus.Pending.ToString().Should().Be("Pending");
        ShareRequestStatus.InReview.ToString().Should().Be("InReview");
        ShareRequestStatus.ChangesRequested.ToString().Should().Be("ChangesRequested");
        ShareRequestStatus.Approved.ToString().Should().Be("Approved");
        ShareRequestStatus.Rejected.ToString().Should().Be("Rejected");
        ShareRequestStatus.Withdrawn.ToString().Should().Be("Withdrawn");
    }

    #endregion

    #region Workflow State Tests

    [Fact]
    public void ShareRequestStatus_InitialState_IsPending()
    {
        // Pending should be the initial state (value 0)
        var initialState = (ShareRequestStatus)0;
        initialState.Should().Be(ShareRequestStatus.Pending);
    }

    [Fact]
    public void ShareRequestStatus_TerminalStates_AreCorrectlyIdentified()
    {
        // Terminal states are Approved, Rejected, and Withdrawn
        var terminalStates = new[]
        {
            ShareRequestStatus.Approved,
            ShareRequestStatus.Rejected,
            ShareRequestStatus.Withdrawn
        };

        var activeStates = new[]
        {
            ShareRequestStatus.Pending,
            ShareRequestStatus.InReview,
            ShareRequestStatus.ChangesRequested
        };

        foreach (var state in terminalStates)
        {
            // Terminal states should have higher values than initial state
            ((int)state).Should().BeGreaterThan((int)ShareRequestStatus.Pending);
        }

        foreach (var state in activeStates)
        {
            // Active states should be less than all terminal states (excluding Approved which is 3)
            ((int)state).Should().BeLessThan((int)ShareRequestStatus.Rejected);
        }
    }

    [Fact]
    public void ShareRequestStatus_CanDistinguishBetweenActiveAndTerminalStates()
    {
        // Define terminal states (no further transitions allowed)
        var terminalStatuses = new HashSet<ShareRequestStatus>
        {
            ShareRequestStatus.Approved,
            ShareRequestStatus.Rejected,
            ShareRequestStatus.Withdrawn
        };

        // Verify Pending is not terminal
        terminalStatuses.Contains(ShareRequestStatus.Pending).Should().BeFalse();

        // Verify InReview is not terminal
        terminalStatuses.Contains(ShareRequestStatus.InReview).Should().BeFalse();

        // Verify ChangesRequested is not terminal
        terminalStatuses.Contains(ShareRequestStatus.ChangesRequested).Should().BeFalse();

        // Verify Approved is terminal
        terminalStatuses.Contains(ShareRequestStatus.Approved).Should().BeTrue();

        // Verify Rejected is terminal
        terminalStatuses.Contains(ShareRequestStatus.Rejected).Should().BeTrue();

        // Verify Withdrawn is terminal
        terminalStatuses.Contains(ShareRequestStatus.Withdrawn).Should().BeTrue();
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ShareRequestStatus_CastFromInt_ReturnsCorrectStatuses()
    {
        ((ShareRequestStatus)0).Should().Be(ShareRequestStatus.Pending);
        ((ShareRequestStatus)1).Should().Be(ShareRequestStatus.InReview);
        ((ShareRequestStatus)2).Should().Be(ShareRequestStatus.ChangesRequested);
        ((ShareRequestStatus)3).Should().Be(ShareRequestStatus.Approved);
        ((ShareRequestStatus)4).Should().Be(ShareRequestStatus.Rejected);
        ((ShareRequestStatus)5).Should().Be(ShareRequestStatus.Withdrawn);
    }

    [Fact]
    public void ShareRequestStatus_IsDefined_ReturnsTrueForValidValues()
    {
        for (int i = 0; i <= 5; i++)
        {
            Enum.IsDefined(typeof(ShareRequestStatus), i).Should().BeTrue();
        }
    }

    [Fact]
    public void ShareRequestStatus_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(ShareRequestStatus), 6).Should().BeFalse();
        Enum.IsDefined(typeof(ShareRequestStatus), -1).Should().BeFalse();
        Enum.IsDefined(typeof(ShareRequestStatus), 100).Should().BeFalse();
    }

    #endregion
}

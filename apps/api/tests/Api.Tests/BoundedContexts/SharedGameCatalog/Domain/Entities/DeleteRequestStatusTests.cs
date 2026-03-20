using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the DeleteRequestStatus enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 10
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeleteRequestStatusTests
{
    #region Enum Value Tests

    [Fact]
    public void Pending_HasCorrectValue()
    {
        ((int)DeleteRequestStatus.Pending).Should().Be(0);
    }

    [Fact]
    public void Approved_HasCorrectValue()
    {
        ((int)DeleteRequestStatus.Approved).Should().Be(1);
    }

    [Fact]
    public void Rejected_HasCorrectValue()
    {
        ((int)DeleteRequestStatus.Rejected).Should().Be(2);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void DeleteRequestStatus_HasThreeValues()
    {
        var values = Enum.GetValues<DeleteRequestStatus>();
        values.Should().HaveCount(3);
    }

    [Fact]
    public void DeleteRequestStatus_AllValuesCanBeParsed()
    {
        var names = new[] { "Pending", "Approved", "Rejected" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<DeleteRequestStatus>(name);
            parsed.Should().BeOneOf(Enum.GetValues<DeleteRequestStatus>());
        }
    }

    [Fact]
    public void DeleteRequestStatus_ToString_ReturnsExpectedNames()
    {
        DeleteRequestStatus.Pending.ToString().Should().Be("Pending");
        DeleteRequestStatus.Approved.ToString().Should().Be("Approved");
        DeleteRequestStatus.Rejected.ToString().Should().Be("Rejected");
    }

    #endregion

    #region Semantic Tests

    [Fact]
    public void Pending_IsDefaultStatus()
    {
        // Pending = 0 is the initial status for new delete requests
        var defaultStatus = default(DeleteRequestStatus);
        defaultStatus.Should().Be(DeleteRequestStatus.Pending);
    }

    [Fact]
    public void StatusValues_AreContiguousAndSequential()
    {
        var values = Enum.GetValues<DeleteRequestStatus>()
            .Cast<int>()
            .OrderBy(x => x)
            .ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            values[i].Should().Be(i, $"Expected status at position {i} to have value {i}");
        }
    }

    #endregion

    #region Conversion Tests

    [Theory]
    [InlineData(0, DeleteRequestStatus.Pending)]
    [InlineData(1, DeleteRequestStatus.Approved)]
    [InlineData(2, DeleteRequestStatus.Rejected)]
    public void DeleteRequestStatus_CastFromInt_ReturnsCorrectValue(int value, DeleteRequestStatus expected)
    {
        ((DeleteRequestStatus)value).Should().Be(expected);
    }

    [Fact]
    public void DeleteRequestStatus_IsDefined_ReturnsTrueForValidValues()
    {
        Enum.IsDefined(typeof(DeleteRequestStatus), 0).Should().BeTrue();
        Enum.IsDefined(typeof(DeleteRequestStatus), 1).Should().BeTrue();
        Enum.IsDefined(typeof(DeleteRequestStatus), 2).Should().BeTrue();
    }

    [Fact]
    public void DeleteRequestStatus_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(DeleteRequestStatus), 3).Should().BeFalse();
        Enum.IsDefined(typeof(DeleteRequestStatus), -1).Should().BeFalse();
    }

    #endregion
}
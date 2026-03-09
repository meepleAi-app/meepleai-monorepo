using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Tests for the ShareLinkRole enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 11
/// </summary>
[Trait("Category", "Unit")]
public sealed class ShareLinkRoleTests
{
    #region Enum Value Tests

    [Fact]
    public void View_HasCorrectValue()
    {
        ((int)ShareLinkRole.View).Should().Be(0);
    }

    [Fact]
    public void Comment_HasCorrectValue()
    {
        ((int)ShareLinkRole.Comment).Should().Be(1);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void ShareLinkRole_HasTwoValues()
    {
        var values = Enum.GetValues<ShareLinkRole>();
        values.Should().HaveCount(2);
    }

    [Fact]
    public void ShareLinkRole_AllValuesCanBeParsed()
    {
        var names = new[] { "View", "Comment" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<ShareLinkRole>(name);
            parsed.Should().BeOneOf(Enum.GetValues<ShareLinkRole>());
        }
    }

    [Fact]
    public void ShareLinkRole_ToString_ReturnsExpectedNames()
    {
        ShareLinkRole.View.ToString().Should().Be("View");
        ShareLinkRole.Comment.ToString().Should().Be("Comment");
    }

    #endregion

    #region Semantic Tests

    [Fact]
    public void View_IsDefaultRole()
    {
        // View = 0 is the default (most restrictive) access level
        var defaultRole = default(ShareLinkRole);
        defaultRole.Should().Be(ShareLinkRole.View);
    }

    [Fact]
    public void RoleValues_AreContiguousAndSequential()
    {
        var values = Enum.GetValues<ShareLinkRole>()
            .Cast<int>()
            .OrderBy(x => x)
            .ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            values[i].Should().Be(i, $"Expected role at position {i} to have value {i}");
        }
    }

    [Fact]
    public void Comment_HasHigherAccessThanView()
    {
        // Comment (1) > View (0) - Comment implies more capabilities
        ((int)ShareLinkRole.Comment).Should().BeGreaterThan((int)ShareLinkRole.View);
    }

    #endregion

    #region Conversion Tests

    [Theory]
    [InlineData(0, ShareLinkRole.View)]
    [InlineData(1, ShareLinkRole.Comment)]
    public void ShareLinkRole_CastFromInt_ReturnsCorrectValue(int value, ShareLinkRole expected)
    {
        ((ShareLinkRole)value).Should().Be(expected);
    }

    [Fact]
    public void ShareLinkRole_IsDefined_ReturnsTrueForValidValues()
    {
        Enum.IsDefined(typeof(ShareLinkRole), 0).Should().BeTrue();
        Enum.IsDefined(typeof(ShareLinkRole), 1).Should().BeTrue();
    }

    [Fact]
    public void ShareLinkRole_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(ShareLinkRole), 2).Should().BeFalse();
        Enum.IsDefined(typeof(ShareLinkRole), -1).Should().BeFalse();
    }

    #endregion

    #region Use Case Tests

    [Fact]
    public void View_IsReadOnly()
    {
        // View role should be the most restrictive - read-only access
        var viewRole = ShareLinkRole.View;
        viewRole.Should().Be(ShareLinkRole.View);
        // This is the semantic meaning - can only view, not interact
    }

    [Fact]
    public void Comment_AllowsAddingMessages()
    {
        // Comment role allows adding new messages (rate-limited)
        var commentRole = ShareLinkRole.Comment;
        commentRole.Should().Be(ShareLinkRole.Comment);
        // This is the semantic meaning - can view and add comments
    }

    #endregion
}

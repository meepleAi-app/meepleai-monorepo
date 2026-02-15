using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserNotifications.Domain;

[Trait("Category", TestCategories.Unit)]
public sealed class EmailQueueStatusTests
{
    [Theory]
    [InlineData("pending")]
    [InlineData("processing")]
    [InlineData("sent")]
    [InlineData("failed")]
    [InlineData("dead_letter")]
    public void FromString_ValidStatus_ReturnsCorrectStatus(string value)
    {
        var status = EmailQueueStatus.FromString(value);
        status.Value.Should().Be(value);
    }

    [Fact]
    public void FromString_InvalidStatus_ThrowsArgumentException()
    {
        var act = () => EmailQueueStatus.FromString("invalid");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsPending_ReturnsTrueForPendingStatus()
    {
        EmailQueueStatus.Pending.IsPending.Should().BeTrue();
        EmailQueueStatus.Sent.IsPending.Should().BeFalse();
    }

    [Fact]
    public void IsSent_ReturnsTrueForSentStatus()
    {
        EmailQueueStatus.Sent.IsSent.Should().BeTrue();
        EmailQueueStatus.Pending.IsSent.Should().BeFalse();
    }

    [Fact]
    public void IsDeadLetter_ReturnsTrueForDeadLetterStatus()
    {
        EmailQueueStatus.DeadLetter.IsDeadLetter.Should().BeTrue();
        EmailQueueStatus.Sent.IsDeadLetter.Should().BeFalse();
    }

    [Fact]
    public void Equality_SameStatus_AreEqual()
    {
        var a = EmailQueueStatus.FromString("pending");
        var b = EmailQueueStatus.FromString("pending");
        a.Should().Be(b);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        EmailQueueStatus.Pending.ToString().Should().Be("pending");
    }
}

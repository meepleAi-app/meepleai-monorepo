using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities.SessionInvite;

/// <summary>
/// Unit tests for SessionInvite domain entity.
/// E3-1: Session Invite Flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SessionInviteTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldGeneratePinAndLinkToken()
    {
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 10);

        invite.Id.Should().NotBe(Guid.Empty);
        invite.SessionId.Should().Be(SessionId);
        invite.CreatedByUserId.Should().Be(UserId);
        invite.Pin.Should().NotBeNull();
        invite.Pin.Length.Should().Be(6);
        invite.LinkToken.Should().NotBeNull();
        invite.LinkToken.Length.Should().Be(32); // Guid without hyphens
        invite.MaxUses.Should().Be(10);
        invite.CurrentUses.Should().Be(0);
        (invite.IsRevoked).Should().BeFalse();
        (invite.IsUsable).Should().BeTrue();
    }

    [Fact]
    public void Create_WithExpiry_ShouldSetExpiresAt()
    {
        var before = DateTime.UtcNow;
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 5, expiryMinutes: 60);
        var after = DateTime.UtcNow;

        (invite.ExpiresAt >= before.AddMinutes(60)).Should().BeTrue();
        (invite.ExpiresAt <= after.AddMinutes(60).AddSeconds(1)).Should().BeTrue();
        (invite.CreatedAt >= before).Should().BeTrue();
        (invite.CreatedAt <= after).Should().BeTrue();
    }

    [Fact]
    public void Create_WithInvalidSessionId_ShouldThrow()
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(Guid.Empty, UserId, 10);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithInvalidUserId_ShouldThrow()
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, Guid.Empty, 10);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithZeroMaxUses_ShouldThrow()
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithZeroExpiry_ShouldThrow()
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 10, expiryMinutes: 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordUse_ShouldIncrementCount()
    {
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 5);

        invite.RecordUse();
        invite.CurrentUses.Should().Be(1);

        invite.RecordUse();
        invite.CurrentUses.Should().Be(2);
    }

    [Fact]
    public void RecordUse_WhenMaxReached_ShouldThrow()
    {
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 1);
        invite.RecordUse(); // Uses up the single use

        var act = () => invite.RecordUse();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IsUsable_WhenRevoked_ReturnsFalse()
    {
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 10);
        (invite.IsUsable).Should().BeTrue();

        invite.Revoke();

        (invite.IsRevoked).Should().BeTrue();
        (invite.IsUsable).Should().BeFalse();
    }

    [Fact]
    public void RecordUse_WhenRevoked_ShouldThrow()
    {
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 10);
        invite.Revoke();

        var act = () => invite.RecordUse();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Pin_ShouldOnlyContainAllowedCharacters()
    {
        const string allowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 10);

        foreach (var c in invite.Pin)
        {
            allowedChars.Should().Contain(c.ToString());
        }
    }
}

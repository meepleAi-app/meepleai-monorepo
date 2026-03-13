using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

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

        Assert.NotEqual(Guid.Empty, invite.Id);
        Assert.Equal(SessionId, invite.SessionId);
        Assert.Equal(UserId, invite.CreatedByUserId);
        Assert.NotNull(invite.Pin);
        Assert.Equal(6, invite.Pin.Length);
        Assert.NotNull(invite.LinkToken);
        Assert.Equal(32, invite.LinkToken.Length); // Guid without hyphens
        Assert.Equal(10, invite.MaxUses);
        Assert.Equal(0, invite.CurrentUses);
        Assert.False(invite.IsRevoked);
        Assert.True(invite.IsUsable);
    }

    [Fact]
    public void Create_WithExpiry_ShouldSetExpiresAt()
    {
        var before = DateTime.UtcNow;
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 5, expiryMinutes: 60);
        var after = DateTime.UtcNow;

        Assert.True(invite.ExpiresAt >= before.AddMinutes(60));
        Assert.True(invite.ExpiresAt <= after.AddMinutes(60).AddSeconds(1));
        Assert.True(invite.CreatedAt >= before);
        Assert.True(invite.CreatedAt <= after);
    }

    [Fact]
    public void Create_WithInvalidSessionId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(Guid.Empty, UserId, 10));
    }

    [Fact]
    public void Create_WithInvalidUserId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, Guid.Empty, 10));
    }

    [Fact]
    public void Create_WithZeroMaxUses_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 0));
    }

    [Fact]
    public void Create_WithZeroExpiry_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 10, expiryMinutes: 0));
    }

    [Fact]
    public void RecordUse_ShouldIncrementCount()
    {
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 5);

        invite.RecordUse();
        Assert.Equal(1, invite.CurrentUses);

        invite.RecordUse();
        Assert.Equal(2, invite.CurrentUses);
    }

    [Fact]
    public void RecordUse_WhenMaxReached_ShouldThrow()
    {
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 1);
        invite.RecordUse(); // Uses up the single use

        Assert.Throws<InvalidOperationException>(() => invite.RecordUse());
    }

    [Fact]
    public void IsUsable_WhenRevoked_ReturnsFalse()
    {
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 10);
        Assert.True(invite.IsUsable);

        invite.Revoke();

        Assert.True(invite.IsRevoked);
        Assert.False(invite.IsUsable);
    }

    [Fact]
    public void RecordUse_WhenRevoked_ShouldThrow()
    {
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 10);
        invite.Revoke();

        Assert.Throws<InvalidOperationException>(() => invite.RecordUse());
    }

    [Fact]
    public void Pin_ShouldOnlyContainAllowedCharacters()
    {
        const string allowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var invite = Api.BoundedContexts.GameManagement.Domain.Entities.SessionInvite.Create(SessionId, UserId, 10);

        foreach (var c in invite.Pin)
        {
            Assert.Contains(c, allowedChars);
        }
    }
}

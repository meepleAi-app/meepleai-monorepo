using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SessionParticipantTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private const string ValidGuestName = "Alice";

    #region CreateGuest

    [Fact]
    public void CreateGuest_ShouldSetGuestNameAndNullUserId()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, ValidGuestName, ParticipantRole.Player);

        Assert.NotEqual(Guid.Empty, participant.Id);
        Assert.Equal(_sessionId, participant.SessionId);
        Assert.Null(participant.UserId);
        Assert.Equal(ValidGuestName, participant.GuestName);
        Assert.Equal(ParticipantRole.Player, participant.Role);
        Assert.False(participant.AgentAccessEnabled);
        Assert.True(participant.JoinedAt <= DateTime.UtcNow);
        Assert.Null(participant.LeftAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateGuest_WithEmptyName_ShouldThrow(string? guestName)
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
                _sessionId, guestName!, ParticipantRole.Player));
    }

    [Fact]
    public void CreateGuest_ShouldTrimGuestName()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, "  Alice  ", ParticipantRole.Player);

        Assert.Equal("Alice", participant.GuestName);
    }

    #endregion

    #region CreateRegistered

    [Fact]
    public void CreateRegistered_ShouldSetUserIdAndNullGuestName()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Player);

        Assert.NotEqual(Guid.Empty, participant.Id);
        Assert.Equal(_sessionId, participant.SessionId);
        Assert.Equal(_userId, participant.UserId);
        Assert.Null(participant.GuestName);
        Assert.Equal(ParticipantRole.Player, participant.Role);
        Assert.True(participant.JoinedAt <= DateTime.UtcNow);
        Assert.Null(participant.LeftAt);
    }

    [Fact]
    public void CreateRegistered_AsHost_ShouldEnableAgentAccess()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Host);

        Assert.True(participant.AgentAccessEnabled);
    }

    [Fact]
    public void CreateRegistered_AsPlayer_ShouldDisableAgentAccess()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Player);

        Assert.False(participant.AgentAccessEnabled);
    }

    [Fact]
    public void CreateRegistered_AsSpectator_ShouldDisableAgentAccess()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Spectator);

        Assert.False(participant.AgentAccessEnabled);
    }

    #endregion

    #region AgentAccess

    [Fact]
    public void ToggleAgentAccess_ShouldFlip()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, ValidGuestName, ParticipantRole.Player);

        Assert.False(participant.AgentAccessEnabled);

        participant.EnableAgentAccess();
        Assert.True(participant.AgentAccessEnabled);

        participant.DisableAgentAccess();
        Assert.False(participant.AgentAccessEnabled);
    }

    #endregion

    #region Leave

    [Fact]
    public void Leave_ShouldSetLeftAt()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, ValidGuestName, ParticipantRole.Player);

        Assert.True(participant.IsActive);

        participant.Leave();

        Assert.NotNull(participant.LeftAt);
        Assert.True(participant.LeftAt <= DateTime.UtcNow);
        Assert.False(participant.IsActive);
    }

    #endregion

    #region ConnectionToken

    [Fact]
    public void ConnectionToken_ShouldBe6Chars()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, ValidGuestName, ParticipantRole.Player);

        Assert.Equal(6, participant.ConnectionToken.Length);
    }

    [Fact]
    public void ConnectionToken_ShouldNotContainAmbiguousChars()
    {
        // Generate multiple to increase coverage of random chars
        for (var i = 0; i < 50; i++)
        {
            var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
                _sessionId, ValidGuestName, ParticipantRole.Player);

            Assert.DoesNotContain("O", participant.ConnectionToken);
            Assert.DoesNotContain("0", participant.ConnectionToken);
            Assert.DoesNotContain("I", participant.ConnectionToken);
            Assert.DoesNotContain("1", participant.ConnectionToken);
            Assert.DoesNotContain("l", participant.ConnectionToken);
        }
    }

    [Fact]
    public void ConnectionToken_ShouldBeUniquePerParticipant()
    {
        var tokens = Enumerable.Range(0, 20)
            .Select(_ => Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
                _sessionId, ValidGuestName, ParticipantRole.Player).ConnectionToken)
            .ToHashSet();

        // With 6 chars from 32-char alphabet, collisions in 20 tokens are extremely unlikely
        Assert.True(tokens.Count > 15, "Expected most connection tokens to be unique");
    }

    #endregion

    #region DisplayName

    [Fact]
    public void DisplayName_Guest_ShouldReturnGuestName()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, ValidGuestName, ParticipantRole.Player);

        Assert.Equal(ValidGuestName, participant.DisplayName);
    }

    [Fact]
    public void DisplayName_RegisteredUser_ShouldReturnUserFallback()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Player);

        Assert.Equal("User", participant.DisplayName);
    }

    #endregion

    #region IsRegisteredUser

    [Fact]
    public void IsRegisteredUser_ShouldReflectUserId()
    {
        var guest = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, ValidGuestName, ParticipantRole.Player);
        var registered = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Player);

        Assert.False(guest.IsRegisteredUser);
        Assert.True(registered.IsRegisteredUser);
    }

    #endregion
}

using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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

        participant.Id.Should().NotBe(Guid.Empty);
        participant.SessionId.Should().Be(_sessionId);
        participant.UserId.Should().BeNull();
        participant.GuestName.Should().Be(ValidGuestName);
        participant.Role.Should().Be(ParticipantRole.Player);
        (participant.AgentAccessEnabled).Should().BeFalse();
        (participant.JoinedAt <= DateTime.UtcNow).Should().BeTrue();
        participant.LeftAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateGuest_WithEmptyName_ShouldThrow(string? guestName)
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
                _sessionId, guestName!, ParticipantRole.Player);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateGuest_ShouldTrimGuestName()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, "  Alice  ", ParticipantRole.Player);

        participant.GuestName.Should().Be("Alice");
    }

    #endregion

    #region CreateRegistered

    [Fact]
    public void CreateRegistered_ShouldSetUserIdAndNullGuestName()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Player);

        participant.Id.Should().NotBe(Guid.Empty);
        participant.SessionId.Should().Be(_sessionId);
        participant.UserId.Should().Be(_userId);
        participant.GuestName.Should().BeNull();
        participant.Role.Should().Be(ParticipantRole.Player);
        (participant.JoinedAt <= DateTime.UtcNow).Should().BeTrue();
        participant.LeftAt.Should().BeNull();
    }

    [Fact]
    public void CreateRegistered_AsHost_ShouldEnableAgentAccess()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Host);

        (participant.AgentAccessEnabled).Should().BeTrue();
    }

    [Fact]
    public void CreateRegistered_AsPlayer_ShouldDisableAgentAccess()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Player);

        (participant.AgentAccessEnabled).Should().BeFalse();
    }

    [Fact]
    public void CreateRegistered_AsSpectator_ShouldDisableAgentAccess()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Spectator);

        (participant.AgentAccessEnabled).Should().BeFalse();
    }

    #endregion

    #region AgentAccess

    [Fact]
    public void ToggleAgentAccess_ShouldFlip()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, ValidGuestName, ParticipantRole.Player);

        (participant.AgentAccessEnabled).Should().BeFalse();

        participant.EnableAgentAccess();
        (participant.AgentAccessEnabled).Should().BeTrue();

        participant.DisableAgentAccess();
        (participant.AgentAccessEnabled).Should().BeFalse();
    }

    #endregion

    #region Leave

    [Fact]
    public void Leave_ShouldSetLeftAt()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, ValidGuestName, ParticipantRole.Player);

        (participant.IsActive).Should().BeTrue();

        participant.Leave();

        participant.LeftAt.Should().NotBeNull();
        (participant.LeftAt <= DateTime.UtcNow).Should().BeTrue();
        (participant.IsActive).Should().BeFalse();
    }

    #endregion

    #region ConnectionToken

    [Fact]
    public void ConnectionToken_ShouldBe6Chars()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, ValidGuestName, ParticipantRole.Player);

        participant.ConnectionToken.Length.Should().Be(6);
    }

    [Fact]
    public void ConnectionToken_ShouldNotContainAmbiguousChars()
    {
        // Generate multiple to increase coverage of random chars
        for (var i = 0; i < 50; i++)
        {
            var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
                _sessionId, ValidGuestName, ParticipantRole.Player);

            participant.ConnectionToken.Should().NotContain("O");
            participant.ConnectionToken.Should().NotContain("0");
            participant.ConnectionToken.Should().NotContain("I");
            participant.ConnectionToken.Should().NotContain("1");
            participant.ConnectionToken.Should().NotContain("l");
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
        (tokens.Count > 15).Should().BeTrue("Expected most connection tokens to be unique");
    }

    #endregion

    #region DisplayName

    [Fact]
    public void DisplayName_Guest_ShouldReturnGuestName()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateGuest(
            _sessionId, ValidGuestName, ParticipantRole.Player);

        participant.DisplayName.Should().Be(ValidGuestName);
    }

    [Fact]
    public void DisplayName_RegisteredUser_ShouldReturnUserFallback()
    {
        var participant = Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipant.CreateRegistered(
            _sessionId, _userId, ParticipantRole.Player);

        participant.DisplayName.Should().Be("User");
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

        (guest.IsRegisteredUser).Should().BeFalse();
        (registered.IsRegisteredUser).Should().BeTrue();
    }

    #endregion
}

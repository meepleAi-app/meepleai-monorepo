using System.Security.Claims;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.Hubs;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Hubs;

/// <summary>
/// #4962: <see cref="GameStateHub"/> is reachable by any connection (<c>[AllowAnonymous]</c>), so it must judge
/// the connection's own user. Before: <c>JoinSession</c> put anyone in any session group, and a dozen methods
/// relayed a client payload into any group (a fake verdict on the Arbitro tab, a fake pause, a forged score).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4962")]
public sealed class GameStateHubTests
{
    private static readonly Guid SessionId = Guid.Parse("4962aaaa-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("4962bbbb-0000-0000-0000-000000000002");
    private static readonly string Group = $"session:{SessionId}";
    private const string ConnectionId = "conn-4962";

    private readonly Mock<ISender> _sender = new();
    private readonly Mock<IGroupManager> _groups = new();
    private readonly Mock<IHubCallerClients> _clients = new();
    private readonly Mock<IClientProxy> _others = new();
    private readonly Mock<HubCallerContext> _context = new();
    private readonly Dictionary<object, object?> _items = new();

    private GameStateHub NewHub(ClaimsPrincipal? user)
    {
        _context.Setup(c => c.ConnectionId).Returns(ConnectionId);
        _context.Setup(c => c.User).Returns(user);
        _context.Setup(c => c.Items).Returns(_items);
        _context.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);
        _clients.Setup(c => c.OthersInGroup(It.IsAny<string>())).Returns(_others.Object);

        return new GameStateHub(new Mock<ILogger<GameStateHub>>().Object, _sender.Object)
        {
            Groups = _groups.Object,
            Clients = _clients.Object,
            Context = _context.Object,
        };
    }

    private static ClaimsPrincipal SignedIn(Guid userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "SessionCookie"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private void AccessIs(bool found, bool authorized) =>
        _sender
            .Setup(s => s.Send(
                It.Is<GetLiveSessionParticipantContextQuery>(q => q.SessionId == SessionId && q.UserId == UserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LiveSessionParticipantContextResult(found, authorized));

    private void VerifyNotAdded() =>
        _groups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

    // ── JoinSession ──

    [Fact]
    public async Task JoinSession_AParticipant_EntersTheSessionGroup()
    {
        AccessIs(found: true, authorized: true);
        var hub = NewHub(SignedIn(UserId));

        await hub.JoinSession(SessionId.ToString());

        _groups.Verify(g => g.AddToGroupAsync(ConnectionId, Group, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinSession_AnAuthenticatedNonParticipant_IsRefused_AndNotAdded()
    {
        AccessIs(found: true, authorized: false);
        var hub = NewHub(SignedIn(UserId));

        var act = () => hub.JoinSession(SessionId.ToString());

        (await act.Should().ThrowAsync<HubException>()).WithMessage(GameStateHub.SessionNotFoundMessage);
        VerifyNotAdded();
    }

    [Fact]
    public async Task JoinSession_AnUnknownSession_GetsTheSameErrorAsANonParticipant()
    {
        AccessIs(found: false, authorized: false);
        var hub = NewHub(SignedIn(UserId));

        var act = () => hub.JoinSession(SessionId.ToString());

        (await act.Should().ThrowAsync<HubException>()).WithMessage(GameStateHub.SessionNotFoundMessage);
        VerifyNotAdded();
    }

    [Fact]
    public async Task JoinSession_AnAnonymousConnection_IsRefusedWithoutAskingWhoTakesPart()
    {
        var hub = NewHub(Anonymous());

        var act = () => hub.JoinSession(SessionId.ToString());

        (await act.Should().ThrowAsync<HubException>()).WithMessage(GameStateHub.AuthenticationRequiredMessage);
        VerifyNotAdded();
        _sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task JoinSession_AGuestTokenIdentity_IsNotAnAuthenticatedUser()
    {
        // SessionParticipantIdProvider turns ?sessionToken=… into UserIdentifier "guest:…" without checking
        // it: the hub must not read that as an identity.
        _context.Setup(c => c.UserIdentifier).Returns("guest:anything");
        var hub = NewHub(Anonymous());

        var act = () => hub.JoinSession(SessionId.ToString());

        (await act.Should().ThrowAsync<HubException>()).WithMessage(GameStateHub.AuthenticationRequiredMessage);
        VerifyNotAdded();
    }

    [Fact]
    public async Task JoinSession_ANonGuidId_IsRefused()
    {
        var hub = NewHub(SignedIn(UserId));

        var act = () => hub.JoinSession("session-123");

        (await act.Should().ThrowAsync<HubException>()).WithMessage(GameStateHub.SessionNotFoundMessage);
        VerifyNotAdded();
    }

    [Fact]
    public async Task JoinSession_UsesTheGroupNameTheServerSendsTo_WhateverTheCasing()
    {
        AccessIs(found: true, authorized: true);
        var hub = NewHub(SignedIn(UserId));

        await hub.JoinSession(SessionId.ToString().ToUpperInvariant());

        _groups.Verify(g => g.AddToGroupAsync(ConnectionId, Group, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── BroadcastStateChange ──

    [Fact]
    public async Task BroadcastStateChange_WithoutJoining_IsRefused_AndNothingIsSent()
    {
        var hub = NewHub(SignedIn(UserId));

        var act = () => hub.BroadcastStateChange(SessionId.ToString(), new { score = 99 });

        (await act.Should().ThrowAsync<HubException>()).WithMessage(GameStateHub.NotJoinedMessage);
        _clients.Verify(c => c.OthersInGroup(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BroadcastStateChange_AfterJoining_RelaysToTheOthersInThatSession()
    {
        AccessIs(found: true, authorized: true);
        var hub = NewHub(SignedIn(UserId));
        await hub.JoinSession(SessionId.ToString());

        await hub.BroadcastStateChange(SessionId.ToString(), new { score = 99 });

        _clients.Verify(c => c.OthersInGroup(Group), Times.Once);
        _others.Verify(
            p => p.SendCoreAsync("StateChanged", It.Is<object?[]>(a => a.Length == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastStateChange_IntoAnotherSession_IsRefused_EvenAfterJoiningOne()
    {
        AccessIs(found: true, authorized: true);
        var hub = NewHub(SignedIn(UserId));
        await hub.JoinSession(SessionId.ToString());

        var act = () => hub.BroadcastStateChange(Guid.NewGuid().ToString(), new { score = 99 });

        (await act.Should().ThrowAsync<HubException>()).WithMessage(GameStateHub.NotJoinedMessage);
    }

    [Fact]
    public async Task BroadcastStateChange_AfterLeaving_IsRefused()
    {
        AccessIs(found: true, authorized: true);
        var hub = NewHub(SignedIn(UserId));
        await hub.JoinSession(SessionId.ToString());
        await hub.LeaveSession(SessionId.ToString());

        var act = () => hub.BroadcastStateChange(SessionId.ToString(), new { score = 99 });

        (await act.Should().ThrowAsync<HubException>()).WithMessage(GameStateHub.NotJoinedMessage);
        _groups.Verify(g => g.RemoveFromGroupAsync(ConnectionId, Group, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastStateChange_AfterBeingRemovedFromTheSession_IsRefused_AndLeavesTheGroup()
    {
        AccessIs(found: true, authorized: true);
        var hub = NewHub(SignedIn(UserId));
        await hub.JoinSession(SessionId.ToString());
        AccessIs(found: true, authorized: false); // removed from the live session after joining

        var act = () => hub.BroadcastStateChange(SessionId.ToString(), new { score = 99 });

        (await act.Should().ThrowAsync<HubException>()).WithMessage(GameStateHub.NotJoinedMessage);
        _groups.Verify(g => g.RemoveFromGroupAsync(ConnectionId, Group, It.IsAny<CancellationToken>()), Times.Once);
        _clients.Verify(c => c.OthersInGroup(It.IsAny<string>()), Times.Never);
    }

    // ── Surface ──

    [Fact]
    public void TheHub_ExposesOnlyJoinLeaveAndTheStateRelay()
    {
        // Every public method of a hub can be invoked by any connected client. The server notifies through
        // IHubContext<GameStateHub>; a new hub method is a new client entry point and needs its own check.
        var methods = typeof(GameStateHub)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        methods.Should().Equal("BroadcastStateChange", "JoinSession", "LeaveSession", "OnDisconnectedAsync");
    }
}

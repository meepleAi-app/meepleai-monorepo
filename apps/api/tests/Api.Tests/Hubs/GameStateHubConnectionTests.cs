using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.Hubs;
using Api.Infrastructure.Authentication;
using SessionCookieConfiguration = Api.Models.SessionCookieConfiguration;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Hubs;

/// <summary>
/// #4962 end to end, on a real hub connection: the production authentication scheme, the production
/// <see cref="IUserIdProvider"/> and <see cref="GameStateHub"/> mapped at <c>/hubs/gamestate</c>, driven by a
/// WebSocket client speaking the SignalR JSON protocol (no SignalR client package in this project).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a real connection and not only the hub's unit tests.</b> The hub judges <c>Context.User</c>, and that
/// user comes from <c>SessionAuthenticationHandler</c>, which until #4962 skipped every path outside
/// <c>/api</c>: a hub connection was anonymous whatever cookie it carried. A participant check in the hub
/// alone would have locked every player out of the live updates. Only a connection through the real scheme
/// shows that a participant's cookie reaches the hub.
/// </para>
/// <para>
/// The mediator is a double: it validates one session token and answers the participant query, so the
/// test pins the hub's decision and the scheme's path, not the session store.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4962")]
public sealed class GameStateHubConnectionTests
{
    private const string GoodToken = "token-of-a-signed-in-user";

    private static readonly Guid SessionId = Guid.Parse("4962cccc-0000-0000-0000-000000000003");
    private static readonly Guid UserId = Guid.Parse("4962dddd-0000-0000-0000-000000000004");

    [Fact]
    public async Task AParticipantWithItsSessionCookie_Joins_AndReceivesWhatTheServerSendsToTheSession()
    {
        await using var app = await StartAppAsync(participant: true);
        await using var client = await RawHubClient.ConnectAsync(app.GetTestServer(), cookieToken: GoodToken);

        var completion = await client.InvokeAsync("JoinSession", SessionId.ToString());
        completion.TryGetProperty("error", out _).Should().BeFalse(completion.ToString());

        await SendToSessionAsync(app, "DisputeResolved", new { verdict = "the rulebook says so" });

        var received = await client.NextInvocationAsync();
        received.GetProperty("target").GetString().Should().Be("DisputeResolved");
    }

    [Fact]
    public async Task AConnectionWithoutCookie_CannotJoin_AndReceivesNothingOfTheSession()
    {
        await using var app = await StartAppAsync(participant: true);
        await using var client = await RawHubClient.ConnectAsync(app.GetTestServer(), cookieToken: null);

        var completion = await client.InvokeAsync("JoinSession", SessionId.ToString());
        completion.GetProperty("error").GetString().Should().Contain(GameStateHub.AuthenticationRequiredMessage);

        await SendToSessionAsync(app, "DisputeResolved", new { verdict = "forged" });
        await SendToEveryoneAsync(app, "Marker");

        // Per-connection order is preserved: the first thing to arrive is the marker sent after the session
        // message, so the session message never reached this connection.
        (await client.NextInvocationAsync()).GetProperty("target").GetString().Should().Be("Marker");
    }

    [Fact]
    public async Task ASignedInNonParticipant_CannotJoin()
    {
        await using var app = await StartAppAsync(participant: false);
        await using var client = await RawHubClient.ConnectAsync(app.GetTestServer(), cookieToken: GoodToken);

        var completion = await client.InvokeAsync("JoinSession", SessionId.ToString());

        completion.GetProperty("error").GetString().Should().Contain(GameStateHub.SessionNotFoundMessage);
    }

    [Fact]
    public async Task AGuestSessionTokenInTheQuery_IsNotAnIdentity()
    {
        await using var app = await StartAppAsync(participant: true);
        await using var client = await RawHubClient.ConnectAsync(
            app.GetTestServer(), cookieToken: null, query: "?sessionToken=whatever");

        var completion = await client.InvokeAsync("JoinSession", SessionId.ToString());

        completion.GetProperty("error").GetString().Should().Contain(GameStateHub.AuthenticationRequiredMessage);
    }

    [Fact]
    public async Task ARetiredRelay_CannotBeInvoked()
    {
        await using var app = await StartAppAsync(participant: true);
        await using var client = await RawHubClient.ConnectAsync(app.GetTestServer(), cookieToken: GoodToken);

        var completion = await client.InvokeAsync("NotifyDisputeResolved", SessionId.ToString(), new { verdict = "forged" });

        completion.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("NotifyDisputeResolved");
    }

    // ── Host in memoria ────────────────────────────────────────────────────────

    private static async Task<WebApplication> StartAppAsync(bool participant)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.Is<ValidateSessionQuery>(q => q.SessionToken == GoodToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidSession());
        mediator
            .Setup(m => m.Send(It.Is<ValidateSessionQuery>(q => q.SessionToken != GoodToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatusDto(IsValid: false, Principal: null, ExpiresAt: null, LastSeenAt: null));
        mediator
            .Setup(m => m.Send(
                It.Is<GetLiveSessionParticipantContextQuery>(q => q.SessionId == SessionId && q.UserId == UserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LiveSessionParticipantContextResult(Found: true, Authorized: participant));

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(mediator.Object);
        builder.Services.AddSingleton<ISender>(mediator.Object);
        builder.Services.Configure<SessionCookieConfiguration>(_ => { });

        // The production scheme and identity provider (AuthenticationServiceExtensions, Program.cs).
        builder.Services
            .AddAuthentication(o =>
            {
                o.DefaultScheme = "SessionCookie";
                o.DefaultChallengeScheme = "SessionCookie";
            })
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>("SessionCookie", _ => { });
        builder.Services.AddSingleton<IUserIdProvider, SessionParticipantIdProvider>();
        builder.Services.AddSignalR();

        var app = builder.Build();
        app.UseAuthentication();
        app.MapHub<GameStateHub>("/hubs/gamestate");
        await app.StartAsync();
        return app;
    }

    private static SessionStatusDto ValidSession()
    {
        var user = new UserDto(
            Id: UserId,
            Email: "player@example.com",
            DisplayName: "Player",
            Role: "User",
            Tier: "normal",
            CreatedAt: DateTime.UtcNow,
            IsTwoFactorEnabled: false,
            TwoFactorEnabledAt: null,
            Level: 1,
            ExperiencePoints: 0);

        return new SessionStatusDto(
            IsValid: true,
            Principal: new Principal(user, Actor: null),
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            LastSeenAt: DateTime.UtcNow);
    }

    private static Task SendToSessionAsync(WebApplication app, string target, object payload) =>
        app.Services.GetRequiredService<IHubContext<GameStateHub>>()
            .Clients.Group($"session:{SessionId}")
            .SendAsync(target, payload);

    private static Task SendToEveryoneAsync(WebApplication app, string target) =>
        app.Services.GetRequiredService<IHubContext<GameStateHub>>().Clients.All.SendAsync(target);

    /// <summary>
    /// Minimal SignalR JSON-protocol client over the TestServer WebSocket: handshake, invocation with
    /// completion, server invocations. Messages end with the record separator 0x1E; pings (type 6) are skipped.
    /// </summary>
    private sealed class RawHubClient : IAsyncDisposable
    {
        private const char RecordSeparator = '\u001e';
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly WebSocket _socket;
        private readonly Queue<string> _pending = new();
        private readonly StringBuilder _partial = new();
        private int _nextInvocationId;

        private RawHubClient(WebSocket socket) => _socket = socket;

        public static async Task<RawHubClient> ConnectAsync(TestServer server, string? cookieToken, string query = "")
        {
            var wsClient = server.CreateWebSocketClient();
            if (cookieToken is not null)
            {
                wsClient.ConfigureRequest = request => request.Headers["Cookie"] = $"meepleai_session={cookieToken}";
            }

            var socket = await wsClient.ConnectAsync(new Uri($"ws://localhost/hubs/gamestate{query}"), CancellationToken.None);
            var client = new RawHubClient(socket);

            await client.SendAsync("{\"protocol\":\"json\",\"version\":1}");
            var handshake = await client.ReceiveMessageAsync();
            handshake.Should().Be("{}", "the hub must accept the JSON protocol handshake");
            return client;
        }

        public async Task<JsonElement> InvokeAsync(string target, params object[] arguments)
        {
            var id = (++_nextInvocationId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            await SendAsync(JsonSerializer.Serialize(new { type = 1, invocationId = id, target, arguments }));

            while (true)
            {
                var message = await NextMessageAsync();
                if (message.GetProperty("type").GetInt32() == 3
                    && message.TryGetProperty("invocationId", out var invocationId)
                    && invocationId.GetString() == id)
                {
                    return message;
                }
            }
        }

        public async Task<JsonElement> NextInvocationAsync()
        {
            while (true)
            {
                var message = await NextMessageAsync();
                if (message.GetProperty("type").GetInt32() == 1)
                {
                    return message;
                }
            }
        }

        private async Task<JsonElement> NextMessageAsync()
        {
            while (true)
            {
                using var doc = JsonDocument.Parse(await ReceiveMessageAsync());
                if (doc.RootElement.GetProperty("type").GetInt32() != 6)
                {
                    return doc.RootElement.Clone();
                }
            }
        }

        private Task SendAsync(string json) =>
            _socket.SendAsync(Encoding.UTF8.GetBytes(json + RecordSeparator), WebSocketMessageType.Text, true, CancellationToken.None);

        private async Task<string> ReceiveMessageAsync()
        {
            using var timeout = new CancellationTokenSource(Timeout);
            var buffer = new byte[4096];
            while (_pending.Count == 0)
            {
                var result = await _socket.ReceiveAsync(buffer, timeout.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new InvalidOperationException("The hub closed the connection.");
                }

                _partial.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                var text = _partial.ToString();
                var last = text.LastIndexOf(RecordSeparator);
                if (last < 0)
                {
                    continue;
                }

                foreach (var part in text[..last].Split(RecordSeparator))
                {
                    _pending.Enqueue(part);
                }

                _partial.Clear().Append(text[(last + 1)..]);
            }

            return _pending.Dequeue();
        }

        public async ValueTask DisposeAsync()
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }

            _socket.Dispose();
        }
    }
}

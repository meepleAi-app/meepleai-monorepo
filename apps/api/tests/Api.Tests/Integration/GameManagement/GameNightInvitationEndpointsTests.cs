using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// E2E integration tests for the public token-based RSVP endpoints introduced in
/// Issue #607 (Wave A.5a). Validates the full HTTP surface (status codes, request/response
/// shape, idempotency D2 b, anonymous access) end-to-end against a real PostgreSQL database
/// via Testcontainers. Mocks <see cref="IGameNightEmailService"/> to avoid SMTP I/O.
/// </summary>
/// <remarks>
/// Endpoints covered:
/// <list type="bullet">
///   <item><c>GET  /api/v1/game-nights/invitations/{token}</c></item>
///   <item><c>POST /api/v1/game-nights/invitations/{token}/respond</c></item>
///   <item><c>POST /api/v1/game-nights/{gameNightId}/invitations</c></item>
/// </list>
/// Idempotency contract D2 b: same-state response → 200 no-op; switch Accepted ⇄ Declined →
/// 409 Conflict; pending past-expiry / terminal Expired or Cancelled → 410 Gone.
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightInvitationEndpointsTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private readonly Mock<IGameNightEmailService> _emailServiceMock = new();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public GameNightInvitationEndpointsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"game_night_invite_e2e_{Guid.NewGuid():N}";

        _emailServiceMock
            .Setup(s => s.SendGameNightInvitationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _factory = IntegrationWebApplicationFactory.Create(
            connectionString,
            extraConfig: new Dictionary<string, string?>
            {
                ["App:BaseUrl"] = "https://meepleai.test",
                ["GameNight:InvitationExpiryDays"] = "14"
            });

        // Override email service to a no-op mock (avoid SMTP)
        _factory = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll(typeof(IGameNightEmailService));
            s.AddSingleton(_emailServiceMock.Object);
        }));

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedGameNightAsync(Guid organizerId, string title = "Test Night")
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var eventId = Guid.NewGuid();
        dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = organizerId,
            Title = title,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(7),
            Location = "Home",
            MaxPlayers = 6,
            GameIdsJson = "[]",
            Status = "Published",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return eventId;
    }

    private async Task<GameNightInvitationEntity> SeedInvitationAsync(
        Guid gameNightId,
        Guid createdBy,
        string email = "guest@example.com",
        string status = "Pending",
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? respondedAt = null,
        Guid? respondedByUserId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var entity = new GameNightInvitationEntity
        {
            Id = Guid.NewGuid(),
            Token = GenerateToken(),
            GameNightId = gameNightId,
            Email = email.Trim().ToLowerInvariant(),
            Status = status,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(14),
            RespondedAt = respondedAt,
            RespondedByUserId = respondedByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy
        };
        dbContext.GameNightInvitations.Add(entity);
        await dbContext.SaveChangesAsync();
        return entity;
    }

    private static string GenerateToken()
    {
        // 22-char base62 — matches InvitationToken.Generate() shape. Crypto RNG (CA5394).
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var chars = new char[22];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /game-nights/invitations/{token} — public lookup
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInvitationByToken_UnknownToken_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/game-nights/invitations/nonexistent-token-1234567");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetInvitationByToken_PendingInvitation_Returns200WithPublicDto()
    {
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(gameNightId, organizerId);

        var response = await _client.GetAsync($"/api/v1/game-nights/invitations/{invitation.Token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<PublicGameNightInvitationDto>();
        dto.Should().NotBeNull();
        dto!.Token.Should().Be(invitation.Token);
        dto.Status.Should().Be("Pending");
        dto.GameNightId.Should().Be(gameNightId);
        dto.AlreadyRespondedAs.Should().BeNull();
    }

    [Fact]
    public async Task GetInvitationByToken_AnonymousRequest_AllowedNoAuthRequired()
    {
        // Sanity check: no cookie header — no auth — should still resolve.
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(gameNightId, organizerId);

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/game-nights/invitations/{invitation.Token}");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetInvitationByToken_AcceptedInvitation_DtoReportsAlreadyRespondedAs()
    {
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(
            gameNightId, organizerId,
            status: "Accepted",
            respondedAt: DateTimeOffset.UtcNow);

        var response = await _client.GetAsync($"/api/v1/game-nights/invitations/{invitation.Token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<PublicGameNightInvitationDto>();
        dto!.Status.Should().Be("Accepted");
        dto.AlreadyRespondedAs.Should().Be("Accepted");
    }

    // ────────────────────────────────────────────────────────────────────
    // POST /game-nights/invitations/{token}/respond — public RSVP
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RespondByToken_AcceptAnonymous_Returns200WithUpdatedDto()
    {
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(gameNightId, organizerId);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
            new { Response = "Accepted" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<PublicGameNightInvitationDto>();
        dto!.Status.Should().Be("Accepted");
        dto.RespondedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RespondByToken_DeclineAnonymous_Returns200()
    {
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(gameNightId, organizerId);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
            new { Response = "Declined" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<PublicGameNightInvitationDto>();
        dto!.Status.Should().Be("Declined");
    }

    [Fact]
    public async Task RespondByToken_AuthenticatedUser_PopulatesRespondedByUserId()
    {
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(gameNightId, organizerId);

        Guid responderId;
        string sessionToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (responderId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        }

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
            sessionToken,
            new { Response = "Accepted" });

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify persistence layer captured the responder
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var persisted = await dbContext.GameNightInvitations
                .AsNoTracking()
                .FirstAsync(i => i.Id == invitation.Id);
            persisted.Status.Should().Be("Accepted");
            persisted.RespondedByUserId.Should().Be(responderId);
        }
    }

    [Fact]
    public async Task RespondByToken_UnknownToken_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/game-nights/invitations/UnknownTokenAbcdef1234/respond",
            new { Response = "Accepted" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RespondByToken_InvalidResponseValue_Returns400()
    {
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(gameNightId, organizerId);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
            new { Response = "Maybe" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Expired")]
    [InlineData("Cancelled")]
    public async Task RespondByToken_RejectsSystemSetStatusValue_Returns400(string systemStatus)
    {
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(gameNightId, organizerId);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
            new { Response = systemStatus });

        // Endpoint enum guard rejects non-Accept/Decline values with 400.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RespondByToken_SameStateRepeat_Returns200NoOp()
    {
        // Idempotency D2 b: repeating the current response is a no-op (200, not 409).
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(
            gameNightId, organizerId,
            status: "Accepted",
            respondedAt: DateTimeOffset.UtcNow);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
            new { Response = "Accepted" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RespondByToken_SwitchAcceptedToDeclined_Returns409Conflict()
    {
        // Idempotency D2 b: switching Accepted ⇄ Declined surfaces as 409 Conflict.
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(
            gameNightId, organizerId,
            status: "Accepted",
            respondedAt: DateTimeOffset.UtcNow);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
            new { Response = "Declined" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RespondByToken_OnExpiredInvitation_Returns410Gone()
    {
        // D2 b: terminal Expired surfaces as 410 Gone.
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(
            gameNightId, organizerId,
            status: "Expired",
            expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
            new { Response = "Accepted" });

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task RespondByToken_OnCancelledInvitation_Returns410Gone()
    {
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(
            gameNightId, organizerId,
            status: "Cancelled");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
            new { Response = "Accepted" });

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task RespondByToken_PendingPastExpiry_Returns410Gone()
    {
        // D2 b: pending invitation past ExpiresAt cutoff is treated as terminal-Expired
        // by the handler (transitions on read) → 410 Gone on respond attempt.
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);
        var invitation = await SeedInvitationAsync(
            gameNightId, organizerId,
            status: "Pending",
            expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
            new { Response = "Accepted" });

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    // ────────────────────────────────────────────────────────────────────
    // POST /game-nights/{gameNightId}/invitations — organizer create
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateInvitationByEmail_Anonymous_Returns401()
    {
        var organizerId = Guid.NewGuid();
        var gameNightId = await SeedGameNightAsync(organizerId);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/game-nights/{gameNightId}/invitations",
            new { Email = "guest@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateInvitationByEmail_AsOrganizer_Returns201WithDto()
    {
        // Authenticate as a user whose ID is the organizer of the seeded game night.
        Guid organizerId;
        string sessionToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (organizerId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        }
        var gameNightId = await SeedGameNightAsync(organizerId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/game-nights/{gameNightId}/invitations",
            sessionToken,
            new { Email = "guest@example.com" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<GameNightInvitationDto>();
        dto.Should().NotBeNull();
        dto!.GameNightId.Should().Be(gameNightId);
        dto.Email.Should().Be("guest@example.com");
        dto.Status.Should().Be("Pending");
        dto.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateInvitationByEmail_AsNonOrganizer_Returns403()
    {
        var organizerId = Guid.NewGuid(); // fake organizer (no session)
        var gameNightId = await SeedGameNightAsync(organizerId);

        // Authenticate as a different user (not the organizer)
        Guid otherUserId;
        string sessionToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (otherUserId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        }

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/game-nights/{gameNightId}/invitations",
            sessionToken,
            new { Email = "guest@example.com" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateInvitationByEmail_UnknownGameNight_Returns404()
    {
        Guid userId;
        string sessionToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        }

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/game-nights/{Guid.NewGuid()}/invitations",
            sessionToken,
            new { Email = "guest@example.com" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateInvitationByEmail_DuplicatePending_Returns409()
    {
        Guid organizerId;
        string sessionToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (organizerId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        }
        var gameNightId = await SeedGameNightAsync(organizerId);
        await SeedInvitationAsync(gameNightId, organizerId, email: "guest@example.com");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/game-nights/{gameNightId}/invitations",
            sessionToken,
            new { Email = "GUEST@EXAMPLE.COM" }); // case-insensitive de-dup

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateInvitationByEmail_InvalidEmailFormat_ReturnsValidationError()
    {
        Guid organizerId;
        string sessionToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (organizerId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        }
        var gameNightId = await SeedGameNightAsync(organizerId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/game-nights/{gameNightId}/invitations",
            sessionToken,
            new { Email = "not-an-email" });

        var response = await _client.SendAsync(request);

        // FluentValidation pipeline surfaces invalid email as 400 BadRequest or 422 UnprocessableEntity
        // depending on configuration; both indicate validation failure.
        ((int)response.StatusCode).Should().BeOneOf(400, 422);
    }
}

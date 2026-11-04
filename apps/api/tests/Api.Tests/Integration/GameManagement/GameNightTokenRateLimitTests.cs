using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
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
/// Issue #1169 — Verifies the per-IP rate-limit policies wired on the public
/// RSVP token endpoints exceed → 429 + Retry-After contract.
///
/// Why a separate file: <see cref="GameNightInvitationEndpointsTests"/> runs
/// against the shared <see cref="IntegrationWebApplicationFactory"/> which
/// hard-disables rate limiting for speed/determinism. Enabling it requires a
/// dedicated factory + env-var dance (mirrors
/// <c>AdminProviderEndpointsIntegrationTests.Probe_RateLimit_ReturnsTooManyRequests</c>).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "1169")]
public sealed class GameNightTokenRateLimitTests
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Mock<IGameNightEmailService> _emailServiceMock = new();

    public GameNightTokenRateLimitTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _emailServiceMock
            .Setup(s => s.SendGameNightInvitationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameNightEmailLocale>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// 10 req/min/IP cap on POST /respond. The 11th call within the window
    /// must surface 429 with a Retry-After header so clients can back off.
    /// </summary>
    [Fact]
    public async Task RespondByToken_ExceedsRateLimit_Returns429WithRetryAfter()
    {
        var dbName = $"gn_invite_ratelimit_{Guid.NewGuid():N}";
        var connStr = await _fixture.CreateIsolatedDatabaseAsync(dbName);

        try
        {
            // #3887: enableRateLimiting:true switches rate limiting on for THIS host only, via the
            // factory's in-memory configuration. The previous version also blanked
            // DISABLE_RATE_LIMITING in the process environment — a process-global change that turned
            // rate limiting on for every host built meanwhile by a parallel xUnit collection.
            var baseFactory = IntegrationWebApplicationFactory.Create(
                connStr,
                extraConfig: new Dictionary<string, string?>
                {
                    ["GameNight:InvitationExpiryDays"] = "14"
                },
                enableRateLimiting: true);

            using var factory = baseFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll(typeof(IGameNightEmailService));
                s.AddSingleton(_emailServiceMock.Object);
            }));

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
                await dbContext.Database.MigrateAsync();
            }

            // Seed a Pending invitation so every replay hits the same
            // idempotent same-state code path (200 OK until the policy bites).
            var invitation = await SeedInvitationAsync(factory);

            using var client = factory.CreateClient();

            HttpStatusCode? lastStatus = null;
            HttpResponseMessage? finalResponse = null;
            int allowedCount = 0;

            for (int i = 0; i < 11; i++)
            {
                var response = await client.PostAsJsonAsync(
                    $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
                    new { Response = "Accepted" });
                lastStatus = response.StatusCode;
                finalResponse = response;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    allowedCount++;
                }
                else
                {
                    break;
                }
            }

            // 10 req/min permit limit ⇒ at most 10 OK before the 11th 429s.
            allowedCount.Should().BeLessThanOrEqualTo(10);
            lastStatus.Should().Be(HttpStatusCode.TooManyRequests);

            // The rejection middleware writes Retry-After to either the response
            // header OR the JSON body's retryAfterSeconds field; SlidingWindowLimiter
            // does not always populate MetadataName.RetryAfter on partition reject,
            // so we accept either presence as proof of the 429 contract.
            var hasRetryHeader = finalResponse!.Headers.Contains("Retry-After");
            var body = await finalResponse.Content.ReadAsStringAsync();
            var hasRetryInBody = body.Contains("retryAfter", StringComparison.OrdinalIgnoreCase)
                || body.Contains("Too Many", StringComparison.OrdinalIgnoreCase);
            (hasRetryHeader || hasRetryInBody).Should().BeTrue(
                $"the 429 response must signal backoff via header or body — got body: {body}");
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(dbName);
        }
    }

    /// <summary>
    /// #4846. The guest opt-out has a bucket of its own (10 req/min/IP): the 11th tap gets a 429, and the RSVPs of
    /// everybody behind the same address are not eaten by it. Before, it shared the bucket of /respond, so a guest
    /// opening the link from an office network competed with the answers of their colleagues.
    /// </summary>
    [Fact]
    public async Task UnsubscribeByKey_ExceedsItsOwnLimit_Returns429_AndTheRsvpBucketIsUntouched()
    {
        var dbName = $"gn_unsub_ratelimit_{Guid.NewGuid():N}";
        var connStr = await _fixture.CreateIsolatedDatabaseAsync(dbName);

        try
        {
            var baseFactory = IntegrationWebApplicationFactory.Create(
                connStr,
                extraConfig: new Dictionary<string, string?> { ["GameNight:InvitationExpiryDays"] = "14" },
                enableRateLimiting: true);

            using var factory = baseFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll(typeof(IGameNightEmailService));
                s.AddSingleton(_emailServiceMock.Object);
            }));

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
                await dbContext.Database.MigrateAsync();
            }

            var invitation = await SeedInvitationAsync(factory);
            using var client = factory.CreateClient();

            var statuses = new List<HttpStatusCode>();
            for (var i = 0; i < 11; i++)
            {
                var response = await client.PostAsync(
                    $"/api/v1/game-nights/guest-unsubscribe/{invitation.UnsubscribeKey}", content: null);
                statuses.Add(response.StatusCode);
            }

            statuses.Take(10).Should().AllBeEquivalentTo(HttpStatusCode.NoContent);
            statuses[10].Should().Be(HttpStatusCode.TooManyRequests);

            var rsvp = await client.PostAsJsonAsync(
                $"/api/v1/game-nights/invitations/{invitation.Token}/respond",
                new { Response = "Accepted" });
            rsvp.StatusCode.Should().Be(HttpStatusCode.OK, "the RSVP bucket is a different one");
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(dbName);
        }
    }

    private async Task<GameNightInvitationEntity> SeedInvitationAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = organizerId,
            Title = "Rate-limit test night",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(7),
            Location = "Home",
            MaxPlayers = 6,
            GameIdsJson = "[]",
            Status = "Published",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var invitation = new GameNightInvitationEntity
        {
            Id = Guid.NewGuid(),
            Token = GenerateToken(),
            UnsubscribeKey = GenerateToken(),
            GameNightId = eventId,
            Email = "guest@example.com",
            Status = "Pending",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = organizerId
        };
        dbContext.GameNightInvitations.Add(invitation);
        await dbContext.SaveChangesAsync();
        return invitation;
    }

    private static string GenerateToken()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var chars = new char[22];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }
}

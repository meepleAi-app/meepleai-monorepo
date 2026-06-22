using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Services;
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

namespace Api.Tests.BoundedContexts.Authentication.Endpoints;

/// <summary>
/// HTTP integration test for C7 session-revoke-on-password-change.
/// Spec: docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md (C7).
/// Review finding F6 follow-up: the existing C7 unit tests only mock
/// ISessionRepository — they verify branching logic but don't prove that
/// <see cref="Api.BoundedContexts.Authentication.Infrastructure.Persistence.SessionRepository.RevokeAllUserSessionsExceptAsync"/>
/// actually revokes the right rows in PostgreSQL or that the revoked
/// session token then fails authentication on a downstream endpoint.
/// This test exercises the full HTTP stack against Testcontainers
/// PostgreSQL so a regression in either the repository SQL or the
/// auth-middleware reject path would surface here.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public sealed class ChangePasswordSessionRevokeEndpointTests : IAsyncLifetime
{
    private const string SessionCookieName = "meepleai_session";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public ChangePasswordSessionRevokeEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"change_pwd_revoke_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _factory = IntegrationWebApplicationFactory.Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Enable public registration so /auth/register doesn't 403
                    // (mirrors the same trick used by other auth integration tests).
                    services.RemoveAll(typeof(IConfigurationService));
                    var mockConfig = new Mock<IConfigurationService>();
                    mockConfig
                        .Setup(c => c.GetValueAsync<bool?>(
                            "Registration:PublicEnabled",
                            It.IsAny<bool?>(),
                            It.IsAny<string?>()))
                        .ReturnsAsync(true);
                    services.AddSingleton<IConfigurationService>(mockConfig.Object);
                });
            });

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
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task ChangePassword_FromSessionA_RevokesSessionB_ButPreservesSessionA()
    {
        // Arrange — register a user (yields session A) then login again
        // with the same credentials to mint a second session B. The two
        // sessions belong to the same user and must be observably distinct
        // at the cookie level.
        var email = $"revoke-test-{Guid.NewGuid():N}@test.local";
        const string oldPassword = "OldPasswordUnique1!";
        const string newPassword = "NewPasswordUnique2!";

        var sessionA = await RegisterAsync(email, oldPassword);
        var sessionB = await LoginAsync(email, oldPassword);
        sessionA.Should().NotBeNullOrWhiteSpace();
        sessionB.Should().NotBeNullOrWhiteSpace();
        sessionA.Should().NotBe(sessionB,
            "the second login must mint a fresh session token, distinct from " +
            "the registration session — otherwise the test setup degenerates.");

        // Sanity: session B is valid before the password change.
        (await CallAuthMeAsync(sessionB)).StatusCode
            .Should().Be(HttpStatusCode.OK,
                "session B must authenticate successfully before the password " +
                "change so we can prove revocation actually happens.");

        // Act — change the password from session A. The endpoint must
        // revoke session B (and any other non-current session) atomically
        // with the password update.
        var changeRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/profile/password");
        changeRequest.Headers.Add("Cookie", $"{SessionCookieName}={sessionA}");
        changeRequest.Content = JsonContent.Create(new
        {
            CurrentPassword = oldPassword,
            NewPassword = newPassword,
        });
        var changeResponse = await _client.SendAsync(changeRequest);
        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "password change must succeed when current-password matches.");

        // Assert — session A still authenticates (the user shouldn't be
        // logged out of the device they just used to change the password).
        (await CallAuthMeAsync(sessionA)).StatusCode
            .Should().Be(HttpStatusCode.OK,
                "C7: the current session MUST be preserved so the user isn't " +
                "kicked off the device that initiated the change.");

        // Session B is rejected (revoked).
        (await CallAuthMeAsync(sessionB)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized,
                "C7: every other session MUST be revoked atomically with the " +
                "password change — otherwise a stolen cookie outlives the " +
                "credential it was bound to.");

        // The new password works for fresh logins.
        var sessionC = await LoginAsync(email, newPassword);
        sessionC.Should().NotBeNullOrWhiteSpace();
        (await CallAuthMeAsync(sessionC)).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> RegisterAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email,
            Password = password,
            DisplayName = "C7 Revoke Test",
        });
        response.EnsureSuccessStatusCode();
        return ExtractSessionCookie(response)
            ?? throw new InvalidOperationException("Register did not set session cookie");
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = password,
        });
        response.EnsureSuccessStatusCode();
        return ExtractSessionCookie(response)
            ?? throw new InvalidOperationException("Login did not set session cookie");
    }

    private async Task<HttpResponseMessage> CallAuthMeAsync(string sessionToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("Cookie", $"{SessionCookieName}={sessionToken}");
        return await _client.SendAsync(request);
    }

    private static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        foreach (var cookie in cookies)
        {
            if (!cookie.StartsWith($"{SessionCookieName}=", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = cookie.Split(';')[0];
            return value[(SessionCookieName.Length + 1)..];
        }

        return null;
    }
}

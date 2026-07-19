using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
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

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for the ToS acceptance foundation (#2954 F1): the append-only
/// user_terms_acceptances persistence, the registration recording path, and the
/// me-scoped accept/status endpoints. Uses real PostgreSQL via Testcontainers.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public sealed class TermsAcceptanceIntegrationTests : IAsyncLifetime
{
    private const string RegisterEndpoint = "/api/v1/auth/register";
    private const string StatusEndpoint = "/api/v1/users/me/terms/status";
    private const string AcceptEndpoint = "/api/v1/users/me/terms/accept";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public TermsAcceptanceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"terms_accept_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _factory = IntegrationWebApplicationFactory.Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Public registration must be enabled so /auth/register skips the
                    // 403 RequirePublicRegistration filter.
                    services.RemoveAll(typeof(IConfigurationService));
                    var mockConfig = new Mock<IConfigurationService>();
                    mockConfig
                        .Setup(c => c.GetValueAsync<bool?>(
                            "Registration:PublicEnabled", It.IsAny<bool?>(), It.IsAny<string?>()))
                        .ReturnsAsync(true);
                    services.AddSingleton<IConfigurationService>(mockConfig.Object);
                });
            });

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        // HandleCookies=false: TestServer's CookieContainer is unreliable at carrying the
        // session cookie to subsequent authenticated requests (see the lenient asserts in
        // AuthenticationFlowTests). RegisterUserAsync attaches the Set-Cookie manually — the
        // deterministic pattern used by AdminUserFactory.
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ---- Task 2: persistence round-trip -------------------------------------

    [Fact]
    public async Task Repository_AppendsRows_AndGetLatestReturnsMostRecent()
    {
        var userId = await RegisterUserAsync(termsAccepted: true);

        // Age every existing acceptance row (the register row, if any) so the row we
        // add next is unambiguously the most recent by AcceptedAt.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await db.Set<TermsAcceptance>()
                .Where(t => t.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    t => t.AcceptedAt, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        int countBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            countBefore = await db.Set<TermsAcceptance>().CountAsync(t => t.UserId == userId);

            var repo = scope.ServiceProvider.GetRequiredService<ITermsAcceptanceRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await repo.AddAsync(TermsAcceptance.Create(userId, TermsVersion.Current, TermsAcceptanceContext.ReConsent));
            await uow.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITermsAcceptanceRepository>();
            var latest = await repo.GetLatestByUserIdAsync(userId);

            latest.Should().NotBeNull();
            latest!.TermsVersion.Should().Be(TermsVersion.Current);
            latest.Context.Should().Be(TermsAcceptanceContext.ReConsent);

            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var total = await db.Set<TermsAcceptance>().CountAsync(t => t.UserId == userId);
            total.Should().Be(countBefore + 1, "append-only: the new row is added, not overwritten");
        }
    }

    // ---- Task 4: registration records acceptance ----------------------------

    [Fact]
    public async Task Register_WithTermsAccepted_WritesExactlyOneRegistrationRow()
    {
        var userId = await RegisterUserAsync(termsAccepted: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var rows = await db.Set<TermsAcceptance>()
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .ToListAsync();

        rows.Should().HaveCount(1);
        rows[0].TermsVersion.Should().Be(TermsVersion.Current);
        rows[0].Context.Should().Be(TermsAcceptanceContext.Registration);
    }

    [Fact]
    public async Task Register_WithoutTermsAccepted_Returns400()
    {
        var response = await _client.PostAsJsonAsync(RegisterEndpoint, new
        {
            email = $"noterms-{Guid.NewGuid():N}@test.local",
            password = "ValidUnusualPwd123!",
            displayName = "No Terms",
            // termsAccepted omitted → defaults false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Task 5: accept + status endpoints ----------------------------------

    [Fact]
    public async Task Status_AfterRegister_ReturnsCurrentAccepted()
    {
        await RegisterUserAsync(termsAccepted: true); // _client is now authenticated

        var status = await _client.GetFromJsonAsync<TermsConsentStatusDto>(StatusEndpoint);

        status.Should().NotBeNull();
        status!.CurrentVersion.Should().Be(TermsVersion.Current);
        status.AcceptedVersion.Should().Be(TermsVersion.Current);
        status.NeedsReAcceptance.Should().BeFalse();
    }

    [Fact]
    public async Task Status_Unauthenticated_Returns401()
    {
        // No registration/login on this fresh client → no session cookie.
        var response = await _client.GetAsync(StatusEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Accept_WhenAlreadyCurrent_IsIdempotent()
    {
        var userId = await RegisterUserAsync(termsAccepted: true); // 1 Current row

        var accept = await _client.PostAsync(AcceptEndpoint, content: null);
        accept.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var currentRows = await db.Set<TermsAcceptance>()
            .AsNoTracking()
            .CountAsync(t => t.UserId == userId && t.TermsVersion == TermsVersion.Current);
        currentRows.Should().Be(1, "accepting the already-current version must not append a second row");
    }

    [Fact]
    public async Task StaleAcceptance_Accept_TransitionsToCurrent()
    {
        var userId = await RegisterUserAsync(termsAccepted: true);

        // Force the registration row stale (older version + older timestamp).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await db.Set<TermsAcceptance>()
                .Where(t => t.UserId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.TermsVersion, "2026-03-09")
                    .SetProperty(t => t.AcceptedAt, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        var before = await _client.GetFromJsonAsync<TermsConsentStatusDto>(StatusEndpoint);
        before!.NeedsReAcceptance.Should().BeTrue();
        before.AcceptedVersion.Should().Be("2026-03-09");

        var accept = await _client.PostAsync(AcceptEndpoint, content: null);
        accept.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await _client.GetFromJsonAsync<TermsConsentStatusDto>(StatusEndpoint);
        after!.NeedsReAcceptance.Should().BeFalse();
        after.AcceptedVersion.Should().Be(TermsVersion.Current);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var total = await db.Set<TermsAcceptance>().CountAsync(t => t.UserId == userId);
            total.Should().Be(2, "append-only: the stale row is preserved and a new current row is added");
        }
    }

    // ---- Helpers ------------------------------------------------------------

    /// <summary>
    /// Registers a user and returns the created user's id. Always sends termsAccepted;
    /// the field is ignored by the endpoint until the T4 change lands and required after.
    /// </summary>
    private async Task<Guid> RegisterUserAsync(bool termsAccepted)
    {
        var response = await _client.PostAsJsonAsync(RegisterEndpoint, new
        {
            email = $"terms-{Guid.NewGuid():N}@test.local",
            password = "ValidUnusualPwd123!",
            displayName = "Terms User",
            termsAccepted,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "registration should succeed for the happy path");

        // Attach the session cookie manually so subsequent authenticated requests
        // (GET /status, POST /accept) carry it deterministically.
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            var cookieHeader = string.Join("; ", setCookies.Select(c => c.Split(';')[0]));
            _client.DefaultRequestHeaders.Remove("Cookie");
            _client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var idString = body.GetProperty("user").GetProperty("id").GetString();
        return Guid.Parse(idString!);
    }
}

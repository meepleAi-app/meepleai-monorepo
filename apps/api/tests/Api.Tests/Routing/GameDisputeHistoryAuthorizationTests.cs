using System.Net;
using System.Text.Encodings.Web;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using Api.Routing;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// #4769 — <c>GET /api/v1/games/{gameId}/dispute-history</c> legge <c>DisputesJson</c> di TUTTE le sessioni
/// di un gioco, di chiunque siano, e restituisce descrizioni, verdetti, riferimenti e il nome di chi ha
/// sollevato la disputa. Con il solo <c>RequireAuthenticatedUser</c> bastava un account qualunque; da #4768 i
/// verdetti derivano dal regolamento indicizzato, quindi la lettura trasversale aggirava anche il cancello per
/// possesso. La rotta non ha consumatori: resta, ma solo per gli amministratori.
/// </summary>
/// <remarks>
/// <para>
/// <b>Perché la tabella di routing vera e un host in memoria.</b> Il filtro di endpoint non lascia traccia nei
/// metadati della rotta (entra nel <c>RequestDelegate</c> compilato), quindi non si può verificare leggendoli.
/// Qui si monta <see cref="LiveSessionEndpoints.MapLiveSessionEndpoints"/> con lo stesso prefisso di
/// <c>Program.cs</c> e si chiama la rotta. La sessione la mette in <c>HttpContext.Items</c> un middleware di
/// prova, che è ciò che fa <c>SessionAuthenticationMiddleware</c> in produzione; il mediatore è un doppio che
/// conta le chiamate, perché «il handler non è stato raggiunto» è l'asserzione che dice che nulla è stato letto.
/// </para>
/// <para>
/// Gira nella suite unitaria, cioè nel cancello delle PR: la sonda
/// <c>NoDuplicateRouteTests.EveryAdminRoute_RejectsAPlainUser</c> e le prove di integrazione girano solo dopo il
/// merge (#4032).
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4769")]
public sealed class GameDisputeHistoryAuthorizationTests
{
    private const string RoleHeader = "X-Test-Role";

    private static readonly Guid GameId = Guid.Parse("4769a000-0000-0000-0000-000000000001");

    private static string Url => $"/api/v1/games/{GameId}/dispute-history";

    [Fact]
    public async Task Anonymous_Gets401_AndTheHandlerIsNotReached()
    {
        var mediator = NewMediator();
        await using var app = await StartAppAsync(mediator);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri(Url, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        VerifyNothingWasRead(mediator);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Creator")]
    [InlineData("Editor")]
    public async Task AuthenticatedNonAdmin_Gets403_AndTheHandlerIsNotReached(string role)
    {
        var mediator = NewMediator();
        await using var app = await StartAppAsync(mediator);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Url, UriKind.Relative));
        request.Headers.Add(RoleHeader, role);
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "a session without the admin role must not read the verdicts of other people's sessions");
        VerifyNothingWasRead(mediator);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task Admin_Gets200_AndTheQueryCarriesTheRequestedGame(string role)
    {
        var mediator = NewMediator();
        await using var app = await StartAppAsync(mediator);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Url, UriKind.Relative));
        request.Headers.Add(RoleHeader, role);
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mediator.Verify(
            m => m.Send(
                It.Is<GetGameDisputeHistoryQuery>(q => q.GameId == GameId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// La sonda <c>NoDuplicateRouteTests.EveryAdminRoute_RejectsAPlainUser</c> elenca la rotta per prefisso, con la
    /// forma che la tabella di routing le da'. Qui si fissa quella forma nel cancello delle PR: se la rotta cambia
    /// modello, la sonda (che gira solo dopo il merge) non deve scoprirlo per prima.
    /// </summary>
    [Fact]
    public async Task TheRoute_IsRegisteredAtTheTemplateTheAdminProbeCovers()
    {
        await using var app = await StartAppAsync(NewMediator());

        var routes = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => string.Equals(
                (e.RoutePattern.RawText ?? string.Empty).Trim('/'),
                "api/v1/games/{gameId}/dispute-history",
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        routes.Should().ContainSingle();
        routes[0].Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Should().BeEquivalentTo(["GET"]);
    }

    // ── Host in memoria ────────────────────────────────────────────────────────

    private static Mock<IMediator> NewMediator()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GetGameDisputeHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetGameDisputeHistoryResult(GameId, [], 0));
        return mediator;
    }

    private static void VerifyNothingWasRead(Mock<IMediator> mediator) =>
        mediator.Verify(
            m => m.Send(It.IsAny<GetGameDisputeHistoryQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private static async Task<WebApplication> StartAppAsync(Mock<IMediator> mediator)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(mediator.Object);

        // Results.Forbid() chiede allo schema di autenticazione di rispondere 403; senza uno schema
        // registrato lancerebbe. Quello di produzione (SessionAuthenticationHandler) lascia il comportamento
        // di base, che è ciò che fa questo.
        builder.Services.AddAuthentication("probe").AddScheme<AuthenticationSchemeOptions, ProbeAuthenticationHandler>("probe", _ => { });

        var app = builder.Build();

        // Quello che fa SessionAuthenticationMiddleware: la sessione valida va in Items sotto il nome del DTO.
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue(RoleHeader, out var role))
            {
                context.Items[nameof(SessionStatusDto)] = SessionOf(role.ToString());
            }

            await next(context);
        });

        app.MapGroup("/api/v1").MapLiveSessionEndpoints();

        await app.StartAsync();
        return app;
    }

    private static SessionStatusDto SessionOf(string role)
    {
        var user = new UserDto(
            Id: Guid.NewGuid(),
            Email: "probe@example.com",
            DisplayName: "Probe",
            Role: role,
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

    private sealed class ProbeAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }
}

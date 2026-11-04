using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>Host e database per classe: i test creano ciascuno il proprio utente e il proprio gioco.</summary>
public sealed class ChunkedUploadEndpointsAuthorizationHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "chunked_upload_authz");

/// <summary>
/// #4395, dalla revisione: il caricamento a pezzi chiedeva solo una sessione valida, senza quota, senza
/// verificare che il gioco esista e senza controllare il proprietario di un gioco privato. Finché il
/// contenuto non arrivava al recupero il buco non aveva effetto; da quando il PDF si lega al gioco e ha
/// le righe vettoriali, un utente qualsiasi poteva aggiungere contenuto alla KB di un gioco del catalogo.
/// Tutti i chiamanti nel web sono pagine admin (wizard dei giochi, zona di caricamento della KB, import):
/// gli endpoint passano ad admin ed editor.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4395")]
public sealed class ChunkedUploadEndpointsAuthorizationTests
    : IClassFixture<ChunkedUploadEndpointsAuthorizationHostFixture>, IAsyncLifetime
{
    /// <summary>Il proprietario della riga di configurazione (chiave esterna su users).</summary>
    private static readonly Guid ConfigOwnerId = Guid.Parse("4395c0de-0000-4000-8000-000000004395");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ChunkedUploadEndpointsAuthorizationTests(ChunkedUploadEndpointsAuthorizationHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    /// <summary>
    /// L'init controlla il flag Features.PdfUpload PRIMA del ruolo, e in un database nuovo il flag manca
    /// (fail-safe a false): senza questa riga ogni init risponde 403 «feature_disabled», e il test
    /// dell'utente normale passerebbe per il motivo sbagliato. Va scritta prima della prima richiesta
    /// della classe, perché il valore letto può finire in cache. Idempotente.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        if (await db.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().AnyAsync(c => c.Key == "Features.PdfUpload", Ct))
        {
            return;
        }

        if (!await db.Users.AnyAsync(u => u.Id == ConfigOwnerId, Ct))
        {
            db.Users.Add(new Api.Infrastructure.Entities.UserEntity
            {
                Id = ConfigOwnerId,
                Email = "chunked-upload-config-owner@meepleai.invalid",
                DisplayName = "Config owner (#4395 test)",
                Role = "admin",
                CreatedAt = DateTime.UtcNow,
            });
        }

        db.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().Add(new Api.Infrastructure.Entities.SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = "Features.PdfUpload",
            Value = "true",
            ValueType = "bool",
            Description = "#4395 test: PDF upload enabled, so the role check is what answers",
            Category = "Features",
            IsActive = true,
            RequiresRestart = false,
            Environment = "All",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = ConfigOwnerId,
        });
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ARegularUser_CannotStartAChunkedUpload()
    {
        var (token, gameId) = await SessionAndGameAsync((db, id) => TestSessionHelper.CreateUserSessionAsync(db, id));

        var response = await _client.SendAsync(InitRequest(token, gameId), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(Ct)).Should().NotContain("feature_disabled",
            "il 403 deve venire dal ruolo, non dal flag delle funzionalità");
    }

    [Theory]
    [InlineData("POST", "/api/v1/ingest/pdf/chunked/complete")]
    [InlineData("POST", "/api/v1/ingest/pdf/chunked/chunk")]
    [InlineData("GET", "/api/v1/ingest/pdf/chunked/{0}/status")]
    public async Task ARegularUser_CannotUseTheOtherChunkedEndpoints(string method, string route)
    {
        var (token, _) = await SessionAndGameAsync((db, id) => TestSessionHelper.CreateUserSessionAsync(db, id));
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            new HttpMethod(method), string.Format(System.Globalization.CultureInfo.InvariantCulture, route, Guid.NewGuid()), token);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { sessionId = Guid.NewGuid() });
        }

        var response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnEditor_StartsAChunkedUploadForAnExistingCatalogueGame()
    {
        var (token, gameId) = await SessionAndGameAsync((db, id) => TestSessionHelper.CreateEditorSessionAsync(db, id));

        var response = await _client.SendAsync(InitRequest(token, gameId), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task AnAdmin_CannotStartAChunkedUploadForAGameThatDoesNotExist()
    {
        // Prima il record nasceva con lo SharedGameId di un gioco inesistente, e il salvataggio cadeva
        // sulla chiave esterna a caricamento finito, lasciando un file orfano nello storage.
        var (token, _) = await SessionAndGameAsync((db, id) => TestSessionHelper.CreateAdminSessionAsync(db, id));

        var response = await _client.SendAsync(InitRequest(token, Guid.NewGuid()), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("not found");
    }

    private static HttpRequestMessage InitRequest(string token, Guid gameId) =>
        TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/ingest/pdf/chunked/init",
            token,
            new { gameId, fileName = "rules.pdf", totalFileSize = 2048 });

    private async Task<(string Token, Guid GameId)> SessionAndGameAsync(
        Func<MeepleAiDbContext, Guid?, Task<(Guid UserId, string RawToken)>> createSession)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await createSession(db, Guid.NewGuid());
        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"Chunked {Guid.NewGuid():N}");
        return (token, gameId);
    }
}

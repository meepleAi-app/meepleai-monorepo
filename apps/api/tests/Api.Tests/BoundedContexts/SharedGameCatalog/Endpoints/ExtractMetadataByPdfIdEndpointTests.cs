using System.Net;
using System.Text.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Endpoints;

/// <summary>
/// #4459: il contratto di <c>GET /api/v1/admin/shared-games/extract-metadata/{pdfId}</c>, letto dal
/// passo 2 del wizard di import, fissato sul lato backend.
/// </summary>
/// <remarks>
/// <para>
/// Il client leggeva <c>yearPublished</c> e <c>playingTimeMinutes</c> mentre l'API serializza
/// <c>GameMetadataDto</c> come <c>year</c> e <c>playingTime</c>, e voleva un <c>title</c> non nullo
/// mentre il risultato vuoto lo manda <c>null</c>. Lo schema del client ora usa i nomi del backend
/// (<c>sharedGamesClient.test.ts</c>); questo test vieta di cambiarli dall'altra parte senza
/// accorgersene. Rinominare le proprietà del DTO romperebbe anche il prompt dell'LLM, che chiede
/// «Year» e «PlayingTime» e deserializza in quel DTO.
/// </para>
/// <para>
/// Gira sull'host vero, quindi con le opzioni JSON configurate in <c>Program.cs</c>. Un PDF che
/// non esiste fa restituire all'handler <c>GameMetadataDto.CreateEmpty()</c>: lo stesso corpo
/// osservato il 2026-10-16 quando l'LLM non risponde, con ogni chiave presente e nulla.
/// </para>
/// <para>
/// Un solo test: l'host si costruisce qui, non in una fixture per classe (#4050, il pareggio è a
/// due test).
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ExtractMetadataByPdfIdEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName = $"extract_meta_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public ExtractMetadataByPdfIdEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

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
    }

    [Fact]
    public async Task EmptyResult_UsesTheNamesTheWizardReads_WithANullTitle()
    {
        string sessionToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/admin/shared-games/extract-metadata/{Guid.NewGuid()}");
        request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={sessionToken}");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var json = JsonDocument.Parse(body);
        var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        names.Should().Contain(["title", "year", "playingTime", "confidenceScore"]);
        names.Should().NotContain(["yearPublished", "playingTimeMinutes"]);
        json.RootElement.GetProperty("title").ValueKind.Should().Be(JsonValueKind.Null);
    }
}

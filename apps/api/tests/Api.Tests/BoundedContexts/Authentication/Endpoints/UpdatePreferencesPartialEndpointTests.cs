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

namespace Api.Tests.BoundedContexts.Authentication.Endpoints;

/// <summary>
/// La fixture di <see cref="UpdatePreferencesPartialEndpointTests"/>: host per classe, database per test.
/// </summary>
public sealed class UpdatePreferencesPartialHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "prefs_partial");

/// <summary>
/// #4157 — <c>PUT /api/v1/users/preferences</c> aggiorna solo i campi che riceve.
/// </summary>
/// <remarks>
/// La pagina <c>/settings/preferences</c> manda tema, lingua e notifiche. Il comando sostituiva tutto,
/// quindi <c>DataRetentionDays</c> assente valeva 0 e la richiesta rispondeva 422: nessuno poteva
/// salvare le preferenze.
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4157")]
public sealed class UpdatePreferencesPartialEndpointTests
    : IClassFixture<UpdatePreferencesPartialHostFixture>, IAsyncLifetime
{
    private const string PreferencesUrl = "/api/v1/users/preferences";

    private readonly UpdatePreferencesPartialHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _token = null!;

    public UpdatePreferencesPartialEndpointTests(UpdatePreferencesPartialHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // 🔴 BeginTestAsync DEVE precedere l'uso di _factory (#4050).
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        Guid userId;
        (userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db);

        // Un valore diverso dal default (90): se il salvataggio lo riportasse al default, si vedrebbe.
        var user = await db.Users.AsTracking().SingleAsync(u => u.Id == userId);
        user.Language = "en";
        user.Theme = "light";
        user.DataRetentionDays = 30;
        await db.SaveChangesAsync();

        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<JsonElement> GetPreferencesAsync()
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, PreferencesUrl, _token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    [Fact]
    public async Task The_body_of_the_preferences_page_saves_and_keeps_the_fields_it_does_not_send()
    {
        // Esattamente ciò che manda PreferencesSection.
        var body = new { theme = "dark", language = "it", emailNotifications = false };

        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Put, PreferencesUrl, _token, body));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        // Riletta, come dopo il ricaricamento della pagina.
        var saved = await GetPreferencesAsync();
        saved.GetProperty("language").GetString().Should().Be("it");
        saved.GetProperty("theme").GetString().Should().Be("dark");
        saved.GetProperty("emailNotifications").GetBoolean().Should().BeFalse();
        saved.GetProperty("dataRetentionDays").GetInt32().Should().Be(30,
            "un campo che la richiesta non manda resta com'era, non torna al suo default");
    }

    [Fact]
    public async Task An_explicit_data_retention_of_zero_is_still_rejected_and_changes_nothing()
    {
        var body = new { language = "it", dataRetentionDays = 0 };

        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Put, PreferencesUrl, _token, body));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var saved = await GetPreferencesAsync();
        saved.GetProperty("language").GetString().Should().Be("en");
        saved.GetProperty("dataRetentionDays").GetInt32().Should().Be(30);
    }
}

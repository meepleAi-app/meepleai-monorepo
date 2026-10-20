using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola (vedi <see cref="IntegrationHostFixture"/>).
/// <para>
/// 🔴 <b>Perché condividere il database è sicuro QUI.</b> Ogni test crea la propria sessione admin con un
/// utente nuovo e lavora su una chiave di configurazione propria (<c>Test4552.{guid}</c>). Le asserzioni
/// leggono solo la riga di quella chiave, anche quando l'endpoint restituisce l'elenco completo.
/// </para>
/// </summary>
public sealed class AlertConfigurationEndpointsHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "alert_config_endpoints");

/// <summary>
/// #4552: <c>AlertConfigurationRepository</c> ricostruiva la riga con <c>AlertConfiguration.Create</c>, la factory
/// di una configurazione nuova. A ogni lettura l'aggregato aveva un id nuovo, <c>UpdatedAt</c> ad "adesso" e
/// <c>IsEncrypted = false</c>. La modifica di una chiave esistente (<c>PUT /api/v1/admin/alert-configuration</c>)
/// cercava la riga per quell'id inventato e falliva sempre; la lettura restituiva id, data e flag sbagliati.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4552")]
public sealed class AlertConfigurationEndpointsIntegrationTests : IClassFixture<AlertConfigurationEndpointsHostFixture>
{
    private const string Endpoint = "/api/v1/admin/alert-configuration";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AlertConfigurationEndpointsIntegrationTests(AlertConfigurationEndpointsHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    [Fact]
    public async Task Put_ExistingKey_Returns200AndUpdatesTheSameRow()
    {
        var token = await CreateAdminSessionTokenAsync();
        var key = $"Test4552.{Guid.NewGuid():N}";

        // La riga la scrive l'endpoint, non il test: è quella che il sistema produce. Una chiave nuova segue il
        // ramo AddAsync, che funzionava anche prima della correzione.
        var created = await PutAsync(token, key, "valore-iniziale", "Email", "descrizione iniziale");
        created.Status.Should().Be(HttpStatusCode.OK, $"setup: a new key must be created. Body: {created.Body}");

        // Una data nel passato, così l'avanzamento di UpdatedAt non dipende dai microsecondi fra le due richieste.
        var storedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        await SetUpdatedAtAsync(key, storedAt);
        var before = await ReadRowAsync(key);

        var updated = await PutAsync(token, key, "valore-aggiornato", "Email", "descrizione ignorata");

        updated.Status.Should().Be(HttpStatusCode.OK,
            $"updating an existing key must work. Before #4552 the read aggregate had a new id, UpdateAsync did " +
            $"not find the row and threw, and the endpoint answered 404. Body: {updated.Body}");

        var rows = await ReadRowsAsync(key);
        rows.Should().ContainSingle("the update must change the existing row, not add one");
        var after = rows[0];
        after.Id.Should().Be(before.Id, "the row keeps its id");
        after.ConfigValue.Should().Be("valore-aggiornato");
        after.IsEncrypted.Should().Be(before.IsEncrypted, "the value is not encrypted by this endpoint, and the flag is not touched");
        after.IsEncrypted.Should().BeFalse("the row was written by the endpoint, which never encrypts");
        after.UpdatedAt.Should().BeAfter(storedAt).And.BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Il ramo di modifica cambia solo valore, data e autore: categoria e descrizione restano quelle salvate.
        after.Category.Should().Be(before.Category);
        after.Description.Should().Be(before.Description);
    }

    [Fact]
    public async Task Put_ExistingEncryptedKey_StoresThePlainValueAsNotEncrypted()
    {
        var token = await CreateAdminSessionTokenAsync();
        var key = $"Test4552.{Guid.NewGuid():N}";

        // Riga marcata cifrata scritta direttamente: AlertConfiguration.Encrypt non ha chiamanti.
        var row = new AlertConfigurationEntity
        {
            Id = Guid.NewGuid(),
            ConfigKey = key,
            ConfigValue = "valore-cifrato",
            Category = "PagerDuty",
            IsEncrypted = true,
            UpdatedAt = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            UpdatedBy = "autore-salvato",
        };
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            db.AlertConfigurations.Add(row);
            await db.SaveChangesAsync();
        }

        var updated = await PutAsync(token, key, "valore-in-chiaro", "PagerDuty", "ignorata");

        updated.Status.Should().Be(HttpStatusCode.OK, $"Body: {updated.Body}");
        var after = await ReadRowAsync(key);
        after.Id.Should().Be(row.Id);
        after.ConfigValue.Should().Be("valore-in-chiaro");
        after.IsEncrypted.Should().BeFalse(
            "the endpoint writes the value as it arrives, in plain text: a row left flagged as encrypted would " +
            "claim a cipher text it does not hold");
    }

    [Fact]
    public async Task GetAll_ReturnsIdEncryptionFlagAndDateOfTheStoredRow()
    {
        var token = await CreateAdminSessionTokenAsync();
        var key = $"Test4552.{Guid.NewGuid():N}";

        // Riga scritta direttamente: nessun percorso del sistema oggi marca un valore come cifrato
        // (AlertConfiguration.Encrypt non ha chiamanti), ma la colonna esiste e la lettura deve riportarla.
        var row = new AlertConfigurationEntity
        {
            Id = Guid.NewGuid(),
            ConfigKey = key,
            ConfigValue = "valore-cifrato",
            Category = "Slack",
            IsEncrypted = true,
            Description = "riga salvata",
            UpdatedAt = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            UpdatedBy = "autore-salvato",
        };
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            db.AlertConfigurations.Add(row);
            await db.SaveChangesAsync();
        }

        using var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, Endpoint, token);
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Body: {body}");

        using var json = JsonDocument.Parse(body);
        var item = json.RootElement.EnumerateArray()
            .Single(e => string.Equals(e.GetProperty("configKey").GetString(), key, StringComparison.Ordinal));

        item.GetProperty("id").GetGuid().Should().Be(row.Id,
            "the id is the row's. Before #4552 every read invented a new one");
        item.GetProperty("isEncrypted").GetBoolean().Should().BeTrue(
            "the flag is the row's. Before #4552 every read reset it to false");
        item.GetProperty("updatedAt").GetDateTime().ToUniversalTime().Should().Be(row.UpdatedAt,
            "the date is the row's. Before #4552 every read stamped the current time");
        item.GetProperty("configValue").GetString().Should().Be(row.ConfigValue);
        item.GetProperty("category").GetString().Should().Be("Slack");
        item.GetProperty("description").GetString().Should().Be(row.Description);
        item.GetProperty("updatedBy").GetString().Should().Be(row.UpdatedBy);
    }

    private async Task<string> CreateAdminSessionTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateAdminSessionAsync(db);
        return token;
    }

    private async Task<(HttpStatusCode Status, string Body)> PutAsync(
        string token, string key, string value, string category, string description)
    {
        using var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Put, Endpoint, token);
        request.Content = JsonContent.Create(new { configKey = key, configValue = value, category, description });
        using var response = await _client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task SetUpdatedAtAsync(string key, DateTime updatedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var changed = await db.AlertConfigurations
            .Where(c => c.ConfigKey == key)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, updatedAt));
        changed.Should().Be(1, "setup: exactly the row of this test");
    }

    private async Task<AlertConfigurationEntity> ReadRowAsync(string key) => (await ReadRowsAsync(key)).Single();

    private async Task<List<AlertConfigurationEntity>> ReadRowsAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.AlertConfigurations.AsNoTracking().Where(c => c.ConfigKey == key).ToListAsync();
    }
}

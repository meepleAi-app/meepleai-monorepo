using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Integration;

/// <summary>
/// Host per classe, database per test (#4050): ogni test semina la propria riga del bootstrap, quindi nessuno
/// deve trovare quella di un altro. Il token è generato a ogni esecuzione: non è mai un segreto vero.
/// </summary>
public sealed class BootstrapAdminClaimHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "bootstrap_claim")
{
    public static readonly string BootstrapToken = $"test-{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}";

    protected override Dictionary<string, string?>? ExtraConfiguration => new()
    {
        ["Authentication:BootstrapAdminToken"] = BootstrapToken,
    };
}

/// <summary>
/// #4612: il claim del primo admin si consuma una volta sola, e il flag che lo ricorda sta nella riga di
/// configurazione <c>Authentication:BootstrapAdmin</c>. Nessuna operazione dell'API admin sulle configurazioni
/// deve poterlo riaprire.
/// </summary>
/// <remarks>
/// <para>
/// Il difetto aveva due porte. (1) <c>ConfigurationRepository.UpdateAsync</c> riscriveva le colonne
/// <c>BootstrapAdminCreated</c> e <c>BootstrapAdminCreatedAt</c> al default a ogni update della riga, perché
/// l'aggregato non le conosce: update, toggle, bulk-update, rollback e import con overwrite (quindi anche un
/// export seguito da un import). (2) <c>DELETE</c> toglieva la riga, e il ramo «primo claim» di
/// <c>RegisterCommandHandler</c> la ricrea e promuove.
/// </para>
/// <para>
/// Ogni test dei percorsi di scrittura controlla prima l'invariante (il flag nel database e l'esito di una
/// registrazione col token) e poi la risposta dell'API: prima della correzione fallisce sull'invariante, che è
/// il difetto, non sul codice di stato. I due controlli di contorno in fondo servono a provare che le
/// asserzioni possono fallire: con il flag a <c>false</c> la stessa registrazione promuove, e una chiave
/// qualunque si modifica ancora.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4612")]
public sealed class BootstrapAdminClaimIntegrationTests : IClassFixture<BootstrapAdminClaimHostFixture>, IAsyncLifetime
{
    private const string BootstrapKey = "Authentication:BootstrapAdmin";
    private const string ConfigurationsEndpoint = "/api/v1/admin/configurations";
    private const string RegisterEndpoint = "/api/v1/auth/register";

    // Nel passato: una data riscritta con "adesso" si vede.
    private static readonly DateTime ClaimedAt = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    private readonly BootstrapAdminClaimHostFixture _host;
    private Guid _adminId;
    private string _adminToken = string.Empty;

    public BootstrapAdminClaimIntegrationTests(BootstrapAdminClaimHostFixture host)
    {
        _host = host;
    }

    public async ValueTask InitializeAsync()
    {
        await _host.BeginTestAsync();

        using var scope = _host.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_adminId, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db);

        // /auth/register è chiuso finché Registration:PublicEnabled non è true. Lo si abilita con la riga vera,
        // quella che legge ConfigurationService, e non con un doppio: il claim resta sul percorso di produzione.
        db.Set<SystemConfigurationEntity>().Add(NewRow("Registration:PublicEnabled", "true", "bool", "Registration"));
        await db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ---------------------------------------------------------------------------------------------------
    // Percorsi che riscrivevano il flag
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Put_OnTheBootstrapRow_KeepsTheClaimClosed()
    {
        var row = await SeedClaimedBootstrapRowAsync();

        var (status, body) = await SendAsync(HttpMethod.Put, $"{ConfigurationsEndpoint}/{row}", new { value = "false" });

        await AssertClaimStaysClosedAsync(row);
        status.Should().Be(HttpStatusCode.Forbidden, $"the reserved row is not editable through the admin API. Body: {body}");
    }

    [Fact]
    public async Task Toggle_OnTheBootstrapRow_KeepsTheClaimClosed()
    {
        var row = await SeedClaimedBootstrapRowAsync();

        var (status, body) = await SendAsync(HttpMethod.Patch, $"{ConfigurationsEndpoint}/{row}/toggle?isActive=false");

        await AssertClaimStaysClosedAsync(row);
        status.Should().Be(HttpStatusCode.Forbidden, $"Body: {body}");
    }

    [Fact]
    public async Task BulkUpdate_IncludingTheBootstrapRow_KeepsTheClaimClosed_AndChangesNothingElse()
    {
        var row = await SeedClaimedBootstrapRowAsync();
        var ordinary = await SeedOrdinaryRowAsync("Test4612:Bulk", "prima");

        var (status, body) = await SendAsync(
            HttpMethod.Post,
            $"{ConfigurationsEndpoint}/bulk-update",
            new { updates = new[] { new { id = ordinary.ToString(), value = "dopo" }, new { id = row.ToString(), value = "false" } } });

        await AssertClaimStaysClosedAsync(row);
        status.Should().Be(HttpStatusCode.Forbidden, $"Body: {body}");
        (await ReadRowAsync(ordinary)).Value.Should().Be("prima",
            "a bulk update is atomic: one refused item refuses the whole request");
    }

    [Fact]
    public async Task Rollback_OnTheBootstrapRow_KeepsTheClaimClosed()
    {
        // Il rollback esiste solo se la riga ha un valore precedente, cioè dopo un update: lo si semina così.
        var row = await SeedClaimedBootstrapRowAsync(previousValue: "false");

        var (status, body) = await SendAsync(HttpMethod.Post, $"{ConfigurationsEndpoint}/{row}/rollback/1");

        await AssertClaimStaysClosedAsync(row);
        status.Should().Be(HttpStatusCode.Forbidden, $"Body: {body}");
    }

    [Fact]
    public async Task ImportWithOverwrite_OfTheBootstrapKey_SkipsItAndKeepsTheClaimClosed()
    {
        var row = await SeedClaimedBootstrapRowAsync();

        var (status, body) = await SendAsync(
            HttpMethod.Post,
            $"{ConfigurationsEndpoint}/import",
            new
            {
                overwriteExisting = true,
                configurations = new[]
                {
                    new { key = BootstrapKey, value = "true", valueType = "bool", category = "Authentication", environment = "All" },
                },
            });

        await AssertClaimStaysClosedAsync(row);
        status.Should().Be(HttpStatusCode.OK, $"Body: {body}");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("importedCount").GetInt32().Should().Be(0);
        json.RootElement.GetProperty("skippedKeys").EnumerateArray().Select(e => e.GetString())
            .Should().Equal(BootstrapKey);
    }

    [Fact]
    public async Task ExportThenImportWithOverwrite_KeepsTheClaimClosed()
    {
        // È il caso che nessuno tocca di proposito: un backup o la copia di una configurazione fra ambienti.
        var row = await SeedClaimedBootstrapRowAsync();
        await SeedOrdinaryRowAsync("Test4612:RoundTrip", "valore");

        var (exportStatus, exportBody) = await SendAsync(HttpMethod.Get, $"{ConfigurationsEndpoint}/export?environment=Development&activeOnly=false");
        exportStatus.Should().Be(HttpStatusCode.OK, $"Body: {exportBody}");
        using var export = JsonDocument.Parse(exportBody);
        var exported = export.RootElement.GetProperty("configurations").EnumerateArray().ToList();
        var exportedKeys = exported.Select(c => c.GetProperty("key").GetString()).ToList();
        exportedKeys.Should().Contain("Test4612:RoundTrip", "setup: the export carries the ordinary rows");

        var items = exported.Select(c => new
        {
            key = c.GetProperty("key").GetString(),
            value = c.GetProperty("value").GetString(),
            valueType = c.GetProperty("valueType").GetString(),
            category = c.GetProperty("category").GetString(),
            isActive = c.GetProperty("isActive").GetBoolean(),
            requiresRestart = c.GetProperty("requiresRestart").GetBoolean(),
            environment = c.GetProperty("environment").GetString(),
        }).ToList();
        var (importStatus, importBody) = await SendAsync(
            HttpMethod.Post, $"{ConfigurationsEndpoint}/import", new { overwriteExisting = true, configurations = items });

        await AssertClaimStaysClosedAsync(row);
        importStatus.Should().Be(HttpStatusCode.OK, $"Body: {importBody}");
        exportedKeys.Should().NotContain(BootstrapKey, "the reserved row does not leave through the export");
    }

    [Fact]
    public async Task Delete_OfTheBootstrapRow_KeepsTheRowAndTheClaimClosed()
    {
        var row = await SeedClaimedBootstrapRowAsync();

        var (status, body) = await SendAsync(HttpMethod.Delete, $"{ConfigurationsEndpoint}/{row}");

        // Cancellata la riga, il ramo «primo claim» la ricrea e promuove: proteggere le due colonne non basta.
        await AssertClaimStaysClosedAsync(row);
        status.Should().Be(HttpStatusCode.Forbidden, $"Body: {body}");
    }

    // ---------------------------------------------------------------------------------------------------
    // La riga non si crea dall'API
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_OfTheBootstrapKey_IsRefused_AndLeavesNoRow()
    {
        var (status, body) = await SendAsync(
            HttpMethod.Post,
            ConfigurationsEndpoint,
            new { key = BootstrapKey, value = "true", valueType = "bool", category = "Authentication", environment = "All" });

        status.Should().Be(HttpStatusCode.Forbidden, $"Body: {body}");
        (await ReadBootstrapRowsAsync()).Should().BeEmpty("a row authored through the API would start with the claim open");
    }

    [Fact]
    public async Task Import_OfTheBootstrapKey_WhenTheRowIsAbsent_CreatesNothing()
    {
        var (status, body) = await SendAsync(
            HttpMethod.Post,
            $"{ConfigurationsEndpoint}/import",
            new
            {
                overwriteExisting = false,
                configurations = new[]
                {
                    new { key = BootstrapKey, value = "true", valueType = "bool", category = "Authentication", environment = "All" },
                },
            });

        status.Should().Be(HttpStatusCode.OK, $"Body: {body}");
        (await ReadBootstrapRowsAsync()).Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------------------
    // Lettura: la riga si vede, non si esporta
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_ShowsTheBootstrapRow_ReadOnly()
    {
        await SeedClaimedBootstrapRowAsync();

        var (status, body) = await SendAsync(HttpMethod.Get, $"{ConfigurationsEndpoint}?activeOnly=false&pageSize=200");

        status.Should().Be(HttpStatusCode.OK, $"Body: {body}");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString())
            .Should().Contain(BootstrapKey, "an admin can see that the claim was consumed, even if they cannot change it");
    }

    // ---------------------------------------------------------------------------------------------------
    // Repository, senza passare dall'API
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Repository_UpdateAsync_OnPostgres_KeepsTheBootstrapColumns()
    {
        // L'API ora rifiuta la riga prima di arrivare qui: questo test tiene ferma la prima delle due difese da sola.
        var row = await SeedClaimedBootstrapRowAsync();

        using (var scope = _host.Factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IConfigurationRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var config = await repository.GetByIdAsync(row);
            config!.UpdateValue("false", _adminId);
            await repository.UpdateAsync(config);
            await unitOfWork.SaveChangesAsync();
        }

        var after = await ReadRowAsync(row);
        after.Value.Should().Be("false", "setup: the update did run");
        after.BootstrapAdminCreated.Should().BeTrue("UpdateAsync must not write the columns the aggregate does not own");
        after.BootstrapAdminCreatedAt.Should().Be(ClaimedAt);
    }

    // ---------------------------------------------------------------------------------------------------
    // Controlli di contorno
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Control_WithTheFlagAtFalse_TheSameRegistrationPromotes()
    {
        // Senza questo, «non promuove» passerebbe anche con una registrazione che non può promuovere mai.
        var row = await SeedBootstrapRowAsync(claimed: false);

        var (status, role) = await RegisterWithTheTokenAsync();

        status.Should().Be(HttpStatusCode.OK);
        role.Should().Be("admin", "with the claim open, the configured token promotes: the harness can tell the two cases apart");
        (await ReadRowAsync(row)).BootstrapAdminCreated.Should().BeTrue("the claim consumes the flag");
    }

    [Fact]
    public async Task Control_AnOrdinaryKey_IsStillEditableAndRollbackable()
    {
        var ordinary = await SeedOrdinaryRowAsync("Test4612:Ordinary", "uno");

        var (putStatus, putBody) = await SendAsync(HttpMethod.Put, $"{ConfigurationsEndpoint}/{ordinary}", new { value = "due" });
        putStatus.Should().Be(HttpStatusCode.OK, $"Body: {putBody}");
        (await ReadRowAsync(ordinary)).Value.Should().Be("due");

        var (rollbackStatus, rollbackBody) = await SendAsync(HttpMethod.Post, $"{ConfigurationsEndpoint}/{ordinary}/rollback/1");
        rollbackStatus.Should().Be(HttpStatusCode.OK, $"Body: {rollbackBody}");
        (await ReadRowAsync(ordinary)).Value.Should().Be("uno");
    }

    // ---------------------------------------------------------------------------------------------------
    // Aiuti
    // ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Il flag nel database e l'esito di una registrazione col token, valutati insieme: se il flag è stato
    /// riscritto vedi entrambi i sintomi, non solo il primo.
    /// </summary>
    private async Task AssertClaimStaysClosedAsync(Guid row)
    {
        var rows = await ReadBootstrapRowsAsync();
        var flagAfterTheOperation = rows.Count == 1 ? rows[0].BootstrapAdminCreated : (bool?)null;
        var claimedAtAfterTheOperation = rows.Count == 1 ? rows[0].BootstrapAdminCreatedAt : null;

        var (status, role) = await RegisterWithTheTokenAsync();

        using var scope = new AssertionScope();
        rows.Select(r => r.Id).Should().Equal([row], "the reserved row must still be there, once, with its own id");
        flagAfterTheOperation.Should().BeTrue("the single-use flag is the only thing that closes the claim");
        claimedAtAfterTheOperation.Should().Be(ClaimedAt, "the claim date is not rewritten either");
        status.Should().Be(HttpStatusCode.OK, "a registration with the token still creates the account");
        role.Should().Be("user", "a registration with the bootstrap token must not promote once the claim was consumed");
    }

    private async Task<(HttpStatusCode Status, string? Role)> RegisterWithTheTokenAsync()
    {
        using var response = await _host.Client.PostAsJsonAsync(RegisterEndpoint, new
        {
            Email = $"claim-{Guid.NewGuid():N}@test.local",
            Password = "ValidUnusualPwd123!",
            DisplayName = "Claim Probe",
            BootstrapToken = BootstrapAdminClaimHostFixture.BootstrapToken,
            TermsAccepted = true,
        });
        string? role = null;
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            role = body.GetProperty("user").GetProperty("role").GetString();
        }

        return (response.StatusCode, role);
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpMethod method, string url, object? payload = null)
    {
        using var request = TestSessionHelper.CreateAuthenticatedRequest(method, url, _adminToken);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        using var response = await _host.Client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private SystemConfigurationEntity NewRow(string key, string value, string valueType, string category) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Value = value,
        ValueType = valueType,
        Category = category,
        Environment = "All",
        IsActive = true,
        Version = 1,
        CreatedByUserId = _adminId,
        UpdatedByUserId = _adminId,
        CreatedAt = ClaimedAt,
        UpdatedAt = ClaimedAt,
    };

    private Task<Guid> SeedClaimedBootstrapRowAsync(string? previousValue = null) =>
        SeedBootstrapRowAsync(claimed: true, previousValue);

    private async Task<Guid> SeedBootstrapRowAsync(bool claimed, string? previousValue = null)
    {
        var row = NewRow(BootstrapKey, "true", "bool", "Authentication");
        row.Description = "Bootstrap-admin single-use guard (C5).";
        row.PreviousValue = previousValue;
        row.BootstrapAdminCreated = claimed;
        row.BootstrapAdminCreatedAt = claimed ? ClaimedAt : null;

        using var scope = _host.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Set<SystemConfigurationEntity>().Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private async Task<Guid> SeedOrdinaryRowAsync(string key, string value)
    {
        var row = NewRow(key, value, "string", "Test4612");
        using var scope = _host.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Set<SystemConfigurationEntity>().Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private async Task<SystemConfigurationEntity> ReadRowAsync(Guid id)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.Set<SystemConfigurationEntity>().AsNoTracking().SingleAsync(c => c.Id == id);
    }

    private async Task<List<SystemConfigurationEntity>> ReadBootstrapRowsAsync()
    {
        using var scope = _host.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.Set<SystemConfigurationEntity>().AsNoTracking().Where(c => c.Key == BootstrapKey).ToListAsync();
    }
}

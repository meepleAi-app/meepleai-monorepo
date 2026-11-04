using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// La fixture di <see cref="SharedHostPerTestDatabaseFixtureTests"/>.
/// </summary>
public sealed class ProbeHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "probe_per_test_db");

/// <summary>
/// #4050 — la prova COMPORTAMENTALE che <see cref="SharedHostPerTestDatabaseFixture"/> mantiene le
/// due promesse che fa: <b>l'host è condiviso</b> e <b>il database non lo è</b>.
/// </summary>
/// <remarks>
/// <para>
/// Senza questi test ci sarebbe solo l'argomento: «<c>AddDbContext</c> con il service provider rende
/// le opzioni scoped, quindi il lambda rilegge la configurazione». L'argomento è verificato nel
/// codice, ma è un argomento: ciò che serve è vedere una riga scritta da un test <b>non</b>
/// comparire nel successivo.
/// </para>
/// <para>
/// 🔴 Il nome dei due test porta un ordinale perché xUnit, dentro una classe, esegue i metodi in
/// ordine non garantito. La coppia è quindi costruita per funzionare in <b>qualunque</b> ordine:
/// ciascuno scrive la propria riga e asserisce di non vedere quella dell'altro. Un test che
/// dipendesse dall'ordine sarebbe esattamente il difetto che la fixture a database condiviso
/// introduce e che questa variante esiste per evitare.
/// </para>
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "4050")]
public sealed class SharedHostPerTestDatabaseFixtureTests
    : IClassFixture<ProbeHostFixture>, IAsyncLifetime
{
    private readonly ProbeHostFixture _fixture;

    public SharedHostPerTestDatabaseFixtureTests(ProbeHostFixture fixture)
    {
        _fixture = fixture;
    }

    public ValueTask InitializeAsync() => _fixture.BeginTestAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task PrimoTest_ScriveUnaRigaEnonVedeQuellaDellAltro()
    {
        await ScriviEAssertiIsolamentoAsync("sonda-uno@example.com", "sonda-due@example.com");
    }

    [Fact]
    public async Task SecondoTest_ScriveUnaRigaEnonVedeQuellaDellAltro()
    {
        await ScriviEAssertiIsolamentoAsync("sonda-due@example.com", "sonda-uno@example.com");
    }

    [Fact]
    public void IlDatabaseDelTestCorrenteEQuelloCheLaConfigurazioneRiporta()
    {
        // Lega le due metà del meccanismo: il database creato da BeginTestAsync e la stringa che
        // l'host serve agli scope. Se divergessero, i test sopra passerebbero lo stesso — entrambi
        // contro lo STESSO database, purché vuoto — e l'isolamento sarebbe un caso fortunato.
        _fixture.CurrentDatabase.Should().NotBeNullOrWhiteSpace();

        using var scope = _fixture.CreateScopeOnCurrentDatabase();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = config.GetConnectionString("DefaultConnection");

        connectionString.Should().Contain(
            _fixture.CurrentDatabase!,
            "lo scope deve risolvere il database del test corrente, non quello del template");
    }

    [Fact]
    public async Task IlDatabaseDelTestEGiaMigrato_senzaMigrareDiNuovo()
    {
        // È il secondo risparmio della fixture: il database arriva clonato da un template già
        // migrato. Se il clone non portasse lo schema, i test sopra fallirebbero con un errore di
        // tabella mancante invece di dire questo.
        using var scope = _fixture.CreateScopeOnCurrentDatabase();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var applicate = await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        var pendenti = await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);

        applicate.Should().NotBeEmpty("il template è migrato una volta e il clone eredita lo schema");
        pendenti.Should().BeEmpty("se ci fossero migrazioni pendenti il clone non sarebbe utilizzabile");
    }

    private async Task ScriviEAssertiIsolamentoAsync(string mioEmail, string altroEmail)
    {
        using var scope = _fixture.CreateScopeOnCurrentDatabase();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        db.Users.Add(new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = mioEmail,
            DisplayName = "sonda #4050",
            PasswordHash = "x",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mia = await db.Users.AsNoTracking()
            .CountAsync(u => u.Email == mioEmail, TestContext.Current.CancellationToken);
        var altrui = await db.Users.AsNoTracking()
            .CountAsync(u => u.Email == altroEmail, TestContext.Current.CancellationToken);

        mia.Should().Be(1, "la riga scritta da questo test deve esserci");
        altrui.Should().Be(
            0,
            "la riga dell'altro test NON deve comparire: è la promessa di isolamento del database. "
            + "Se compare, la stringa di connessione non viene sostituita e tutti i test della "
            + "classe girano contro lo stesso database");
    }
}

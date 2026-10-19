using Api.Tests.Constants;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.FrontendSdk;

/// <summary>
/// I contratti di <see cref="FrontendSdkTestFactory"/> su cui contano le altre classi di questa
/// cartella. #4518.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reset.</b> Dopo <see cref="FrontendSdkTestFactory.ResetDatabaseAsync"/> nessuna tabella degli
/// schemi utente ha righe, tranne la storia delle migrazioni. Il reset cancella con i trigger
/// sospesi (<c>session_replication_role = 'replica'</c>), quindi non propaga niente per cascata: una
/// tabella che non è nell'elenco resta piena, con righe che possono puntare a genitori ormai
/// cancellati. La prima versione elencava solo lo schema <c>public</c>, mentre il modello ha tabelle
/// anche in altri schemi. Questi test non usano l'host: bastano il database della collection e la
/// sua stringa di connessione.
/// </para>
/// <para>
/// <b>HTTPS.</b> Un <c>CreateClient()</c> senza opzioni parla HTTPS: su <c>http://</c> il cookie di
/// sessione <c>secure</c> non torna indietro, e la richiesta dopo un login riuscito è 401.
/// </para>
/// </remarks>
[Collection(nameof(FrontendSdkTestCollection))]
[Trait("Category", TestCategories.Integration)]
public class FrontendSdkTestFactoryTests
{
    // Una tabella fuori da `public` in cui si può scrivere una riga con la sola chiave: tutte le
    // altre colonne obbligatorie hanno un default.
    private const string TableOutsidePublic = "\"SystemConfiguration\".\"IncidentBannerState\"";

    private readonly FrontendSdkTestFactory _factory;

    public FrontendSdkTestFactoryTests(FrontendSdkTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reset_EmptiesATableOutsideThePublicSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync(ct);

        await ExecuteAsync(connection, $"INSERT INTO {TableOutsidePublic} (\"Id\") VALUES (gen_random_uuid())", ct);
        (await CountAsync(connection, TableOutsidePublic, ct)).Should().BeGreaterThan(0, "la riga di prova deve esserci prima del reset");

        await _factory.ResetDatabaseAsync();

        (await CountAsync(connection, TableOutsidePublic, ct)).Should().Be(0);
    }

    [Fact]
    public async Task Reset_LeavesNoRowInAnyUserTable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync(ct);

        await ExecuteAsync(connection, $"INSERT INTO {TableOutsidePublic} (\"Id\") VALUES (gen_random_uuid())", ct);

        await _factory.ResetDatabaseAsync();

        // L'elenco qui sotto è indipendente dalla query del reset: tutte le tabelle che il server
        // conosce fuori dai cataloghi di sistema. Se il reset ne saltasse una con righe, compare qui
        // per nome.
        var tables = new List<string>();
        await using (var list = connection.CreateCommand())
        {
            list.CommandText =
                "SELECT quote_ident(schemaname) || '.' || quote_ident(tablename) FROM pg_tables " +
                "WHERE schemaname NOT LIKE 'pg\\_%' AND schemaname <> 'information_schema'";
            await using var reader = await list.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tables.Add(reader.GetString(0));
            }
        }

        tables.Should().Contain(t => !t.StartsWith("public.", StringComparison.Ordinal),
            "il modello ha tabelle fuori da public: è il caso che questo test esiste per coprire");

        var notEmpty = new List<string>();
        foreach (var table in tables.Where(t => t != "public.\"__EFMigrationsHistory\""))
        {
            if (await CountAsync(connection, table, ct) > 0)
            {
                notEmpty.Add(table);
            }
        }

        notEmpty.Should().BeEmpty("dopo il reset ogni tabella degli schemi utente deve essere vuota");
    }

    [Fact]
    public void CreateClient_WithoutOptions_UsesHttps()
    {
        using var client = _factory.CreateClient();

        client.BaseAddress.Should().Be(FrontendSdkTestFactory.HttpsBaseAddress);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // SQL costante del test, o nomi quotati dal server
        command.CommandText = sql;
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // nome costante del test, o quotato dal server con quote_ident
        command.CommandText = $"SELECT count(*) FROM {table}";
#pragma warning restore CA2100
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }
}

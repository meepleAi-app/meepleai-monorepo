using System;
using System.Threading.Tasks;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Issue #3633 — pinna il clone da database-modello.
///
/// <para>
/// Ognuna delle 362 classi di integrazione creava il proprio database e ci applicava tutte le
/// migration: 250 CreateTable e 727 CreateIndex, misurati in 5,1-7,4 s di solo SQL. Un
/// <c>CREATE DATABASE ... TEMPLATE</c> costa 135-159 ms perché è una copia a livello di file.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection("Integration-GroupA")]
public sealed class IsolatedDatabaseTemplateTests
{
    private readonly SharedTestcontainersFixture _fixture;

    public IsolatedDatabaseTemplateTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task WithTemplate_TheDatabaseArrivesWithTheSchemaAlreadyApplied()
    {
        var databaseName = $"test_tplon_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName, useTemplate: true);

        try
        {
            (await TableExistsAsync(connectionString, "__EFMigrationsHistory"))
                .Should().BeTrue(
                    "clonare il modello copia anche la history di EF. È ciò che rende il " +
                    "MigrateAsync() già presente nelle 362 classi un no-op invece di una " +
                    "riesecuzione: senza, il guadagno sparirebbe e i file di test andrebbero toccati");
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task WithoutTemplate_TheDatabaseArrivesEmpty()
    {
        var databaseName = $"test_tploff_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName, useTemplate: false);

        try
        {
            (await TableExistsAsync(connectionString, "__EFMigrationsHistory"))
                .Should().BeFalse(
                    "l'opt-out serve ai canary delle migration, che devono continuare a esercitare " +
                    "il percorso vero: se il modello arrivasse anche a loro, una migration rotta " +
                    "smetterebbe di essere intercettata");
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task TheTemplate_RefusesConnections()
    {
        // Costruisce il modello come effetto collaterale.
        var databaseName = $"test_tplseal_{Guid.NewGuid():N}";
        await _fixture.CreateIsolatedDatabaseAsync(databaseName, useTemplate: true);
        await _fixture.DropIsolatedDatabaseAsync(databaseName);

        var builder = new NpgsqlConnectionStringBuilder(_fixture.PostgresConnectionString)
        {
            Database = "meepleai_test_template",
        };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        var connect = async () => await connection.OpenAsync();

        await connect.Should().ThrowAsync<PostgresException>(
            "una sola connessione aperta sul modello fa fallire con 55006 ogni CREATE DATABASE ... " +
            "TEMPLATE concorrente. Negare le connessioni trasforma la convenzione 'non " +
            "connettersi' in un invariante imposto dal server");
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name = @t);";
        command.Parameters.AddWithValue("t", table);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}

using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Prova che <c>provider_credentials</c> rilevi davvero un conflitto di scrittura concorrente.
///
/// Prima di #3651 la tabella dichiarava la concorrenza ottimistica con <c>byte[]? RowVersion</c>
/// su una colonna <c>bytea</c>. Postgres non popola una <c>bytea</c> da solo, e il trigger che lo
/// faceva è stato rimosso da #2305 nel passaggio a <c>xmin</c> delle altre entità: da allora il
/// token restava NULL su ogni riga, EF confrontava <c>NULL = NULL</c> a ogni update, e nessun
/// conflitto veniva mai rilevato. La protezione era dichiarata ma inesistente.
///
/// Lo scenario qui è quello che il dominio prevede esplicitamente: due rotazioni di chiave
/// concorrenti che disattivano la stessa credenziale precedente. L'indice parziale
/// <c>ux_provider_credentials_active_one</c> protegge l'INSERT della nuova riga attiva, ma non
/// l'UPDATE che disattiva la vecchia — quello è scoperto, ed è ciò che questo test copre.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class ProviderCredentialXminConcurrencyTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public ProviderCredentialXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"provcred_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "Due disattivazioni concorrenti: la seconda solleva DbUpdateConcurrencyException")]
    public async Task ConcurrentDeactivations_SecondWriterThrowsConcurrencyException()
    {
        // ── Arrange: una credenziale attiva, come dopo una prima rotazione ────────
        var credential = ProviderCredential.Create(
            ProviderName.Create("openrouter"),
            "encrypted-ciphertext",
            KeyFingerprint.FromPlaintext("sk-or-abcd1234"),
            Guid.NewGuid(),
            previousCredentialId: null,
            new FakeTimeProvider(FixedNow));
        var credentialId = credential.Id;

        await _dbContext.ProviderCredentials.AddAsync(credential);
        await _dbContext.SaveChangesAsync();

        // ── Due scope indipendenti leggono la stessa riga ─────────────────────────
        // Change tracker separati: ciascuno tiene il proprio snapshot del token.
        await using var dbA = _fixture.CreateDbContext(_connectionString);
        await using var dbB = _fixture.CreateDbContext(_connectionString);

        var credentialA = await dbA.ProviderCredentials.FirstAsync(c => c.Id == credentialId);
        var credentialB = await dbB.ProviderCredentials.FirstAsync(c => c.Id == credentialId);

        credentialA.Should().NotBeSameAs(credentialB);

        // ── Act: A vince la gara e committa per primo ─────────────────────────────
        credentialA.Deactivate(new FakeTimeProvider(FixedNow));
        await dbA.SaveChangesAsync();

        // B ha letto prima del commit di A: il suo original value di IsActive è ancora true,
        // quindi EF emette comunque l'UPDATE — ma con un token ormai stale, che in Postgres
        // non corrisponde ad alcuna riga. 0 righe affette ⇒ DbUpdateConcurrencyException.
        credentialB.Deactivate(new FakeTimeProvider(FixedNow));
        Func<Task> act = async () => await dbB.SaveChangesAsync();

        // ── Assert ────────────────────────────────────────────────────────────────
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "due rotazioni concorrenti non possono disattivare entrambe la stessa credenziale: " +
            "la seconda deve vedersi rifiutata invece di sovrascrivere in silenzio");
    }
}

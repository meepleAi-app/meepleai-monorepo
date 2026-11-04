using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Un database migrato e un container di servizi per l'intera classe di test (#4569): i test creano
/// partite con id propri e leggono solo quelle, quindi non si vedono a vicenda. Si paga una
/// migrazione invece di una per test.
/// <para>
/// Il container è quello dei test dei comandi di <c>PlayRecord</c>
/// (<see cref="PlayRecordTestServices"/>): i comandi passano da MediatR e dai repository veri, su
/// Postgres vero. Ogni <see cref="CreateScope"/> è una richiesta: un <c>DbContext</c> nuovo, senza
/// niente di tracciato dalle richieste precedenti.
/// </para>
/// </summary>
public sealed class PlayRecordMigratedDatabase(SharedTestcontainersFixture shared) : IAsyncLifetime
{
    private readonly string _databaseName = $"playrecord_children_{Guid.NewGuid():N}";
    private ServiceProvider? _services;

    public string ConnectionString { get; private set; } = null!;

    public TestTimeProvider Time { get; } = new();

    public MeepleAiDbContext CreateDbContext() => shared.CreateDbContext(ConnectionString);

    public IServiceScope CreateScope() =>
        (_services ?? throw new InvalidOperationException("Fixture not initialized.")).CreateScope();

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await shared.CreateIsolatedDatabaseAsync(_databaseName);

        await using (var context = CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var services = IntegrationServiceCollectionBuilder.CreateBase(ConnectionString);
        services.AddPlayRecordHandlerDependencies(Time);
        _services = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await shared.DropIsolatedDatabaseAsync(_databaseName);
    }

    /// <summary>L'utente che crea la partita: la FK di <c>play_records.CreatedByUserId</c>.</summary>
    public async Task<Guid> SeedUserAsync()
    {
        var id = Guid.NewGuid();
        await using var context = CreateDbContext();
        context.Users.Add(new UserEntity
        {
            Id = id,
            Email = $"partite-{id:N}@meepleai.test",
            DisplayName = "Giocatore di prova",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
        return id;
    }
}

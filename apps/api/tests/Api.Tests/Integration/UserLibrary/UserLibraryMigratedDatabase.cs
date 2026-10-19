using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Un database migrato per l'intera classe di test (#4499): i test seminano righe con id propri e
/// leggono solo quelle, quindi non si vedono a vicenda. Si paga una migrazione invece di una per test.
/// </summary>
public sealed class UserLibraryMigratedDatabase(SharedTestcontainersFixture shared) : IAsyncLifetime
{
    private readonly string _databaseName = $"userlib_migrated_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = null!;

    public MeepleAiDbContext CreateDbContext() => shared.CreateDbContext(ConnectionString);

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await shared.CreateIsolatedDatabaseAsync(_databaseName);
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await shared.DropIsolatedDatabaseAsync(_databaseName);

    /// <summary>
    /// Utente e gioco del catalogo, le due FK di una voce di libreria (pitfall #2620).
    /// </summary>
    public static async Task SeedUserAndGameAsync(MeepleAiDbContext context, Guid userId, Guid gameId)
    {
        context.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"libreria-{userId:N}@meepleai.test",
            Tier = "free",
            Role = "user",
        });
        context.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gioco in libreria" });
        await context.SaveChangesAsync();
    }
}

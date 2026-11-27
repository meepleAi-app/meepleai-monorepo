using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Migrations;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// #5160 — a user without a name was read with their email address (<c>UserRepository.MapToDomain</c>), and rows
/// saved before #4780 carried the address in <c>users."DisplayName"</c>. The column is now NOT NULL with a generated
/// default, and a migration gave a generated name to the rows that held nothing or an address.
/// </summary>
/// <remarks>
/// On Postgres: the default, the NOT NULL and the backfill are the database's. Every reader of the name reads this
/// column, directly or through <c>MapToDomain</c>, so the column's guarantee is what every reader gets.
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "5160")]
public sealed class UserDisplayNameColumnIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public UserDisplayNameColumnIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_displayname5160_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _serviceProvider = IntegrationServiceCollectionBuilder.CreateBase(connectionString).BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        await TestMigrationHelper.MigrateWithRetryAsync(scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();
        try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
        catch { /* ignore cleanup errors */ }
    }

    /// <summary>A row inserted without a name — a seed, a test, a path that skips the domain — gets a generated one.</summary>
    [Fact]
    public async Task AUserInsertedWithoutAName_GetsAGeneratedOne_FromTheDatabase()
    {
        var id = await InsertAsync(email: "no-name-5160@example.com", displayName: null);

        var stored = await ReadNameAsync(id);

        GeneratedDisplayName.IsGenerated(stored).Should().BeTrue(
            $"the column's default must make the same shape as GeneratedDisplayName.Create, not «{stored}»");
    }

    /// <summary>
    /// The backfill of the migration, the very SQL it runs. Rows with nothing, blanks, the account's own address
    /// (other case, spaces) or an older address get a generated name; a name someone chose is left alone.
    /// </summary>
    [Fact]
    public async Task TheBackfill_GivesAGeneratedNameToTheRowsThatShowAnAddressOrNothing_AndLeavesRealNames()
    {
        var blank = await InsertAsync("blank-5160@example.com", "   ");
        var ownAddress = await InsertAsync("marta-5160@example.com", " Marta-5160@Example.com ");
        var olderAddress = await InsertAsync("luca-5160@example.com", "luca.old@provider.it");
        var realName = await InsertAsync("anna-5160@example.com", "Anna Bianchi");
        var generated = await InsertAsync("gen-5160@example.com", "Player-ab12cd3");

        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database
                .ExecuteSqlRawAsync(UsersDisplayNameNotNullWithGeneratedDefault.BackfillSql, Ct);
        }

        foreach (var id in new[] { blank, ownAddress, olderAddress })
        {
            var stored = await ReadNameAsync(id);
            GeneratedDisplayName.IsGenerated(stored).Should().BeTrue($"«{stored}» is not a name anyone chose");
        }

        (await ReadNameAsync(realName)).Should().Be("Anna Bianchi");
        (await ReadNameAsync(generated)).Should().Be("Player-ab12cd3", "a generated name is already a name");
    }

    /// <summary>
    /// The NOT NULL is the migration's, not the EF model's (the InMemory provider would refuse every test entity without
    /// a name): nothing else would notice it gone.
    /// </summary>
    [Fact]
    public async Task TheDatabase_RefusesANullName()
    {
        var id = await InsertAsync("null-later-5160@example.com", "Anna");

        await using var scope = _serviceProvider!.CreateAsyncScope();
        var clear = () => scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Users
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.DisplayName, (string?)null), Ct);

        (await clear.Should().ThrowAsync<Npgsql.PostgresException>())
            .Which.SqlState.Should().Be(Npgsql.PostgresErrorCodes.NotNullViolation);
    }

    /// <summary>Through the repository, as every reader of the domain user does: never the address.</summary>
    [Fact]
    public async Task TheRepository_ReadsAUserInsertedWithoutAName_WithTheGeneratedName_NeverTheAddress()
    {
        const string email = "repo-5160@example.com";
        var id = await InsertAsync(email, displayName: null);

        await using var scope = _serviceProvider!.CreateAsyncScope();
        var repository = new UserRepository(
            scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventCollector>());
        var user = await repository.GetByIdAsync(id, Ct);

        user.Should().NotBeNull();
        user!.DisplayName.Should().NotBe(email);
        GeneratedDisplayName.IsGenerated(user.DisplayName).Should().BeTrue();
    }

    /// <summary>
    /// A name that is still blank — written as "" on purpose, the NOT NULL lets it through — is read blank, not as the
    /// address: the readers who show it to others put their neutral name in its place (#4780).
    /// </summary>
    [Fact]
    public async Task TheRepository_ReadsABlankName_Blank_NeverTheAddress()
    {
        const string email = "blank-read-5160@example.com";
        var id = await InsertAsync(email, displayName: string.Empty);

        await using var scope = _serviceProvider!.CreateAsyncScope();
        var repository = new UserRepository(
            scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventCollector>());
        var user = await repository.GetByIdAsync(id, Ct);

        user!.DisplayName.Should().BeEmpty("a blank name is not replaced by the address, nor by a name made at each read");
    }

    private async Task<Guid> InsertAsync(string email, string? displayName)
    {
        var id = Guid.NewGuid();
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Users.Add(new UserEntity
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<string?> ReadNameAsync(Guid id)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => u.DisplayName)
            .SingleAsync(Ct);
    }
}

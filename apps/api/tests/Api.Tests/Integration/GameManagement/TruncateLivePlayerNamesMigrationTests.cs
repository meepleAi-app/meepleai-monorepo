using Api.BoundedContexts.GameManagement.Infrastructure.Persistence.Mappers;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// #5150 — the name of a live player goes down from 100 to 50 characters, and the mapper reads a live back
/// through the constructor that checks the length: a seat saved before with 51 to 100 characters would make the
/// whole live unreadable. The migration cuts it to the limit, on Postgres, and the live reads again.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "5150")]
public sealed class TruncateLivePlayerNamesMigrationTests(SharedTestcontainersFixture fixture)
{
    private const string Previous = "20261124120828_MakeLiveGameSessionTrackingSessionIdUnique";
    private const string Truncation = "20261125065917_TruncateLivePlayerNamesToTheNameLimit";

    [Fact]
    public async Task Migration_CutsASeatNamedOverTheLimit_AndTheLiveReadsAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"truncate_names_{Guid.NewGuid():N}";
        var connectionString = await fixture.CreateIsolatedDatabaseAsync(databaseName);
        try
        {
            await using var context = fixture.CreateDbContext(connectionString);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(Previous, ct);

            // 49 letters, a space, then 30 more: the cut ends on the space, which goes too.
            var longName = new string('a', 49) + " " + new string('b', 30);
            var liveId = await SeedLiveAsync(context, longName, "Marta", ct);

            var before = await context.LiveGameSessions.AsNoTracking().Include(s => s.Players).SingleAsync(s => s.Id == liveId, ct);
            ((Action)(() => LiveGameSessionMapper.ToDomain(before))).Should().Throw<ValidationException>(
                "without the migration the seat is over the new limit, and the live cannot be read");

            await migrator.MigrateAsync(Truncation, ct);

            var after = await context.LiveGameSessions.AsNoTracking().Include(s => s.Players).SingleAsync(s => s.Id == liveId, ct);
            LiveGameSessionMapper.ToDomain(after).Players.Select(p => p.DisplayName)
                .Should().BeEquivalentTo([new string('a', 49), "Marta"], "the long name is cut, the short one is left alone");
        }
        finally
        {
            await fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    private static async Task<Guid> SeedLiveAsync(
        Api.Infrastructure.MeepleAiDbContext context, string firstName, string secondName, CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        context.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"host-{userId:N}@test.local",
            DisplayName = "Host",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        });

        var liveId = Guid.NewGuid();
        context.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = liveId,
            SessionCode = "NAME50",
            GameId = null, // free-form: no catalog game to seed
            GameName = "Long names",
            CreatedByUserId = userId,
            Visibility = 0,
            Status = 0,
            ScoringConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Players =
            {
                Seat(liveId, firstName, "Red", "Host"),
                Seat(liveId, secondName, "Blue", "Player"),
            },
        });

        await context.SaveChangesAsync(ct);
        context.ChangeTracker.Clear();
        return liveId;
    }

    private static SessionPlayerEntity Seat(Guid liveId, string name, string color, string role) => new()
    {
        Id = Guid.NewGuid(),
        LiveGameSessionId = liveId,
        DisplayName = name,
        Color = color,
        Role = role,
        JoinedAt = DateTime.UtcNow,
        IsActive = true,
    };
}

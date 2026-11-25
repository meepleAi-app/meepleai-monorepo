using Api.BoundedContexts.GameManagement.Infrastructure.Persistence.Mappers;
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
/// <remarks>
/// The rows are written with SQL against the schema of the migration before, not with the EF model of today: a
/// later migration that adds a required column would otherwise break this test for a change it is not about.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "5150")]
public sealed class TruncateLivePlayerNamesMigrationTests(SharedTestcontainersFixture fixture)
{
    private const string Previous = "20261124120828_MakeLiveGameSessionTrackingSessionIdUnique";
    private const string Truncation = "20261125065917_TruncateLivePlayerNamesToTheNameLimit";

    private const string Dice = "🎲"; // two UTF-16 units, one Postgres character
    private const string EmptyScoringConfig = "{}";

    [Fact]
    public async Task Migration_CutsTheSeatsOverTheLimit_InTheDomainsUnits_AndTheLiveReadsAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"truncate_names_{Guid.NewGuid():N}";
        var connectionString = await fixture.CreateIsolatedDatabaseAsync(databaseName);
        try
        {
            await using var context = fixture.CreateDbContext(connectionString);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(Previous, ct);

            var seats = new[]
            {
                // 49 letters, a space, 30 more: the cut ends on the space, which goes too.
                new string('a', 49) + " " + new string('b', 30),
                // 46 letters and three dice: 49 Postgres characters but 52 units, over the limit for the domain.
                new string('c', 46) + Dice + Dice + Dice,
                // 49 letters and a die: the die would cross the limit, and is not split.
                new string('d', 49) + Dice,
                "Marta",
            };
            var liveId = await SeedLiveAsync(context, seats, ct);

            ((Action)(() => LiveGameSessionMapper.ToDomain(Load(context, liveId)))).Should().Throw<ValidationException>(
                "without the migration the seats are over the new limit, and the live cannot be read");

            await migrator.MigrateAsync(Truncation, ct);

            LiveGameSessionMapper.ToDomain(Load(context, liveId)).Players.Select(p => p.DisplayName).Should().BeEquivalentTo(
            [
                new string('a', 49),
                new string('c', 46) + Dice + Dice,
                new string('d', 49),
                "Marta",
            ], "each long name is cut to 50 UTF-16 units without splitting a character, the short one is left alone");
        }
        finally
        {
            await fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    private static Api.Infrastructure.Entities.GameManagement.LiveGameSessionEntity Load(
        Api.Infrastructure.MeepleAiDbContext context, Guid liveId)
    {
        context.ChangeTracker.Clear();
        return context.LiveGameSessions.AsNoTracking().Include(s => s.Players).Single(s => s.Id == liveId);
    }

    private static async Task<Guid> SeedLiveAsync(
        Api.Infrastructure.MeepleAiDbContext context, IReadOnlyList<string> names, CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        var liveId = Guid.NewGuid();
        var colors = new[] { "Red", "Blue", "Green", "Yellow" };

        // users keeps the quoted PascalCase column names of the entity; the live tables are snake_case.
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO users ("Id", "Email", "Role", "Tier", "Status", "CreatedAt", "IsContributor", "IsDemoAccount",
                               "IsSuspended", "OnboardingCompleted", "OnboardingSkipped")
            VALUES ({userId}, {$"host-{userId:N}@test.local"}, 'user', 'free', 'Active', now(), false, false,
                    false, true, false);
            """, ct);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO live_game_sessions (id, session_code, game_name, created_by_user_id, visibility, status,
                                            scoring_config_json, created_at, updated_at, "TotalPausedDurationMs")
            VALUES ({liveId}, 'NAME50', 'Long names', {userId}, 0, 0, {EmptyScoringConfig}::jsonb, now(), now(), 0);
            """, ct);

        for (var i = 0; i < names.Count; i++)
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO session_players (id, live_game_session_id, display_name, color, role, joined_at, is_active)
                VALUES ({Guid.NewGuid()}, {liveId}, {names[i]}, {colors[i]}, {(i == 0 ? "Host" : "Player")}, now(), true);
                """, ct);
        }

        return liveId;
    }
}

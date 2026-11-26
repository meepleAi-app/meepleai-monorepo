using Api.Infrastructure.EntityConfigurations.GameManagement;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// The two unique indexes of <c>game_night_sessions</c> that two writers of a night's links can meet, after both
/// passed the guard in memory (#4780, #5167): the night's single live slot, and the per-night play order. One place
/// for the rule, used by every handler that adds or starts a link — the start, the go-live and the attach of a
/// gamebook campaign — so that the same race answers the same 409 whichever of them loses it.
/// </summary>
/// <remarks>
/// The names come from the index declarations (<see cref="GameNightSessionEntityConfiguration"/>), so a match can
/// never drift from them.
/// </remarks>
internal static class GameNightLinkIndexViolations
{
    /// <summary>The partial unique index on the night's live slot: one <c>InProgress</c> link per night.</summary>
    public static bool IsLiveSlot(DbUpdateException ex) =>
        IsUniqueViolationOf(ex, GameNightSessionEntityConfiguration.UniqueActiveIndexName);

    /// <summary>The unique index on <c>(event_id, play_order)</c>: two links added with the same play order.</summary>
    public static bool IsPlayOrder(DbUpdateException ex) =>
        IsUniqueViolationOf(ex, GameNightSessionEntityConfiguration.PlayOrderIndexName);

    private static bool IsUniqueViolationOf(DbUpdateException ex, string indexName) =>
        ex.InnerException is PostgresException pg
        && string.Equals(pg.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
        && string.Equals(pg.ConstraintName, indexName, StringComparison.Ordinal);
}

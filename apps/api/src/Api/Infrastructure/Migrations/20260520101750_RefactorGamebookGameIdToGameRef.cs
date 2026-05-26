using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Task A0.2 (#1320 Phase 2c follow-up): refactors
    /// <c>session_tracking.gamebook_campaign_sessions.game_id</c> from a bare
    /// <c>uuid</c> column into a discriminated <c>GameRef</c> value object
    /// represented as two scalar columns:
    /// <list type="bullet">
    ///   <item><c>game_ref_id</c> (uuid, renamed from <c>game_id</c> — data preserved)</item>
    ///   <item><c>game_ref_kind</c> (smallint, new, default 0 = Shared)</item>
    /// </list>
    ///
    /// <para><b>Backfill assumption:</b> all existing rows in
    /// <c>gamebook_campaign_sessions</c> are assumed to point at
    /// <c>SharedGameEntity</c> (Kind = 0 = Shared) because the gamebook feature
    /// has not yet shipped private-game support. The <c>defaultValue: 0</c> on
    /// the new <c>game_ref_kind</c> column applies to all pre-existing rows,
    /// which is correct per this assumption. If, in any environment, a row's
    /// pre-migration <c>game_id</c> referenced a <c>games</c> (private) table
    /// row instead of a <c>shared_games</c> row, the resulting
    /// <c>(kind=Shared, id=&lt;private-uuid&gt;)</c> tuple would point at a
    /// non-existent shared game — that data corruption must be resolved
    /// manually post-deploy if it occurs (no DB FK enforces this).</para>
    ///
    /// <para><b>Index swap:</b> drops the old
    /// <c>ix_gamebook_campaign_sessions_owner_game</c> (on <c>game_id</c>) and
    /// creates a new
    /// <c>ix_gamebook_campaign_sessions_owner_game_ref</c> over
    /// <c>(owner_user_id, game_ref_kind, game_ref_id, is_deleted)</c>. The
    /// new index is created via raw SQL because EF Core 9
    /// <c>HasIndex(string[])</c> cannot bind owned-type properties from the
    /// parent entity builder.</para>
    /// </summary>
    public partial class RefactorGamebookGameIdToGameRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop old index keyed on bare game_id.
            migrationBuilder.DropIndex(
                name: "ix_gamebook_campaign_sessions_owner_game",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions");

            // 2. Rename game_id → game_ref_id. RenameColumn is data-preserving
            //    in PostgreSQL (single ALTER TABLE ... RENAME COLUMN statement);
            //    this is the EF idiomatic equivalent of the plan's prescribed
            //    "ADD COLUMN game_ref_id + UPDATE game_ref_id = game_id +
            //    DROP COLUMN game_id" sequence.
            migrationBuilder.RenameColumn(
                name: "game_id",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions",
                newName: "game_ref_id");

            // 3. Add the discriminator column with default 0 (= GameRefKind.Shared).
            //    All pre-existing rows are backfilled to Shared per the assumption
            //    documented above.
            migrationBuilder.AddColumn<short>(
                name: "game_ref_kind",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            // 4. Create the new composite index. Done via raw SQL because EF Core 9
            //    HasIndex(params string[]) cannot address owned-type scalar
            //    properties from the parent EntityTypeBuilder.
            migrationBuilder.Sql(@"
                CREATE INDEX ""ix_gamebook_campaign_sessions_owner_game_ref""
                ON session_tracking.gamebook_campaign_sessions
                (owner_user_id, game_ref_kind, game_ref_id, is_deleted);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse step 4: drop the new composite index first.
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS session_tracking.""ix_gamebook_campaign_sessions_owner_game_ref"";
            ");

            // Reverse step 3: drop the discriminator column.
            migrationBuilder.DropColumn(
                name: "game_ref_kind",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions");

            // Reverse step 2: rename game_ref_id back to game_id.
            migrationBuilder.RenameColumn(
                name: "game_ref_id",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions",
                newName: "game_id");

            // Reverse step 1: recreate the old index.
            migrationBuilder.CreateIndex(
                name: "ix_gamebook_campaign_sessions_owner_game",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions",
                columns: new[] { "owner_user_id", "game_id", "is_deleted" });
        }
    }
}

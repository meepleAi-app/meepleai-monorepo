using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #3153 — replace the case-SENSITIVE unique index on
    /// game_designers.name / game_publishers.name with a case-INSENSITIVE
    /// functional unique index on lower(name), so the seed→game promotion's
    /// get-or-create-by-name resolver deterministically reuses an existing
    /// designer/publisher regardless of casing (matches RelationshipSeeder
    /// semantics) and the DB enforces what the app-layer dedup assumes.
    /// </summary>
    /// <remarks>
    /// The functional index cannot be expressed via EF <c>HasIndex</c>, so it is
    /// hand-written in raw SQL (mirroring 20260713065129_RestoreSearchVectorColumns)
    /// and is NOT model-tracked (distinct name <c>*_name_lower</c>). The old
    /// case-sensitive indexes are dropped (EF-generated; the fluent HasIndex was
    /// removed from the entity configs).
    /// <para><b>Irreversible:</b> the Up de-dup MERGES existing case-variant
    /// designer/publisher rows (keeper = earliest created_at, then lowest id) and
    /// repoints their join rows. Down restores the old indexes but CANNOT un-merge
    /// the deleted duplicate rows.</para>
    /// </remarks>
    public partial class AddLowerNameUniqueIndexToDesignersAndPublishers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- 1. De-dup game_designers case-insensitively BEFORE the functional
            //         unique index is created (keeper = earliest created_at, then lowest id).
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE _designer_dups ON COMMIT DROP AS
                SELECT id AS dup_id, keeper_id FROM (
                    SELECT id,
                           first_value(id) OVER (PARTITION BY lower(name) ORDER BY created_at, id) AS keeper_id
                    FROM game_designers
                ) r
                WHERE id <> keeper_id;

                -- drop join rows that would collide with the keeper's existing (game_designer_id, shared_game_id)
                DELETE FROM shared_game_designers sgd
                USING _designer_dups d
                WHERE sgd.game_designer_id = d.dup_id
                  AND EXISTS (SELECT 1 FROM shared_game_designers k
                              WHERE k.game_designer_id = d.keeper_id
                                AND k.shared_game_id  = sgd.shared_game_id);

                -- repoint remaining join rows to the keeper
                UPDATE shared_game_designers sgd
                SET game_designer_id = d.keeper_id
                FROM _designer_dups d
                WHERE sgd.game_designer_id = d.dup_id;

                -- delete the now-orphaned duplicate designer rows
                DELETE FROM game_designers gd USING _designer_dups d WHERE gd.id = d.dup_id;
            ");

            // ---- 2. De-dup game_publishers (identical shape).
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE _publisher_dups ON COMMIT DROP AS
                SELECT id AS dup_id, keeper_id FROM (
                    SELECT id,
                           first_value(id) OVER (PARTITION BY lower(name) ORDER BY created_at, id) AS keeper_id
                    FROM game_publishers
                ) r
                WHERE id <> keeper_id;

                DELETE FROM shared_game_publishers sgp
                USING _publisher_dups d
                WHERE sgp.game_publisher_id = d.dup_id
                  AND EXISTS (SELECT 1 FROM shared_game_publishers k
                              WHERE k.game_publisher_id = d.keeper_id
                                AND k.shared_game_id    = sgp.shared_game_id);

                UPDATE shared_game_publishers sgp
                SET game_publisher_id = d.keeper_id
                FROM _publisher_dups d
                WHERE sgp.game_publisher_id = d.dup_id;

                DELETE FROM game_publishers gp USING _publisher_dups d WHERE gp.id = d.dup_id;
            ");

            // ---- 3. Drop the old case-sensitive unique indexes (EF-generated).
            migrationBuilder.DropIndex(
                name: "ix_game_publishers_name",
                table: "game_publishers");

            migrationBuilder.DropIndex(
                name: "ix_game_designers_name",
                table: "game_designers");

            // ---- 4. Create the case-insensitive functional unique indexes.
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ix_game_designers_name_lower  ON game_designers  (lower(name));");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ix_game_publishers_name_lower ON game_publishers (lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the functional indexes; note the Up de-dup is NOT reversible.
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_game_publishers_name_lower;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_game_designers_name_lower;");

            migrationBuilder.CreateIndex(
                name: "ix_game_publishers_name",
                table: "game_publishers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_designers_name",
                table: "game_designers",
                column: "name",
                unique: true);
        }
    }
}

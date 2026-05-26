using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Task A6 (Gamebook multi-book generalization, spec 2026-05-19): creates the
    /// <c>game_books</c> table that backs the <c>GameBook</c> aggregate (Phase A).
    /// Generalises the libro-game companion to support N books per game (community
    /// + personal) with role flags, paragraph schemes, and optional KB indexing,
    /// replacing the hardcoded "Press Start + Rules" tuple.
    ///
    /// <para><b>Composite index over owned-type columns:</b> the EF config at
    /// <c>GameBookEntityConfiguration.cs:46-51</c> defers the
    /// <c>ix_game_books_game_ref</c> index over
    /// <c>(game_ref_kind, game_ref_id, deleted_at)</c> to this migration because
    /// EF Core 9 <c>HasIndex(params string[])</c> cannot address owned-type
    /// scalar properties (here <c>GameRef.Kind</c>, <c>GameRef.Id</c>) from the
    /// parent entity builder. Mirrors the A0.2 pattern already proven in
    /// <c>20260520101750_RefactorGamebookGameIdToGameRef.cs:71-78</c>
    /// (<c>ix_gamebook_campaign_sessions_owner_game_ref</c>).</para>
    /// </summary>
    public partial class AddGameBooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_books",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_ref_kind = table.Column<short>(type: "smallint", nullable: false),
                    game_ref_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    roles = table.Column<int>(type: "integer", nullable: false),
                    paragraph_scheme = table.Column<short>(type: "smallint", nullable: false),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    sequential_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    kb_source_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    physical_only = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_books", x => x.id);
                    table.CheckConstraint("chk_game_books_physical_kb_coherence", "(physical_only = true AND kb_source_doc_id IS NULL) OR (physical_only = false)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_books_owner_user_id",
                table: "game_books",
                columns: new[] { "owner_user_id", "deleted_at" },
                filter: "owner_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_game_books_kb_source_community",
                table: "game_books",
                column: "kb_source_doc_id",
                unique: true,
                filter: "kb_source_doc_id IS NOT NULL AND owner_user_id IS NULL AND deleted_at IS NULL");

            // Composite index over (game_ref_kind, game_ref_id, deleted_at) — declared via raw
            // SQL because EF Core 9 HasIndex(params string[]) cannot bind owned-type scalar
            // properties (GameRef.Kind, GameRef.Id) from the parent EntityTypeBuilder. See the
            // deferral note in GameBookEntityConfiguration.cs:46-51 and the matching A0.2 pattern
            // in 20260520101750_RefactorGamebookGameIdToGameRef.cs:71-78.
            migrationBuilder.Sql(@"
                CREATE INDEX ""ix_game_books_game_ref""
                ON game_books (game_ref_kind, game_ref_id, deleted_at);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the manually-added composite index first; EF will drop the table (and its
            // EF-declared indexes + check constraint) on its own via the DropTable call below.
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""ix_game_books_game_ref"";
            ");

            migrationBuilder.DropTable(
                name: "game_books");
        }
    }
}

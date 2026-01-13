using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable MA0051 // Method is too long - auto-generated EF Core migration
namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedGameCatalogPhase1a : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_designers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_designers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_mechanics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_mechanics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_publishers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_publishers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shared_games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bgg_id = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    year_published = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false),
                    max_players = table.Column<int>(type: "integer", nullable: false),
                    playing_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    min_age = table.Column<int>(type: "integer", nullable: false),
                    complexity_rating = table.Column<decimal>(type: "numeric(3,2)", nullable: true),
                    average_rating = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    thumbnail_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rules_content = table.Column<string>(type: "text", nullable: true),
                    rules_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_games", x => x.id);
                    table.CheckConstraint("chk_shared_games_complexity", "complexity_rating IS NULL OR (complexity_rating >= 1.0 AND complexity_rating <= 5.0)");
                    table.CheckConstraint("chk_shared_games_min_age", "min_age >= 0");
                    table.CheckConstraint("chk_shared_games_players", "min_players > 0 AND max_players >= min_players");
                    table.CheckConstraint("chk_shared_games_playing_time", "playing_time_minutes > 0");
                    table.CheckConstraint("chk_shared_games_rating", "average_rating IS NULL OR (average_rating >= 1.0 AND average_rating <= 10.0)");
                    table.CheckConstraint("chk_shared_games_year_published", "year_published > 1900 AND year_published <= 2100");
                });

            migrationBuilder.CreateTable(
                name: "shared_game_categories",
                columns: table => new
                {
                    game_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_categories", x => new { x.game_category_id, x.shared_game_id });
                    table.ForeignKey(
                        name: "FK_shared_game_categories_game_categories_game_category_id",
                        column: x => x.game_category_id,
                        principalTable: "game_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_game_categories_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_game_designers",
                columns: table => new
                {
                    game_designer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_designers", x => new { x.game_designer_id, x.shared_game_id });
                    table.ForeignKey(
                        name: "FK_shared_game_designers_game_designers_game_designer_id",
                        column: x => x.game_designer_id,
                        principalTable: "game_designers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_game_designers_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_game_mechanics",
                columns: table => new
                {
                    game_mechanic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_mechanics", x => new { x.game_mechanic_id, x.shared_game_id });
                    table.ForeignKey(
                        name: "FK_shared_game_mechanics_game_mechanics_game_mechanic_id",
                        column: x => x.game_mechanic_id,
                        principalTable: "game_mechanics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_game_mechanics_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_game_publishers",
                columns: table => new
                {
                    game_publisher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_publishers", x => new { x.game_publisher_id, x.shared_game_id });
                    table.ForeignKey(
                        name: "FK_shared_game_publishers_game_publishers_game_publisher_id",
                        column: x => x.game_publisher_id,
                        principalTable: "game_publishers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_game_publishers_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_categories_name",
                table: "game_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_categories_slug",
                table: "game_categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_designers_name",
                table: "game_designers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_mechanics_name",
                table: "game_mechanics",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_mechanics_slug",
                table: "game_mechanics",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_publishers_name",
                table: "game_publishers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_game_categories_shared_game_id",
                table: "shared_game_categories",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_shared_game_designers_shared_game_id",
                table: "shared_game_designers",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_shared_game_mechanics_shared_game_id",
                table: "shared_game_mechanics",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_shared_game_publishers_shared_game_id",
                table: "shared_game_publishers",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_shared_games_bgg_id",
                table: "shared_games",
                column: "bgg_id",
                unique: true,
                filter: "bgg_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_shared_games_status",
                table: "shared_games",
                column: "status",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_shared_games_title",
                table: "shared_games",
                column: "title",
                filter: "is_deleted = false");

            // Add SearchVector column (tsvector type for PostgreSQL full-text search)
            migrationBuilder.Sql(@"
                ALTER TABLE shared_games
                ADD COLUMN search_vector tsvector;
            ");

            // Create GIN index on search_vector for fast full-text search
            migrationBuilder.Sql(@"
                CREATE INDEX ix_shared_games_search_vector
                ON shared_games
                USING gin(search_vector);
            ");

            // Create trigger function to auto-update search_vector on insert/update
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION update_shared_games_search_vector()
                RETURNS TRIGGER AS $$
                BEGIN
                    NEW.search_vector := to_tsvector('italian', COALESCE(NEW.title, '') || ' ' || COALESCE(NEW.description, ''));
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create trigger on shared_games table
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_update_search_vector
                BEFORE INSERT OR UPDATE ON shared_games
                FOR EACH ROW
                EXECUTE FUNCTION update_shared_games_search_vector();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop trigger and function for SearchVector
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_update_search_vector ON shared_games;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_shared_games_search_vector();");

            migrationBuilder.DropTable(
                name: "shared_game_categories");

            migrationBuilder.DropTable(
                name: "shared_game_designers");

            migrationBuilder.DropTable(
                name: "shared_game_mechanics");

            migrationBuilder.DropTable(
                name: "shared_game_publishers");

            migrationBuilder.DropTable(
                name: "game_categories");

            migrationBuilder.DropTable(
                name: "game_designers");

            migrationBuilder.DropTable(
                name: "game_mechanics");

            migrationBuilder.DropTable(
                name: "game_publishers");

            migrationBuilder.DropTable(
                name: "shared_games");
        }
    }
}

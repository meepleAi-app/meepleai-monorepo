using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCacheStatsTablePerf03 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cache_stats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    question_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    hit_count = table.Column<long>(type: "bigint", nullable: false),
                    miss_count = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_hit_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cache_stats", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cache_stats_game_id_question_hash",
                table: "cache_stats",
                columns: new[] { "game_id", "question_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cache_stats_hit_count",
                table: "cache_stats",
                column: "hit_count");

            migrationBuilder.CreateIndex(
                name: "IX_cache_stats_last_hit_at",
                table: "cache_stats",
                column: "last_hit_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cache_stats");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionScoringType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "score_data",
                table: "session_tracking_sessions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "scoring_type",
                table: "session_tracking_sessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Points");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "score_data",
                table: "session_tracking_sessions");

            migrationBuilder.DropColumn(
                name: "scoring_type",
                table: "session_tracking_sessions");
        }
    }
}

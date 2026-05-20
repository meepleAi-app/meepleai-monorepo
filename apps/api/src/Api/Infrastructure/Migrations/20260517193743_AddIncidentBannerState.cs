using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentBannerState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentBannerState",
                schema: "SystemConfiguration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    Severity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentBannerState", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "SystemConfiguration",
                table: "IncidentBannerState",
                columns: new[] { "Id", "CreatedAt", "EndsAt", "Message", "StartsAt", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentBannerState",
                schema: "SystemConfiguration");
        }
    }
}

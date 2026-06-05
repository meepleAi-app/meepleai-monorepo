using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_name = table.Column<string>(type: "text", nullable: false),
                    encrypted_api_key = table.Column<string>(type: "text", nullable: false),
                    key_fingerprint = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    rotated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rotated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_credential_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_credentials", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_credentials_rotated_at",
                table: "provider_credentials",
                columns: new[] { "provider_name", "rotated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_provider_credentials_active_one",
                table: "provider_credentials",
                column: "provider_name",
                unique: true,
                filter: "is_active = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_credentials");
        }
    }
}

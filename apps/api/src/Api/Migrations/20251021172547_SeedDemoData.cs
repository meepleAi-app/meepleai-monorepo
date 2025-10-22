using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedDemoData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert demo users (DB-02)
            // Password for all users: Demo123!
            // NOTE: Only inserting users for now. Games, rule specs, and agents can be added later.
            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "Email", "DisplayName", "PasswordHash", "Role", "CreatedAt" },
                values: new object[,]
                {
                    {
                        "demo-admin-001",
                        "admin@meepleai.dev",
                        "Demo Admin",
                        "v1.210000.gB/MpsLy2G4gIK2PiDY1+w==.RPkVU/xY3D/ne+lsmyQEAw6IDrUjJK5RUMl3oRqBoQs=",
                        "Admin", // UserRole.Admin (enum stored as string)
                        new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        "demo-editor-001",
                        "editor@meepleai.dev",
                        "Demo Editor",
                        "v1.210000.DY+OddskSSaUg0msekNH2A==.x+wPl5YrO4UeCMoURYo1PN6wi5ZKl4rpcTtcHeMrrKc=",
                        "Editor", // UserRole.Editor (enum stored as string)
                        new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        "demo-user-001",
                        "user@meepleai.dev",
                        "Demo User",
                        "v1.210000.T5lbyZaK4KncVSs+FzZLow==.Th0oRlycEbzCX2WrA/yaxOKyDHc3wEg9AxTKZTm/UAQ=",
                        "User", // UserRole.User (enum stored as string)
                        new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove demo users
            migrationBuilder.DeleteData(table: "users", keyColumn: "Id", keyValues: new object[] { "demo-admin-001", "demo-editor-001", "demo-user-001" });
        }
    }
}

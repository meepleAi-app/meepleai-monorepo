using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores - EF Core migration naming convention
    public partial class Issue4416_PushSubscriptionFields : Migration
#pragma warning restore CA1707
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PushAuthKey",
                table: "notification_preferences",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PushEndpoint",
                table: "notification_preferences",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PushP256dhKey",
                table: "notification_preferences",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PushAuthKey",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "PushEndpoint",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "PushP256dhKey",
                table: "notification_preferences");
        }
    }
}

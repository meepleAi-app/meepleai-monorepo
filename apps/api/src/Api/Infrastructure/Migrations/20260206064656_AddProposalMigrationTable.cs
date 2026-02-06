using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalMigrationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProposalMigrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrivateGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Choice = table.Column<int>(type: "integer", nullable: false, comment: "0 = Pending, 1 = LinkToCatalog, 2 = KeepPrivate"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ChoiceAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalMigrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalMigrations_private_games_PrivateGameId",
                        column: x => x.PrivateGameId,
                        principalTable: "private_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalMigrations_shared_games_SharedGameId",
                        column: x => x.SharedGameId,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalMigrations_PrivateGameId",
                table: "ProposalMigrations",
                column: "PrivateGameId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalMigrations_SharedGameId",
                table: "ProposalMigrations",
                column: "SharedGameId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalMigrations_ShareRequestId",
                table: "ProposalMigrations",
                column: "ShareRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalMigrations_UserId_Choice",
                table: "ProposalMigrations",
                columns: new[] { "UserId", "Choice" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalMigrations");
        }
    }
}

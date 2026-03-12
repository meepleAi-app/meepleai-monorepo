using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationTokensAndUserInterests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Interests",
                table: "users",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "invitation_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_invitation_tokens_users_accepted_by_user_id",
                        column: x => x.accepted_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_invitation_tokens_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_accepted_by_user_id",
                table: "invitation_tokens",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_email_status",
                table: "invitation_tokens",
                columns: new[] { "email", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_expires_at",
                table: "invitation_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_invited_by_user_id",
                table: "invitation_tokens",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_token_hash",
                table: "invitation_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitation_tokens");

            migrationBuilder.DropColumn(
                name: "Interests",
                table: "users");
        }
    }
}

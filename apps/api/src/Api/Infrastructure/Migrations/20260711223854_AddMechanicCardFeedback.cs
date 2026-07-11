using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMechanicCardFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mechanic_card_feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_positive = table.Column<bool>(type: "boolean", nullable: false),
                    error_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    suggested_citation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mechanic_card_feedback", x => x.id);
                    table.CheckConstraint("ck_mechanic_card_feedback_error_type", "error_type IS NULL OR error_type IN ('factual', 'ambiguous', 'contradicts_rule')");
                    table.ForeignKey(
                        name: "FK_mechanic_card_feedback_mechanic_cards_card_id",
                        column: x => x.card_id,
                        principalTable: "mechanic_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_card_feedback_user_created",
                table: "mechanic_card_feedback",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_mechanic_card_feedback_card_user_claim",
                table: "mechanic_card_feedback",
                columns: new[] { "card_id", "user_id", "claim_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mechanic_card_feedback");
        }
    }
}

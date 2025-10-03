using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class PDF03_AddStructuredExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "rule_specs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AtomicRuleCount",
                table: "pdf_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtomicRules",
                table: "pdf_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiagramCount",
                table: "pdf_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedDiagrams",
                table: "pdf_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedTables",
                table: "pdf_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TableCount",
                table: "pdf_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_CreatedByUserId",
                table: "rule_specs",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_rule_specs_users_CreatedByUserId",
                table: "rule_specs",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rule_specs_users_CreatedByUserId",
                table: "rule_specs");

            migrationBuilder.DropIndex(
                name: "IX_rule_specs_CreatedByUserId",
                table: "rule_specs");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "rule_specs");

            migrationBuilder.DropColumn(
                name: "AtomicRuleCount",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "AtomicRules",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "DiagramCount",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "ExtractedDiagrams",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "ExtractedTables",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "TableCount",
                table: "pdf_documents");
        }
    }
}

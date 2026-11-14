using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #4440, contract phase (the expand was #4922): drops from <c>pdf_documents</c> the six columns of the retired iText
    /// extractor. No pipeline has written them since #4922. Tables, diagrams and the three counters never had a reader;
    /// <c>AtomicRules</c> lost its only one, <c>GenerateRuleSpecFromPdfCommandHandler</c>, with #4942 (#4930), which is on
    /// <c>main</c> since release #4977. Without a deployed environment (#4428) the entity properties leave in this same
    /// change, as release-promotion.md §5 allows. The historical values are discarded: the tables of <c>hi_res</c> (#4439)
    /// go to <c>pdf_table_extractions</c>.
    /// </summary>
    public partial class DropPdfDocumentsLegacyStructureColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("-- safe: drop the six legacy structure columns of pdf_documents, contract of #4440; writes: no pipeline writes any of them since #4922 (expand); ExtractedTables, ExtractedDiagrams, TableCount, DiagramCount, AtomicRuleCount: no code ever read them; AtomicRules: its only reader, GenerateRuleSpecFromPdfCommandHandler, ignores it since #4942 (#4930), on main since release #4977; the entity properties leave in this same change, and no environment runs an older release against this schema: deploy-staging.yml is disabled and deploy-production.yml exists only as .disabled (#4428) (release-promotion.md §5)");
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}

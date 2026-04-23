using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M1_1_MechanicAnalysisSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mechanic_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    total_tokens_used = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    estimated_cost_usd = table.Column<decimal>(type: "numeric(12,6)", nullable: false, defaultValue: 0m),
                    model_used = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    cost_cap_usd = table.Column<decimal>(type: "numeric(12,6)", nullable: false),
                    cost_cap_override_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cost_cap_override_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_cap_override_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_suppressed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    suppressed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    suppressed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    suppression_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    suppression_requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    suppression_request_source = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mechanic_analyses", x => x.id);
                    table.CheckConstraint("ck_mechanic_analyses_cost_cap_override_all_or_none", "(cost_cap_override_at IS NULL AND cost_cap_override_by IS NULL AND cost_cap_override_reason IS NULL) OR (cost_cap_override_at IS NOT NULL AND cost_cap_override_by IS NOT NULL AND cost_cap_override_reason IS NOT NULL)");
                    table.CheckConstraint("ck_mechanic_analyses_cost_cap_positive", "cost_cap_usd > 0");
                    table.CheckConstraint("ck_mechanic_analyses_status_range", "status BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_mechanic_analyses_suppression_completeness", "(is_suppressed = false AND suppressed_at IS NULL AND suppressed_by IS NULL AND suppression_reason IS NULL AND suppression_request_source IS NULL AND suppression_requested_at IS NULL) OR (is_suppressed = true AND suppressed_at IS NOT NULL AND suppressed_by IS NOT NULL AND suppression_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_mechanic_analyses_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mechanic_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mechanic_claims", x => x.id);
                    table.CheckConstraint("ck_mechanic_claims_display_order_non_negative", "display_order >= 0");
                    table.CheckConstraint("ck_mechanic_claims_rejection_note_when_rejected", "status <> 2 OR rejection_note IS NOT NULL");
                    table.CheckConstraint("ck_mechanic_claims_section_range", "section BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_mechanic_claims_status_range", "status BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_mechanic_claims_mechanic_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "mechanic_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mechanic_status_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: false),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mechanic_status_audit", x => x.id);
                    table.CheckConstraint("ck_mechanic_status_audit_distinct_states", "from_status <> to_status");
                    table.CheckConstraint("ck_mechanic_status_audit_status_range", "from_status BETWEEN 0 AND 3 AND to_status BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_mechanic_status_audit_mechanic_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "mechanic_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mechanic_suppression_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_suppressed = table.Column<bool>(type: "boolean", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    request_source = table.Column<int>(type: "integer", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mechanic_suppression_audit", x => x.id);
                    table.CheckConstraint("ck_mechanic_suppression_audit_request_source_range", "request_source IS NULL OR request_source BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_mechanic_suppression_audit_mechanic_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "mechanic_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mechanic_citations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_page = table.Column<int>(type: "integer", nullable: false),
                    quote = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    chunk_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mechanic_citations", x => x.id);
                    table.CheckConstraint("ck_mechanic_citations_display_order_non_negative", "display_order >= 0");
                    table.CheckConstraint("ck_mechanic_citations_page_positive", "pdf_page > 0");
                    table.CheckConstraint("ck_mechanic_citations_quote_chars_cap", "char_length(quote) <= 400");
                    table.CheckConstraint("ck_mechanic_citations_quote_not_empty", "char_length(btrim(quote)) > 0");
                    table.CheckConstraint("ck_mechanic_citations_quote_word_cap", "array_length(regexp_split_to_array(btrim(quote), '\\s+'), 1) <= 25");
                    table.ForeignKey(
                        name: "FK_mechanic_citations_mechanic_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "mechanic_claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mechanic_citations_text_chunks_chunk_id",
                        column: x => x.chunk_id,
                        principalTable: "text_chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_analyses_created_by",
                table: "mechanic_analyses",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_analyses_is_suppressed",
                table: "mechanic_analyses",
                column: "is_suppressed",
                filter: "is_suppressed = true");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_analyses_pdf_document_id",
                table: "mechanic_analyses",
                column: "pdf_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_analyses_status",
                table: "mechanic_analyses",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_mechanic_analyses_published_per_game",
                table: "mechanic_analyses",
                column: "shared_game_id",
                unique: true,
                filter: "status = 2 AND is_suppressed = false");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_citations_chunk_id",
                table: "mechanic_citations",
                column: "chunk_id",
                filter: "chunk_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_citations_claim_id",
                table: "mechanic_citations",
                column: "claim_id");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_citations_claim_page",
                table: "mechanic_citations",
                columns: new[] { "claim_id", "pdf_page" });

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_claims_analysis_id",
                table: "mechanic_claims",
                column: "analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_claims_analysis_section_order",
                table: "mechanic_claims",
                columns: new[] { "analysis_id", "section", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_claims_status",
                table: "mechanic_claims",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_status_audit_analysis_id",
                table: "mechanic_status_audit",
                column: "analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_status_audit_analysis_time",
                table: "mechanic_status_audit",
                columns: new[] { "analysis_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_suppression_audit_analysis_id",
                table: "mechanic_suppression_audit",
                column: "analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_suppression_audit_analysis_time",
                table: "mechanic_suppression_audit",
                columns: new[] { "analysis_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mechanic_citations");

            migrationBuilder.DropTable(
                name: "mechanic_status_audit");

            migrationBuilder.DropTable(
                name: "mechanic_suppression_audit");

            migrationBuilder.DropTable(
                name: "mechanic_claims");

            migrationBuilder.DropTable(
                name: "mechanic_analyses");
        }
    }
}

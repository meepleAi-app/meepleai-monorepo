using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores
    public partial class AddShareRequestAndContributorEntities_Issue2726 : Migration
#pragma warning restore CA1707 // Identifiers should not contain underscores
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contributors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary_contributor = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contributors", x => x.id);
                    table.ForeignKey(
                        name: "FK_contributors_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "share_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_shared_game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    status_before_review = table.Column<int>(type: "integer", nullable: true),
                    contribution_type = table.Column<int>(type: "integer", nullable: false),
                    user_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    admin_feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewing_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_lock_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_share_requests_shared_games_source_game_id",
                        column: x => x.source_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_share_requests_shared_games_target_shared_game_id",
                        column: x => x.target_shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "contribution_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    contributed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    share_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_ids = table.Column<string>(type: "jsonb", nullable: true),
                    includes_game_data = table.Column<bool>(type: "boolean", nullable: false),
                    includes_metadata = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contribution_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_contribution_records_contributors_contributor_id",
                        column: x => x.contributor_id,
                        principalTable: "contributors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "share_request_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    attached_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_request_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_share_request_documents_share_requests_share_request_id",
                        column: x => x.share_request_id,
                        principalTable: "share_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contribution_records_contributor_id",
                table: "contribution_records",
                column: "contributor_id");

            migrationBuilder.CreateIndex(
                name: "ix_contribution_records_contributor_version",
                table: "contribution_records",
                columns: new[] { "contributor_id", "version" });

            migrationBuilder.CreateIndex(
                name: "ix_contribution_records_share_request_id",
                table: "contribution_records",
                column: "share_request_id",
                filter: "share_request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_contributors_shared_game_id",
                table: "contributors",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_contributors_user_id",
                table: "contributors",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_contributors_user_shared_game_unique",
                table: "contributors",
                columns: new[] { "user_id", "shared_game_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_share_request_documents_document_id",
                table: "share_request_documents",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_request_documents_request_document",
                table: "share_request_documents",
                columns: new[] { "share_request_id", "document_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_share_request_documents_share_request_id",
                table: "share_request_documents",
                column: "share_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_review_lock_expires_at",
                table: "share_requests",
                column: "review_lock_expires_at",
                filter: "review_lock_expires_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_reviewing_admin_id",
                table: "share_requests",
                column: "reviewing_admin_id",
                filter: "reviewing_admin_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_source_game_id",
                table: "share_requests",
                column: "source_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_status",
                table: "share_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_share_requests_target_shared_game_id",
                table: "share_requests",
                column: "target_shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_user_id",
                table: "share_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_user_source_status",
                table: "share_requests",
                columns: new[] { "user_id", "source_game_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contribution_records");

            migrationBuilder.DropTable(
                name: "share_request_documents");

            migrationBuilder.DropTable(
                name: "contributors");

            migrationBuilder.DropTable(
                name: "share_requests");
        }
    }
}

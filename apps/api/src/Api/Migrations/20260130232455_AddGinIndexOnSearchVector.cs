using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGinIndexOnSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #3228: Complete search_vector feature (column + trigger + GIN index)
            // Root cause: Column never created, causing KeywordSearchService timeout

            // Step 1: Add tsvector column for full-text search
            migrationBuilder.Sql(@"
                ALTER TABLE text_chunks
                ADD COLUMN IF NOT EXISTS search_vector tsvector;
            ");

            // Step 2: Populate search_vector from existing Content
            migrationBuilder.Sql(@"
                UPDATE text_chunks
                SET search_vector = to_tsvector('english', COALESCE(""Content"", ''))
                WHERE search_vector IS NULL;
            ");

            // Step 3: Create trigger to auto-update search_vector on INSERT/UPDATE
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION text_chunks_search_vector_update()
                RETURNS trigger AS $$
                BEGIN
                    NEW.search_vector := to_tsvector('english', COALESCE(NEW.""Content"", ''));
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS text_chunks_search_vector_trigger ON text_chunks;

                CREATE TRIGGER text_chunks_search_vector_trigger
                BEFORE INSERT OR UPDATE ON text_chunks
                FOR EACH ROW
                EXECUTE FUNCTION text_chunks_search_vector_update();
            ");

            // Step 4: Create GIN index for fast full-text search
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_text_chunks_search_vector
                ON text_chunks USING GIN (search_vector);
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_session_tracking_participants_session_tracking_sessions_Ses~",
                table: "session_tracking_participants");

            migrationBuilder.DropIndex(
                name: "IX_session_tracking_participants_SessionId1",
                table: "session_tracking_participants");

            migrationBuilder.DropColumn(
                name: "SessionId1",
                table: "session_tracking_participants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Issue #3228: Remove search_vector feature completely
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS idx_text_chunks_search_vector;
                DROP TRIGGER IF EXISTS text_chunks_search_vector_trigger ON text_chunks;
                DROP FUNCTION IF EXISTS text_chunks_search_vector_update();
                ALTER TABLE text_chunks DROP COLUMN IF EXISTS search_vector;
            ");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId1",
                table: "session_tracking_participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_participants_SessionId1",
                table: "session_tracking_participants",
                column: "SessionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_session_tracking_participants_session_tracking_sessions_Ses~",
                table: "session_tracking_participants",
                column: "SessionId1",
                principalTable: "session_tracking_sessions",
                principalColumn: "id");
        }
    }
}

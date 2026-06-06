using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceEventIdToCounterTables_CF2 : Migration
    {
        // PR comment: questa migration include — oltre ai 5 SourceEventId additions di CF-2 —
        // 5 colonne `test_run_id` che sono *drift di main-dev*: le entity di asse-d task B
        // (UserEntity, GameNightEventEntity, GameNightInvitationEntity, GameNightRsvpEntity,
        // GameNightSessionEntity) sono state shippate al main con le proprietà TestRunId ma
        // SENZA generare la migration corrispondente. La mia regenerazione le ha catturate.
        // Le tengo qui (vs rimuoverle) perché altrimenti il ModelSnapshot le riflette ma il
        // DB non le avrebbe → next migration "perde" il drift e produce runtime errors quando
        // codice asse-d cerca di scrivere/leggere TestRunId. Tracker: aprire follow-up issue
        // se Aaron / responsabile asse-d task B preferisce migration dedicata.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_event_id",
                table: "session_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_event_id",
                table: "ProposalMigrations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_event_id",
                table: "play_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_event_id",
                table: "pdf_processing_metrics",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_event_id",
                table: "ledger_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "game_night_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "game_night_rsvps",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "game_night_invitations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "game_night_events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_session_snapshots_source_event_id",
                table: "session_snapshots",
                column: "source_event_id",
                unique: true,
                filter: "source_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_proposal_migrations_source_event_id",
                table: "ProposalMigrations",
                column: "source_event_id",
                unique: true,
                filter: "source_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_play_records_source_event_id",
                table: "play_records",
                column: "source_event_id",
                unique: true,
                filter: "source_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_pdf_processing_metrics_source_event_id",
                table: "pdf_processing_metrics",
                column: "source_event_id",
                unique: true,
                filter: "source_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ledger_entries_source_event_id",
                table: "ledger_entries",
                column: "source_event_id",
                unique: true,
                filter: "source_event_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_session_snapshots_source_event_id",
                table: "session_snapshots");

            migrationBuilder.DropIndex(
                name: "UX_proposal_migrations_source_event_id",
                table: "ProposalMigrations");

            migrationBuilder.DropIndex(
                name: "UX_play_records_source_event_id",
                table: "play_records");

            migrationBuilder.DropIndex(
                name: "UX_pdf_processing_metrics_source_event_id",
                table: "pdf_processing_metrics");

            migrationBuilder.DropIndex(
                name: "UX_ledger_entries_source_event_id",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                table: "session_snapshots");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                table: "ProposalMigrations");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                table: "play_records");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                table: "pdf_processing_metrics");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "game_night_sessions");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "game_night_rsvps");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "game_night_invitations");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "game_night_events");
        }
    }
}

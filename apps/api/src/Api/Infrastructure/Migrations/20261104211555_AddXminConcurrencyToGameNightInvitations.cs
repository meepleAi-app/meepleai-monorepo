using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddXminConcurrencyToGameNightInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #4744 — optimistic concurrency on `game_night_invitations` through PostgreSQL's `xmin`
            // system column (ADR-060). There is no schema change: `xmin` exists on every row of every
            // table, and the migration only brings the model snapshot in line with the model.
            //
            // The scaffold emitted `AddColumn<uint>("xmin", ..., rowVersion: true)`. It is removed on
            // purpose: `ADD COLUMN xmin` fails (system column names are reserved). Same hand fix as the
            // earlier xmin migrations, e.g. 20260817070722 (#3651).
            //
            // What was missing: `GameNightInvitationRepository.UpdateAsync` maps the aggregate onto a
            // detached entity and calls `Update()`, so EF wrote EVERY column from what the handler had
            // read. With no concurrency token, two overlapping writers (the guest's answer and the
            // organizer's withdrawal, or two answers) did not conflict: the last one silently won, and
            // an accepted seat could be undone by a withdrawal that had read «pending» (or the reverse).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: no column was added (see Up).
        }
    }
}

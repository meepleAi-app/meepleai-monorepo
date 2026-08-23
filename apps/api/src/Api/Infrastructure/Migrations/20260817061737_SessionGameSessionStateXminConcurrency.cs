using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessionGameSessionStateXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Safety Gate (#1087, rollback-runbook §8.2): i DROP COLUMN sono
            // rollback-safe perché le colonne sono già di fatto morte. Nessun trigger le popola
            // (`ef_update_row_version()` rimosso da #2305) e Postgres non valorizza una `bytea`,
            // quindi contengono NULL su ogni riga. Il Down() le ricrea identiche.
            //
            // NB: i due nomi differiscono — `row_version` su session_tracking_sessions,
            // `RowVersion` in PascalCase su game_session_states, perché solo la prima
            // configuration dichiarava HasColumnName. Presi dallo schema, non dedotti.
            migrationBuilder.Sql("-- safe: drop dead bytea concurrency columns, NULL on every row (no trigger populates them since #2305); replaced by the xmin system column (§8.3)");

            // #3651 lotto 10 — le ultime due entità del lotto dopo RuleSpecEntity (isolata: il suo
            // token esce dal boundary HTTP come ETag).
            //
            // `sessions`: la riga con la superficie più larga del lotto — una sessione di gioco è
            // toccata da più partecipanti insieme (punteggi, note, stato), ed è il dominio in cui
            // due schede aperte sono la norma. Misurato prima della conversione:
            //   SessionXminConcurrencyTests.Update_AfterConcurrentWrite_… → «no exception was thrown»
            //
            // `game_session_states`: il commento sull'entità dichiarava «Optimistic locking via
            // PostgreSQL xmin (EF Core Timestamp)» — quarta affermazione di questo tipo trovata da
            // #3651, e falsa come le altre tre: `[Timestamp]` su `byte[]` produce una `bytea`.
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "session_tracking_sessions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "game_session_states");

            // NB: nessun AddColumn per `xmin`, benché lo scaffold ne avesse generati due.
            // `xmin` è una colonna di SISTEMA: `ADD COLUMN xmin` fallirebbe con
            // «column name "xmin" conflicts with a system column name».
            // Decima volta che va corretto a mano: 20260811130532 (#3658), 20260811224354 (#3683),
            // 20260812035720 (#3698), 20260816120212 (#3710), 20260816150535 (#3714),
            // 20260816153040 (#3715), 20260816162423 (#3716), 20260816164401 (#3717),
            // 20260816165510 (#3718).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina le colonne, non un meccanismo che le valorizzi: tornando indietro il token
            // resterebbe NULL come prima. Nessun DropColumn per `xmin`: non è stata aggiunta.
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "session_tracking_sessions",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "game_session_states",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}

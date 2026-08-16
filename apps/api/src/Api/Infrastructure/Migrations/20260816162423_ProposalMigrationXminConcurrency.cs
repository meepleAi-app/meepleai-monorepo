using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProposalMigrationXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Safety Gate (#1087, rollback-runbook §8.2): il DROP COLUMN è rollback-safe
            // perché la colonna è già di fatto morta. Nessun trigger la popola
            // (`ef_update_row_version()` rimosso da #2305) e Postgres non valorizza una `bytea`,
            // quindi contiene NULL su ogni riga. Il Down() la ricrea identica.
            //
            // Come in #3715, il nome è `RowVersion` in PascalCase — la configuration non
            // dichiarava `HasColumnName`. Va preso dallo schema, non dedotto.
            migrationBuilder.Sql("-- safe: drop dead bytea concurrency column, NULL on every row (no trigger populates it since #2305); replaced by the xmin system column (§8.3)");

            // #3651 lotto 7. Misurato prima della conversione:
            //   ProposalMigrationXminConcurrencyTests.Update_AfterConcurrentWrite_… → «no exception was thrown»
            //
            // La riga registra la scelta dell'utente dopo l'approvazione di una proposta (collegare
            // al catalogo o tenere privato): una decisione one-shot che due percorsi possono
            // toccare insieme. Senza token, un automatismo poteva sovrascrivere una scelta esplicita.
            //
            // Come #3715, la conversione richiedeva anche il round-trip del token: UpdateAsync ha un
            // ramo che riattacca un grafo detached (:97), e senza il token nel mapper ogni scrittura
            // sarebbe fallita (#3688).
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProposalMigrations");

            // NB: nessun AddColumn per `xmin` — colonna di SISTEMA, `ADD COLUMN xmin` fallirebbe con
            // «column name "xmin" conflicts with a system column name».
            // Settima volta che va corretto a mano: 20260811130532 (#3658), 20260811224354 (#3683),
            // 20260812035720 (#3698), 20260816120212 (#3710), 20260816150535 (#3714),
            // 20260816153040 (#3715).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina la colonna, non un meccanismo che la valorizzi: tornando indietro il token
            // resterebbe NULL come prima. Nessun DropColumn per `xmin`: non è stata aggiunta.
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProposalMigrations",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}

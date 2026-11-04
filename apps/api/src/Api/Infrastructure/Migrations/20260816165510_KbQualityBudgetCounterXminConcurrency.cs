using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KbQualityBudgetCounterXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Safety Gate (#1087, rollback-runbook §8.2): il DROP COLUMN è rollback-safe
            // perché la colonna è già di fatto morta. Nessun trigger la popola
            // (`ef_update_row_version()` rimosso da #2305) e Postgres non valorizza una `bytea`,
            // quindi contiene NULL su ogni riga. Il Down() la ricrea identica.
            migrationBuilder.Sql("-- safe: drop dead bytea concurrency column, NULL on every row (no trigger populates it since #2305); replaced by the xmin system column (§8.3)");

            // #3651 lotto 9 — l'entità con la conseguenza più concreta dell'intero lotto.
            //
            // `EvaluationRepository.IncrementSpentAsync` ha un retry loop esplicito su
            // DbUpdateConcurrencyException, scritto per impedire che due valutazioni concorrenti
            // dello stesso tenant perdano un incremento di spesa. Quel loop NON è mai entrato in
            // funzione: il token era `byte[]` su `bytea` e l'eccezione non poteva essere sollevata.
            //
            // Misurato prima della conversione, su un contatore da 10 USD con due incrementi
            // concorrenti da 15 e 5:
            //   KbQualityBudgetCounterXminConcurrencyTests.…_RetriesAndKeepsBothIncrements
            //   → «Expected persisted.SpentUsd to be 30M … but found 15.0000M»
            // Quindici dollari di spesa spariti, e un tetto di budget superabile in silenzio.
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "kb_quality_budget_counters");

            // NB: nessun AddColumn per `xmin` — colonna di SISTEMA, `ADD COLUMN xmin` fallirebbe.
            // Nona volta che va corretto a mano: 20260811130532 (#3658), 20260811224354 (#3683),
            // 20260812035720 (#3698), 20260816120212 (#3710), 20260816150535 (#3714),
            // 20260816153040 (#3715), 20260816162423 (#3716), 20260816164401 (lotto 8).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina la colonna, non un meccanismo che la valorizzi: tornando indietro il token
            // resterebbe NULL e il retry loop tornerebbe inerte. Nessun DropColumn per `xmin`.
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "kb_quality_budget_counters",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}

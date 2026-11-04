using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RuleSpecXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Safety Gate (#1087, rollback-runbook §8.2): il DROP COLUMN è rollback-safe
            // perché la colonna è già di fatto morta. Nessun trigger la popola
            // (`ef_update_row_version()` rimosso da #2305) e Postgres non valorizza una `bytea`,
            // quindi contiene NULL su ogni riga. Il Down() la ricrea identica.
            migrationBuilder.Sql("-- safe: drop dead bytea concurrency column, NULL on every row (no trigger populates it since #2305); replaced by the xmin system column (§8.3)");

            // #3651 — UNDICESIMA e ultima entità del lotto, e l'unica il cui token esce dal
            // boundary HTTP: viene esposto come ETag in RuleSpecDto/GameDto e i client lo rimandano
            // in UpdateRuleSpecCommand.ExpectedETag (editing collaborativo, #2055).
            //
            // Che cosa non ha mai funzionato: UpdateRuleSpecCommandHandler confrontava l'ETag solo
            // dentro una guardia `latestSpec.RowVersion != null`, sempre falsa con un token bytea.
            // Il ConflictException «RuleSpec has been modified by another user» non è mai stato
            // sollevato, e l'ETag restituito ai client è sempre stato null. Misurato:
            //   RuleSpecXminConcurrencyTests.Update_AfterConcurrentWrite_… → «no exception was thrown»
            //   RuleSpecXminConcurrencyTests.GetRuleSpec_ExposesANonNullETag → ETag null
            //
            // Il cambio di formato dell'ETag (da base64 di bytea a rappresentazione decimale di
            // xmin) NON rompe alcun client: nessuno ne possiede uno valido, perché è sempre stato
            // null. È la ragione per cui questa conversione, benché tocchi un contratto pubblico,
            // è sicura.
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "rule_specs");

            // NB: nessun AddColumn per `xmin` — colonna di SISTEMA, `ADD COLUMN xmin` fallirebbe.
            // Undicesima e ultima volta che va corretto a mano in questo lotto: 20260811130532
            // (#3658), 20260811224354 (#3683), 20260812035720 (#3698), 20260816120212 (#3710),
            // 20260816150535 (#3714), 20260816153040 (#3715), 20260816162423 (#3716),
            // 20260816164401 (#3717), 20260816165510 (#3718), 20260817061737 (#3729).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina la colonna, non un meccanismo che la valorizzi: tornando indietro l'ETag
            // tornerebbe null e l'editing collaborativo tornerebbe inerte. Nessun DropColumn per
            // `xmin`: non è stata aggiunta.
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "rule_specs",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}

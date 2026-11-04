using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserLibraryEntryXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Safety Gate (#1087, rollback-runbook §8.2): il DROP COLUMN è rollback-safe
            // perché la colonna è già di fatto morta. Nessun trigger la popola
            // (`ef_update_row_version()` rimosso da #2305) e Postgres non valorizza una `bytea`,
            // quindi contiene NULL su ogni riga. Il Down() la ricrea identica.
            //
            // NB: qui la colonna si chiama `RowVersion` in PascalCase, non `row_version` — la
            // configuration non dichiarava `HasColumnName`, a differenza delle entità dei lotti
            // precedenti. Il nome va preso dallo schema, non dedotto dalla convenzione.
            migrationBuilder.Sql("-- safe: drop dead bytea concurrency column, NULL on every row (no trigger populates it since #2305); replaced by the xmin system column (§8.3)");

            // #3651 lotto 6 — sesta entità allineata al pattern xmin di ADR-060.
            // Misurato prima della conversione:
            //   UserLibraryEntryXminConcurrencyTests.Update_AfterConcurrentWrite_… → «no exception was thrown»
            //
            // ⚠️ Questa entità è la prima del lotto il cui write-path richiedeva anche il lavoro di
            // #3688: `UserLibraryRepository.UpdateAsync` persiste un grafo DETACHED
            // (`MapToPersistence` + `Update()`), quindi il token deve attraversare il mapper. Senza
            // quel round-trip la conversione avrebbe spostato il guasto da «non protegge nulla» a
            // «rifiuta ogni scrittura». Il test `Update_WithoutConcurrentWrite_Succeeds` è ciò che
            // lo dimostra: fallirebbe se il mapper perdesse il token.
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "user_library_entries");

            // NB: nessun AddColumn per `xmin`, benché lo scaffold di EF ne avesse generato uno.
            // `xmin` è una colonna di SISTEMA di PostgreSQL: `ADD COLUMN xmin` fallirebbe con
            // «column name "xmin" conflicts with a system column name».
            // Sesta volta che va corretto a mano: 20260811130532 (#3658), 20260811224354 (#3683),
            // 20260812035720 (#3698), 20260816120212 (#3710), 20260816150535 (#3714).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina la colonna, non un meccanismo che la valorizzi: tornando indietro il token
            // resterebbe NULL come prima. Nessun DropColumn per `xmin`: non è stata aggiunta.
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "user_library_entries",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}

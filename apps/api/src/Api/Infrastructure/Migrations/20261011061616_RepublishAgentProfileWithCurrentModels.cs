using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #4179: la versione 1 del profilo agente, pubblicata dal seed di ADR-095 fetta 4, nomina due
    /// modelli dismessi dai provider: <c>deepseek-chat</c> (DeepSeek, dal 2026-07-24) come principale
    /// e <c>meta-llama/llama-3.3-70b-instruct:free</c> (sparito dal listino di OpenRouter, verificato il
    /// 2026-10-10) come riserva. Una versione pubblicata non si modifica (D2): se ne pubblica una nuova
    /// con lo stesso contenuto e i due modelli correnti, e la 1 passa in archivio.
    /// </summary>
    /// <remarks>
    /// Scatta solo se la versione pubblicata nomina ancora uno dei due modelli: un profilo già
    /// ripubblicato non si tocca. Il contenuto della nuova versione è quello della pubblicata, con
    /// sostituita solo la chiave che nomina un modello dismesso: un modello scelto altrimenti resta.
    /// </remarks>
    public partial class RepublishAgentProfileWithCurrentModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH published AS (
                    SELECT id, agent_profile_id, content
                    FROM knowledge_base.agent_profile_versions
                    WHERE agent_profile_id = 'a9e7f5c1-4167-4095-9a1e-000000000001'
                      AND status = 1
                      AND (content->>'primaryModelId' = 'deepseek-chat'
                           OR content->>'fallbackModelId' = 'meta-llama/llama-3.3-70b-instruct:free')
                ),
                archived AS (
                    UPDATE knowledge_base.agent_profile_versions v
                    SET status = 2, archived_at = NOW()
                    FROM published p
                    WHERE v.id = p.id
                    RETURNING p.agent_profile_id, p.content
                )
                INSERT INTO knowledge_base.agent_profile_versions
                    (id, agent_profile_id, version_number, status, content, created_at, published_at)
                SELECT
                    'a9e7f5c1-4167-4095-9a1e-000000000102',
                    a.agent_profile_id,
                    (SELECT MAX(version_number) + 1
                     FROM knowledge_base.agent_profile_versions
                     WHERE agent_profile_id = a.agent_profile_id),
                    1,
                    a.content || jsonb_build_object(
                        'primaryModelId',
                        CASE WHEN a.content->>'primaryModelId' = 'deepseek-chat'
                             THEN 'deepseek-flash'
                             ELSE a.content->>'primaryModelId' END,
                        'fallbackModelId',
                        CASE WHEN a.content->>'fallbackModelId' = 'meta-llama/llama-3.3-70b-instruct:free'
                             THEN 'meta-llama/llama-3.3-70b-instruct'
                             ELSE a.content->>'fallbackModelId' END),
                    NOW(),
                    NOW()
                FROM archived a;

                UPDATE knowledge_base.agent_profiles
                SET updated_at = NOW()
                WHERE id = 'a9e7f5c1-4167-4095-9a1e-000000000001'
                  AND EXISTS (
                      SELECT 1 FROM knowledge_base.agent_profile_versions
                      WHERE id = 'a9e7f5c1-4167-4095-9a1e-000000000102');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina la versione archiviata da Up, solo se la nuova è ancora quella pubblicata.
            migrationBuilder.Sql(
                """
                WITH removed AS (
                    DELETE FROM knowledge_base.agent_profile_versions
                    WHERE id = 'a9e7f5c1-4167-4095-9a1e-000000000102' AND status = 1
                    RETURNING agent_profile_id, version_number
                )
                UPDATE knowledge_base.agent_profile_versions v
                SET status = 1, archived_at = NULL
                FROM removed r
                WHERE v.agent_profile_id = r.agent_profile_id
                  AND v.version_number = r.version_number - 1
                  AND v.status = 2;
                """);
        }
    }
}

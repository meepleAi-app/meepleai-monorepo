using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_profiles",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_profiles", x => x.id);
                    table.CheckConstraint("ck_agent_profiles_singleton", "id = 'a9e7f5c1-4167-4095-9a1e-000000000001'");
                });

            migrationBuilder.CreateTable(
                name: "agent_profile_versions",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_profile_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_profile_versions_agent_profiles_agent_profile_id",
                        column: x => x.agent_profile_id,
                        principalSchema: "knowledge_base",
                        principalTable: "agent_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_agent_profile_versions_one_draft",
                schema: "knowledge_base",
                table: "agent_profile_versions",
                column: "agent_profile_id",
                unique: true,
                filter: "status = 0");

            migrationBuilder.CreateIndex(
                name: "ux_agent_profile_versions_profile_version",
                schema: "knowledge_base",
                table: "agent_profile_versions",
                columns: new[] { "agent_profile_id", "version_number" },
                unique: true);

            // «Esattamente una pubblicata», controllato al commit e non riga per riga: vedi il
            // commento in AgentProfileVersionConfiguration. Un indice unico parziale non può essere
            // differito; un vincolo di esclusione sì, e su btree con = equivale a un unico.
            migrationBuilder.Sql(
                """
                ALTER TABLE knowledge_base.agent_profile_versions
                    ADD CONSTRAINT ex_agent_profile_versions_one_published
                    EXCLUDE USING btree (agent_profile_id WITH =) WHERE (status = 1)
                    DEFERRABLE INITIALLY DEFERRED;
                """);

            // Seed della versione 1 (ADR-095 fetta 4): i valori oggi fissi nel codice, pubblicati.
            // Sta nello schema e non in un seeder applicativo per la stessa ragione di
            // SeedCertificationThresholdsSingleton: la tabella dichiara il singleton, e una tabella
            // che ammette una riga sola e non ce l'ha è valida per il DB ma non per il dominio.
            // Il jsonb segue AgentProfileContentJson (camelCase, enum come stringhe); il test
            // AgentProfilePersistenceIntegrationTests lo rilegge e lo confronta con valori scritti a mano.
            // Idempotente: non sovrascrive mai un profilo già presente.
            migrationBuilder.Sql(
                """
                INSERT INTO knowledge_base.agent_profiles (id, created_at, updated_at)
                VALUES ('a9e7f5c1-4167-4095-9a1e-000000000001', NOW(), NOW())
                ON CONFLICT (id) DO NOTHING;

                INSERT INTO knowledge_base.agent_profile_versions
                    (id, agent_profile_id, version_number, status, content, created_at, published_at)
                SELECT
                    'a9e7f5c1-4167-4095-9a1e-000000000101',
                    'a9e7f5c1-4167-4095-9a1e-000000000001',
                    1,
                    1,
                    $content${
                        "name": "MeepleAI",
                        "description": "Specialized agent for board game rules interpretation and clarification.",
                        "persona": "a precise board game rules assistant",
                        "primaryModelId": "deepseek-chat",
                        "fallbackModelId": "meta-llama/llama-3.3-70b-instruct:free",
                        "temperature": 0.3,
                        "maxResponseTokens": 1500,
                        "citationStyle": "PageReferencesInText",
                        "systemPrompts": {
                            "en": "You are MeepleAI, a precise board game rules assistant. Answer ONLY using the provided rulebook context. For each claim you make, it MUST be directly supported by the context provided. If the context does not contain the answer, respond EXACTLY with: 'This information is not available in the provided rulebook.' Never invent rules, game mechanics, or examples not present in the context. Always cite the page number in brackets, e.g. [Page 3]."
                        },
                        "fallbackLanguage": "en",
                        "candidatePoolSize": 20,
                        "finalTopK": 5,
                        "minScore": 0.55,
                        "vectorWeight": 0.7,
                        "keywordWeight": 0.3,
                        "rerankerEnabled": true,
                        "allowedCategories": ["Rulebook", "Expansion", "Errata", "QuickStart", "Reference", "PlayerAid", "Other"]
                    }$content$::jsonb,
                    NOW(),
                    NOW()
                WHERE NOT EXISTS (
                    SELECT 1 FROM knowledge_base.agent_profile_versions
                    WHERE agent_profile_id = 'a9e7f5c1-4167-4095-9a1e-000000000001'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_profile_versions",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "agent_profiles",
                schema: "knowledge_base");
        }
    }
}

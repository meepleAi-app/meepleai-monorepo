using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <summary>
    /// CONFIG-04: Seed RAG configuration parameters into system_configurations table.
    /// Provides database-driven configuration for vector search (TopK), relevance thresholds (MinScore),
    /// query expansion (EnableQueryExpansion, MaxQueryVariations), and RRF fusion (RrfK).
    /// Fallback chain: Database → appsettings.json → hardcoded defaults in RagService.
    /// </summary>
    public partial class SeedRagConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CONFIG-04: Insert RAG configuration parameters
            // These configurations control the RAG pipeline behavior for vector search and query expansion
            migrationBuilder.Sql(@"
                INSERT INTO system_configurations (
                    ""Id"",
                    ""Key"",
                    ""Value"",
                    ""ValueType"",
                    ""Description"",
                    ""Category"",
                    ""Environment"",
                    ""IsActive"",
                    ""CreatedAt"",
                    ""UpdatedAt"",
                    ""CreatedByUserId"",
                    ""UpdatedByUserId""
                ) VALUES
                -- Production environment configurations
                (
                    'config-rag-topk-prod',
                    'RAG.TopK',
                    '5',
                    'Integer',
                    'Number of top results to retrieve from vector search (1-50)',
                    'RAG',
                    'Production',
                    true,
                    NOW(),
                    NOW(),
                    'demo-admin-001',
                    'demo-admin-001'
                ),
                (
                    'config-rag-minscore-prod',
                    'RAG.MinScore',
                    '0.7',
                    'Double',
                    'Minimum similarity score threshold for vector search results (0.0-1.0)',
                    'RAG',
                    'Production',
                    true,
                    NOW(),
                    NOW(),
                    'demo-admin-001',
                    'demo-admin-001'
                ),
                (
                    'config-rag-rrfk-prod',
                    'RAG.RrfK',
                    '60',
                    'Integer',
                    'Reciprocal Rank Fusion constant for combining multiple query results (1-100)',
                    'RAG',
                    'Production',
                    true,
                    NOW(),
                    NOW(),
                    'demo-admin-001',
                    'demo-admin-001'
                ),
                (
                    'config-rag-enableexpansion-prod',
                    'RAG.EnableQueryExpansion',
                    'true',
                    'Boolean',
                    'Enable query expansion for improved recall (PERF-08 feature)',
                    'RAG',
                    'Production',
                    true,
                    NOW(),
                    NOW(),
                    'demo-admin-001',
                    'demo-admin-001'
                ),
                (
                    'config-rag-maxvariations-prod',
                    'RAG.MaxQueryVariations',
                    '4',
                    'Integer',
                    'Maximum number of query variations for expansion (1-10)',
                    'RAG',
                    'Production',
                    true,
                    NOW(),
                    NOW(),
                    'demo-admin-001',
                    'demo-admin-001'
                ),
                -- Development environment configurations
                (
                    'config-rag-topk-dev',
                    'RAG.TopK',
                    '5',
                    'Integer',
                    'Number of top results to retrieve from vector search (Development)',
                    'RAG',
                    'Development',
                    true,
                    NOW(),
                    NOW(),
                    'demo-admin-001',
                    'demo-admin-001'
                ),
                (
                    'config-rag-minscore-dev',
                    'RAG.MinScore',
                    '0.7',
                    'Double',
                    'Minimum similarity score threshold for vector search (Development)',
                    'RAG',
                    'Development',
                    true,
                    NOW(),
                    NOW(),
                    'demo-admin-001',
                    'demo-admin-001'
                ),
                (
                    'config-rag-rrfk-dev',
                    'RAG.RrfK',
                    '60',
                    'Integer',
                    'Reciprocal Rank Fusion constant (Development)',
                    'RAG',
                    'Development',
                    true,
                    NOW(),
                    NOW(),
                    'demo-admin-001',
                    'demo-admin-001'
                ),
                (
                    'config-rag-enableexpansion-dev',
                    'RAG.EnableQueryExpansion',
                    'true',
                    'Boolean',
                    'Enable query expansion (Development)',
                    'RAG',
                    'Development',
                    true,
                    NOW(),
                    NOW(),
                    'demo-admin-001',
                    'demo-admin-001'
                ),
                (
                    'config-rag-maxvariations-dev',
                    'RAG.MaxQueryVariations',
                    '4',
                    'Integer',
                    'Maximum query variations for expansion (Development)',
                    'RAG',
                    'Development',
                    true,
                    NOW(),
                    NOW(),
                    'demo-admin-001',
                    'demo-admin-001'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // CONFIG-04: Remove RAG configuration parameters
            migrationBuilder.Sql(@"
                DELETE FROM system_configurations
                WHERE ""Id"" IN (
                    'config-rag-topk-prod',
                    'config-rag-minscore-prod',
                    'config-rag-rrfk-prod',
                    'config-rag-enableexpansion-prod',
                    'config-rag-maxvariations-prod',
                    'config-rag-topk-dev',
                    'config-rag-minscore-dev',
                    'config-rag-rrfk-dev',
                    'config-rag-enableexpansion-dev',
                    'config-rag-maxvariations-dev'
                );
            ");
        }
    }
}

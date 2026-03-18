using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class ListTablesHandler : IQueryHandler<ListTablesQuery, IReadOnlyList<TableInfo>>
{
    private static readonly Dictionary<string, string> TableToBoundedContext = new(StringComparer.Ordinal)
    {
        ["users"] = "Administration",
        ["audit_logs"] = "Administration",
        ["ai_request_logs"] = "Administration",
        ["user_ai_consents"] = "Administration",
        ["user_sessions"] = "Authentication",
        ["api_keys"] = "Authentication",
        ["api_key_usage_logs"] = "Authentication",
        ["oauth_accounts"] = "Authentication",
        ["games"] = "GameManagement",
        ["game_sessions"] = "GameManagement",
        ["play_records"] = "GameManagement",
        ["record_players"] = "GameManagement",
        ["record_scores"] = "GameManagement",
        ["rule_conflict_faqs"] = "GameManagement",
        ["game_reviews"] = "GameManagement",
        ["game_strategies"] = "GameManagement",
        ["live_game_sessions"] = "SessionTracking",
        ["session_participants"] = "SessionTracking",
        ["session_invites"] = "SessionTracking",
        ["session_players"] = "SessionTracking",
        ["session_teams"] = "SessionTracking",
        ["live_round_scores"] = "SessionTracking",
        ["live_turn_records"] = "SessionTracking",
        ["toolkit_session_states"] = "SessionTracking",
        ["game_toolkits"] = "GameToolkit",
        ["toolkits"] = "GameToolkit",
        ["rule_specs"] = "KnowledgeBase",
        ["rule_atoms"] = "KnowledgeBase",
        ["agents"] = "KnowledgeBase",
        ["chats"] = "KnowledgeBase",
        ["chat_threads"] = "KnowledgeBase",
        ["chat_logs"] = "KnowledgeBase",
        ["agent_feedbacks"] = "KnowledgeBase",
        ["agent_configurations"] = "KnowledgeBase",
        ["agent_typologies"] = "KnowledgeBase",
        ["typology_prompt_templates"] = "KnowledgeBase",
        ["agent_sessions"] = "KnowledgeBase",
        ["pdf_documents"] = "DocumentProcessing",
        ["processing_metrics"] = "DocumentProcessing",
        ["processing_jobs"] = "DocumentProcessing",
        ["processing_steps"] = "DocumentProcessing",
        ["step_log_entries"] = "DocumentProcessing",
        ["processing_queue_configs"] = "DocumentProcessing",
        ["vector_documents"] = "DocumentProcessing",
        ["text_chunks"] = "DocumentProcessing",
        ["n8n_configs"] = "WorkflowIntegration",
        ["rule_spec_comments"] = "KnowledgeBase",
        ["prompt_templates"] = "KnowledgeBase",
        ["prompt_versions"] = "KnowledgeBase",
        ["prompt_audit_logs"] = "KnowledgeBase",
        ["system_configurations"] = "SystemConfiguration",
    };

    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;

    public ListTablesHandler(MeepleAiDbContext dbContext, IRemoteDatabaseConnector remoteConnector)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _remoteConnector = remoteConnector ?? throw new ArgumentNullException(nameof(remoteConnector));
    }

    public async Task<IReadOnlyList<TableInfo>> Handle(ListTablesQuery query, CancellationToken cancellationToken)
    {
        var localConn = _dbContext.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Expected NpgsqlConnection");
        if (localConn.State != System.Data.ConnectionState.Open)
        {
            await localConn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var localTables = await ReadTableStatsAsync(localConn, cancellationToken).ConfigureAwait(false);

        var remoteConn = await _remoteConnector.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (remoteConn.ConfigureAwait(false))
        {
            var remoteTables = await ReadTableStatsAsync(remoteConn, cancellationToken).ConfigureAwait(false);

            var allTableNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in localTables.Keys)
                allTableNames.Add(key);
            foreach (var key in remoteTables.Keys)
                allTableNames.Add(key);

            var result = new List<TableInfo>(allTableNames.Count);
            foreach (var tableName in allTableNames.OrderBy(t => t, StringComparer.Ordinal))
            {
                localTables.TryGetValue(tableName, out var localCount);
                remoteTables.TryGetValue(tableName, out var remoteCount);
                TableToBoundedContext.TryGetValue(tableName, out var boundedContext);

                result.Add(new TableInfo(
                    TableName: tableName,
                    SchemaName: "public",
                    LocalRowCount: localCount,
                    StagingRowCount: remoteCount,
                    BoundedContext: boundedContext));
            }

            return result;
        }
    }

    private static async Task<Dictionary<string, long>> ReadTableStatsAsync(
        NpgsqlConnection conn, CancellationToken ct)
    {
        var tables = new Dictionary<string, long>(StringComparer.Ordinal);
        var cmd = new NpgsqlCommand(
            "SELECT relname, COALESCE(n_live_tup, 0) FROM pg_stat_user_tables WHERE schemaname = 'public' ORDER BY relname",
            conn);
        await using (cmd.ConfigureAwait(false))
        {
            var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    tables[reader.GetString(0)] = reader.GetInt64(1);
                }
            }
        }
        return tables;
    }
}

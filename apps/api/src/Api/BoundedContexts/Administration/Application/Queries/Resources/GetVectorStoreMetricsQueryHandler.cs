using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Handler for vector store metrics query.
/// Queries real pgvector data from the pgvector_embeddings table and
/// other embedding-bearing tables (conversation_memory, strategy_patterns,
/// agent_game_state_snapshots).
/// Issue #3695: Resources Monitoring - Vector store metrics
/// </summary>
internal class GetVectorStoreMetricsQueryHandler : IQueryHandler<GetVectorStoreMetricsQuery, VectorStoreMetricsDto>
{
    private readonly MeepleAiDbContext _db;

    public GetVectorStoreMetricsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<VectorStoreMetricsDto> Handle(GetVectorStoreMetricsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var connection = _db.Database.GetDbConnection();

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var collections = new List<CollectionStatsDto>();
            long totalVectors = 0;
            long totalMemoryBytes = 0;

            // Query the main pgvector_embeddings table
            var mainCollection = await QueryTableMetricsAsync(
                connection, "pgvector_embeddings", "vector",
                "idx_pgvector_embeddings_vector_cosine", cancellationToken).ConfigureAwait(false);
            if (mainCollection != null)
            {
                collections.Add(mainCollection);
                totalVectors += mainCollection.VectorCount;
                totalMemoryBytes += mainCollection.MemoryBytes;
            }

            // Query auxiliary embedding tables
            var auxTables = new[]
            {
                ("conversation_memory", "embedding", (string?)null),
                ("strategy_patterns", "embedding", (string?)null),
                ("agent_game_state_snapshots", "embedding", (string?)null),
            };

            foreach (var (tableName, columnName, indexName) in auxTables)
            {
                var auxCollection = await QueryTableMetricsAsync(
                    connection, tableName, columnName, indexName, cancellationToken).ConfigureAwait(false);
                if (auxCollection != null)
                {
                    collections.Add(auxCollection);
                    totalVectors += auxCollection.VectorCount;
                    totalMemoryBytes += auxCollection.MemoryBytes;
                }
            }

            return new VectorStoreMetricsDto(
                TotalCollections: collections.Count,
                TotalVectors: totalVectors,
                IndexedVectors: totalVectors, // All rows in pgvector tables are indexed
                MemoryBytes: totalMemoryBytes,
                MemoryFormatted: FormatBytes(totalMemoryBytes),
                Collections: collections,
                MeasuredAt: DateTime.UtcNow
            );
        }
        catch (Exception)
        {
            // If pgvector tables don't exist or any query fails, return empty metrics gracefully
            return new VectorStoreMetricsDto(
                TotalCollections: 0,
                TotalVectors: 0,
                IndexedVectors: 0,
                MemoryBytes: 0,
                MemoryFormatted: "0 B",
                Collections: new List<CollectionStatsDto>(),
                MeasuredAt: DateTime.UtcNow
            );
        }
        finally
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Queries metrics for a single table that contains vector embeddings.
    /// Returns null if the table does not exist.
    /// </summary>
    private static async Task<CollectionStatsDto?> QueryTableMetricsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        string vectorColumnName,
        string? indexName,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if the table exists
            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = @"
                    SELECT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = @tableName
                    )";
                var param = checkCmd.CreateParameter();
                param.ParameterName = "@tableName";
                param.Value = tableName;
                checkCmd.Parameters.Add(param);

                var exists = await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (exists is not true && exists is not (object)true)
                {
                    // Handle both bool and boxed bool
                    if (exists == null || !Convert.ToBoolean(exists, System.Globalization.CultureInfo.InvariantCulture))
                        return null;
                }
            }

            // Count vectors (rows where the embedding column is not null)
            long vectorCount;
            using (var countCmd = connection.CreateCommand())
            {
                countCmd.CommandText = $"SELECT count(*) FROM \"{tableName}\" WHERE \"{vectorColumnName}\" IS NOT NULL";
                var result = await countCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                vectorCount = result != null ? Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture) : 0;
            }

            // Get table + index size (total relation size includes indexes and TOAST)
            long memoryBytes;
            using (var sizeCmd = connection.CreateCommand())
            {
                sizeCmd.CommandText = $"SELECT pg_total_relation_size('{tableName}')";
                var result = await sizeCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                memoryBytes = result != null ? Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture) : 0;
            }

            // Get vector dimensions from the column type definition (e.g. vector(768))
            int dimensions = 0;
            using (var dimCmd = connection.CreateCommand())
            {
                dimCmd.CommandText = @"
                    SELECT CASE
                        WHEN udt_name = 'vector' AND character_maximum_length IS NOT NULL
                            THEN character_maximum_length
                        ELSE 0
                    END
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = @tableName
                      AND column_name = @columnName";
                var tableParam = dimCmd.CreateParameter();
                tableParam.ParameterName = "@tableName";
                tableParam.Value = tableName;
                dimCmd.Parameters.Add(tableParam);
                var colParam = dimCmd.CreateParameter();
                colParam.ParameterName = "@columnName";
                colParam.Value = vectorColumnName;
                dimCmd.Parameters.Add(colParam);

                var result = await dimCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (result != null && result != DBNull.Value)
                {
                    dimensions = Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            // If information_schema didn't return dimensions, try pg_attribute with atttypmod
            if (dimensions == 0)
            {
                using var typCmd = connection.CreateCommand();
                typCmd.CommandText = @"
                    SELECT atttypmod
                    FROM pg_attribute a
                    JOIN pg_class c ON a.attrelid = c.oid
                    JOIN pg_namespace n ON c.relnamespace = n.oid
                    WHERE n.nspname = 'public'
                      AND c.relname = @tableName
                      AND a.attname = @columnName
                      AND a.atttypmod > 0";
                var tp = typCmd.CreateParameter();
                tp.ParameterName = "@tableName";
                tp.Value = tableName;
                typCmd.Parameters.Add(tp);
                var cp = typCmd.CreateParameter();
                cp.ParameterName = "@columnName";
                cp.Value = vectorColumnName;
                typCmd.Parameters.Add(cp);

                var result = await typCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (result != null && result != DBNull.Value)
                {
                    dimensions = Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            // Determine distance metric from index if one exists
            string distanceMetric = "cosine"; // default for pgvector HNSW with vector_cosine_ops
            if (!string.IsNullOrEmpty(indexName))
            {
                using var idxCmd = connection.CreateCommand();
                idxCmd.CommandText = @"
                    SELECT indexdef FROM pg_indexes
                    WHERE schemaname = 'public'
                      AND tablename = @tableName
                      AND indexname = @indexName";
                var tp = idxCmd.CreateParameter();
                tp.ParameterName = "@tableName";
                tp.Value = tableName;
                idxCmd.Parameters.Add(tp);
                var ip = idxCmd.CreateParameter();
                ip.ParameterName = "@indexName";
                ip.Value = indexName;
                idxCmd.Parameters.Add(ip);

                var result = await idxCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (result is string indexDef)
                {
                    if (indexDef.Contains("vector_l2_ops", StringComparison.OrdinalIgnoreCase))
                        distanceMetric = "l2";
                    else if (indexDef.Contains("vector_ip_ops", StringComparison.OrdinalIgnoreCase))
                        distanceMetric = "inner_product";
                    else if (indexDef.Contains("vector_cosine_ops", StringComparison.OrdinalIgnoreCase))
                        distanceMetric = "cosine";
                }
            }

            return new CollectionStatsDto(
                CollectionName: tableName,
                VectorCount: vectorCount,
                IndexedCount: vectorCount,
                VectorDimensions: dimensions,
                DistanceMetric: distanceMetric,
                MemoryBytes: memoryBytes,
                MemoryFormatted: FormatBytes(memoryBytes)
            );
        }
        catch (Exception)
        {
            // If any individual table query fails, skip it gracefully
            return null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##} {1}", len, sizes[order]);
    }
}

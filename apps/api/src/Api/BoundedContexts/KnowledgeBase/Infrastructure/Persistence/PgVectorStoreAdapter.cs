using System.Data;
using System.Data.Common;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using DomainVector = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.Vector;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// pgvector-backed implementation of IQdrantVectorStoreAdapter.
/// Uses a dedicated pgvector_embeddings table with HNSW indexing for similarity search.
/// Replaces Qdrant as the vector store for MVP stack simplification.
/// </summary>
internal sealed class PgVectorStoreAdapter : IQdrantVectorStoreAdapter
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<PgVectorStoreAdapter> _logger;

    private const string TableName = "pgvector_embeddings";

    public PgVectorStoreAdapter(
        MeepleAiDbContext context,
        ILogger<PgVectorStoreAdapter> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<Embedding>> SearchAsync(
        Guid gameId,
        DomainVector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken).ConfigureAwait(false);

        // Build SQL with cosine distance operator.
        // pgvector <=> returns cosine distance (0 = identical, 2 = opposite).
        // Similarity = 1 - distance.
        var sql = $"""
            SELECT id, vector_document_id, text_content, model, chunk_index, page_number,
                   1 - (vector <=> @queryVector) AS similarity
            FROM {TableName}
            WHERE game_id = @gameId
              AND 1 - (vector <=> @queryVector) >= @minScore
            """;

        if (documentIds is { Count: > 0 })
        {
            sql += "\n  AND vector_document_id = ANY(@documentIds)";
        }

        sql += $"""

            ORDER BY vector <=> @queryVector
            LIMIT @topK
            """;

        var command = (NpgsqlCommand)connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = sql;

            var pgVector = new Pgvector.Vector(queryVector.Values);
            command.Parameters.AddWithValue("@queryVector", pgVector);
            command.Parameters.AddWithValue("@gameId", gameId);
            command.Parameters.AddWithValue("@minScore", minScore);
            command.Parameters.AddWithValue("@topK", topK);

            if (documentIds is { Count: > 0 })
            {
                command.Parameters.AddWithValue("@documentIds", documentIds.ToArray());
            }

            var results = new List<Embedding>();

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = reader.GetGuid(0);
                    var vectorDocumentId = reader.GetGuid(1);
                    var textContent = reader.GetString(2);
                    var model = reader.GetString(3);
                    var chunkIndex = reader.GetInt32(4);
                    var pageNumber = reader.GetInt32(5);

                    // Use placeholder vector for search results (actual vector not needed by callers)
                    var placeholderVector = DomainVector.CreatePlaceholder(queryVector.Dimensions);
                    var embedding = new Embedding(
                        id: id,
                        vectorDocumentId: vectorDocumentId,
                        textContent: textContent,
                        vector: placeholderVector,
                        model: model,
                        chunkIndex: chunkIndex,
                        pageNumber: Math.Max(1, pageNumber));

                    results.Add(embedding);
                }
            }

            _logger.LogInformation(
                "pgvector search returned {ResultCount} results for gameId={GameId} (minScore={MinScore}, topK={TopK})",
                results.Count, gameId, minScore, topK);

            return results;
        }
    }

    /// <inheritdoc/>
    public async Task<List<Embedding>> SearchByMultipleGameIdsAsync(
        IReadOnlyList<Guid> gameIds,
        DomainVector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default)
    {
        if (gameIds is not { Count: > 0 })
            return new List<Embedding>();

        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken).ConfigureAwait(false);

        // Build SQL with IN clause for multiple game_ids
        var sql = $"""
            SELECT id, vector_document_id, text_content, model, chunk_index, page_number,
                   1 - (vector <=> @queryVector) AS similarity
            FROM {TableName}
            WHERE game_id = ANY(@gameIds)
              AND 1 - (vector <=> @queryVector) >= @minScore
            """;

        if (documentIds is { Count: > 0 })
        {
            sql += "\n  AND vector_document_id = ANY(@documentIds)";
        }

        sql += $"""

            ORDER BY vector <=> @queryVector
            LIMIT @topK
            """;

        var command = (NpgsqlCommand)connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = sql;

            var pgVector = new Pgvector.Vector(queryVector.Values);
            command.Parameters.AddWithValue("@queryVector", pgVector);
            command.Parameters.AddWithValue("@gameIds", gameIds.ToArray());
            command.Parameters.AddWithValue("@minScore", minScore);
            command.Parameters.AddWithValue("@topK", topK);

            if (documentIds is { Count: > 0 })
            {
                command.Parameters.AddWithValue("@documentIds", documentIds.ToArray());
            }

            var results = new List<Embedding>();

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = reader.GetGuid(0);
                    var vectorDocumentId = reader.GetGuid(1);
                    var textContent = reader.GetString(2);
                    var model = reader.GetString(3);
                    var chunkIndex = reader.GetInt32(4);
                    var pageNumber = reader.GetInt32(5);

                    var placeholderVector = DomainVector.CreatePlaceholder(queryVector.Dimensions);
                    var embedding = new Embedding(
                        id: id,
                        vectorDocumentId: vectorDocumentId,
                        textContent: textContent,
                        vector: placeholderVector,
                        model: model,
                        chunkIndex: chunkIndex,
                        pageNumber: Math.Max(1, pageNumber));

                    results.Add(embedding);
                }
            }

            _logger.LogInformation(
                "pgvector multi-game search returned {ResultCount} results for gameIds=[{GameIds}] (minScore={MinScore}, topK={TopK})",
                results.Count, string.Join(",", gameIds), minScore, topK);

            return results;
        }
    }

    public async Task IndexBatchAsync(
        List<Embedding> embeddings,
        CancellationToken cancellationToken = default)
    {
        if (embeddings is not { Count: > 0 })
            return;

        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken).ConfigureAwait(false);

        // Resolve game_id from VectorDocuments table via the embedding's VectorDocumentId.
        // All embeddings in a batch typically share the same VectorDocumentId.
        var vectorDocumentIds = embeddings.Select(e => e.VectorDocumentId).Distinct().ToList();
        var gameIdLookup = new Dictionary<Guid, Guid>();

        var lookupCmd = (NpgsqlCommand)connection.CreateCommand();
        await using (lookupCmd.ConfigureAwait(false))
        {
            lookupCmd.CommandText = "SELECT \"Id\", \"GameId\" FROM \"VectorDocuments\" WHERE \"Id\" = ANY(@ids)";
            lookupCmd.Parameters.AddWithValue("@ids", vectorDocumentIds.ToArray());

            var reader = await lookupCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    gameIdLookup[reader.GetGuid(0)] = reader.GetGuid(1);
                }
            }
        }

        // Use COPY for efficient bulk insert via Npgsql binary import
        var npgsqlConnection = (NpgsqlConnection)connection;

        var writer = await npgsqlConnection.BeginBinaryImportAsync(
            $"COPY {TableName} (id, vector_document_id, game_id, text_content, vector, model, chunk_index, page_number, created_at) FROM STDIN (FORMAT BINARY)",
            cancellationToken).ConfigureAwait(false);
        await using (writer.ConfigureAwait(false))
        {
            foreach (var embedding in embeddings)
            {
                var gameId = gameIdLookup.GetValueOrDefault(embedding.VectorDocumentId);
                if (gameId == Guid.Empty)
                {
                    _logger.LogWarning(
                        "No GameId found for VectorDocumentId={VectorDocumentId}, skipping embedding {EmbeddingId}",
                        embedding.VectorDocumentId, embedding.Id);
                    continue;
                }

                await writer.StartRowAsync(cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(embedding.Id, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(embedding.VectorDocumentId, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(gameId, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(embedding.TextContent, NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(new Pgvector.Vector(embedding.Vector.Values), cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(embedding.Model, NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(embedding.ChunkIndex, NpgsqlDbType.Integer, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(embedding.PageNumber, NpgsqlDbType.Integer, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(embedding.CreatedAt, NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
            }

            await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Indexed {Count} embeddings into pgvector for vectorDocumentId={VectorDocumentId}",
            embeddings.Count, embeddings[0].VectorDocumentId);
    }

    public async Task DeleteByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var command = (NpgsqlCommand)connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = $"DELETE FROM {TableName} WHERE vector_document_id = @vectorDocumentId";
            command.Parameters.AddWithValue("@vectorDocumentId", vectorDocumentId);

            var deleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Deleted {DeletedCount} embeddings for vectorDocumentId={VectorDocumentId} from pgvector",
                deleted, vectorDocumentId);
        }
    }

    public async Task<bool> CollectionExistsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var command = (NpgsqlCommand)connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            // Check if the table exists AND has data for this gameId.
            // In pgvector mode, all games share one table — "collection exists" means
            // the table is present and has at least one embedding for the game.
            command.CommandText = $"""
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = @tableName
                )
                AND EXISTS (
                    SELECT 1 FROM {TableName}
                    WHERE game_id = @gameId
                    LIMIT 1
                )
                """;
            command.Parameters.AddWithValue("@tableName", TableName);
            command.Parameters.AddWithValue("@gameId", gameId);

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is bool exists && exists;
        }
    }

    public async Task EnsureCollectionExistsAsync(
        Guid gameId,
        int vectorDimension = 1024,
        CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken).ConfigureAwait(false);

        // Enable pgvector extension
        var extCmd = (NpgsqlCommand)connection.CreateCommand();
        await using (extCmd.ConfigureAwait(false))
        {
            extCmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector";
            await extCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Create table
        var tableCmd = (NpgsqlCommand)connection.CreateCommand();
        await using (tableCmd.ConfigureAwait(false))
        {
            tableCmd.CommandText = $"""
                CREATE TABLE IF NOT EXISTS {TableName} (
                    id UUID PRIMARY KEY,
                    vector_document_id UUID NOT NULL,
                    game_id UUID NOT NULL,
                    text_content TEXT NOT NULL,
                    vector vector({vectorDimension}) NOT NULL,
                    model TEXT NOT NULL,
                    chunk_index INTEGER NOT NULL,
                    page_number INTEGER NOT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                )
                """;
            await tableCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Create HNSW index for cosine distance
        var indexCmd = (NpgsqlCommand)connection.CreateCommand();
        await using (indexCmd.ConfigureAwait(false))
        {
            indexCmd.CommandText = $"""
                CREATE INDEX IF NOT EXISTS idx_{TableName}_vector_cosine
                ON {TableName}
                USING hnsw (vector vector_cosine_ops)
                WITH (m = 16, ef_construction = 200)
                """;
            await indexCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Create index on game_id for filtering
        var gameIdxCmd = (NpgsqlCommand)connection.CreateCommand();
        await using (gameIdxCmd.ConfigureAwait(false))
        {
            gameIdxCmd.CommandText = $"""
                CREATE INDEX IF NOT EXISTS idx_{TableName}_game_id
                ON {TableName} (game_id)
                """;
            await gameIdxCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Create index on vector_document_id for deletion
        var docIdxCmd = (NpgsqlCommand)connection.CreateCommand();
        await using (docIdxCmd.ConfigureAwait(false))
        {
            docIdxCmd.CommandText = $"""
                CREATE INDEX IF NOT EXISTS idx_{TableName}_vector_document_id
                ON {TableName} (vector_document_id)
                """;
            await docIdxCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Ensured pgvector_embeddings table exists with HNSW index (dimension={Dimension})",
            vectorDimension);
    }

    private static async Task EnsureConnectionOpenAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

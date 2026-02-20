using System.Globalization;
using Api.Filters;
using Api.Services;
using Qdrant.Client.Grpc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Qdrant vector database operations.
/// Issue #4877: Qdrant Advanced Operations + Delete
/// </summary>
internal static class AdminQdrantEndpoints
{
    public static RouteGroupBuilder MapAdminQdrantEndpoints(this RouteGroupBuilder group)
    {
        var qdrantGroup = group.MapGroup("/admin/qdrant")
            .WithTags("Admin", "Qdrant")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/qdrant/collections/{name} - Detailed collection info
        qdrantGroup.MapGet("/collections/{name}", async (
            string name,
            IQdrantClientAdapter qdrantClient,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                var info = await qdrantClient.GetCollectionInfoAsync(name, ct).ConfigureAwait(false);
                var count = await qdrantClient.CountAsync(name, cancellationToken: ct).ConfigureAwait(false);

                return Results.Ok(new
                {
                    name,
                    pointsCount = (long)info.PointsCount,
                    indexedVectorsCount = (long)info.IndexedVectorsCount,
                    status = info.Status.ToString(),
                    config = new
                    {
                        vectorSize = info.Config?.Params?.VectorsConfig?.Params?.Size,
                        distance = info.Config?.Params?.VectorsConfig?.Params?.Distance.ToString(),
                    },
                    exactCount = (long)count,
                    health = info.PointsCount > 0
                        ? (int)Math.Min(100, ((long)info.IndexedVectorsCount * 100) / Math.Max(1, (long)info.PointsCount))
                        : 0,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get collection info for {Name}", name);
                return Results.NotFound(new { error = $"Collection '{name}' not found or unavailable" });
            }
        });

        // DELETE /api/v1/admin/qdrant/collections/{name}?confirmed=true
        qdrantGroup.MapDelete("/collections/{name}", async (
            string name,
            bool? confirmed,
            IQdrantClientAdapter qdrantClient,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (confirmed != true)
            {
                return Results.BadRequest(new { error = "Must pass confirmed=true to delete a collection" });
            }

            try
            {
                await qdrantClient.DeleteCollectionAsync(name, ct).ConfigureAwait(false);
                logger.LogWarning("Admin deleted Qdrant collection {CollectionName}", name);
                return Results.Ok(new { deleted = true, collection = name });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete collection {Name}", name);
                return Results.Problem($"Failed to delete collection: {ex.Message}");
            }
        });

        // POST /api/v1/admin/qdrant/collections/{name}/search - Test semantic search
        qdrantGroup.MapPost("/collections/{name}/search", async (
            string name,
            AdminSearchRequest request,
            IQdrantClientAdapter qdrantClient,
            IEmbeddingService embeddingService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                // Embed the query text
                var embeddingResult = await embeddingService.GenerateEmbeddingAsync(request.Query, ct)
                    .ConfigureAwait(false);

                if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
                {
                    return Results.BadRequest(new { error = "Failed to generate embedding for query" });
                }

                var queryVector = embeddingResult.Embeddings[0];

                // Build optional filter
                Filter? filter = null;
                if (!string.IsNullOrEmpty(request.GameId))
                {
                    filter = new Filter
                    {
                        Must = { new Condition { Field = new FieldCondition
                        {
                            Key = "game_id",
                            Match = new Match { Keyword = request.GameId },
                        }}},
                    };
                }

                var results = await qdrantClient.SearchAsync(
                    name,
                    queryVector,
                    filter,
                    (ulong)(request.Limit ?? 10),
                    ct).ConfigureAwait(false);

                var items = results.Select(r => new
                {
                    id = r.Id?.Num ?? 0,
                    score = r.Score,
                    payload = r.Payload?.ToDictionary(
                        p => p.Key,
                        p => p.Value?.StringValue ?? p.Value?.IntegerValue.ToString(CultureInfo.InvariantCulture) ?? "",
                        StringComparer.Ordinal),
                }).ToList();

                return Results.Ok(new { query = request.Query, results = items, total = items.Count });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Admin search failed for collection {Name}", name);
                return Results.Problem($"Search failed: {ex.Message}");
            }
        });

        // GET /api/v1/admin/qdrant/collections/{name}/points?limit=20
        qdrantGroup.MapGet("/collections/{name}/points", async (
            string name,
            int? limit,
            IQdrantClientAdapter qdrantClient,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                var points = await qdrantClient.ScrollPointsAsync(
                    name,
                    limit: (uint)(limit ?? 20),
                    cancellationToken: ct).ConfigureAwait(false);

                var items = points.Select(p => new
                {
                    id = p.Id?.Num ?? 0,
                    payload = p.Payload?.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value?.StringValue ?? kv.Value?.IntegerValue.ToString(CultureInfo.InvariantCulture) ?? "",
                        StringComparer.Ordinal),
                }).ToList();

                return Results.Ok(new { points = items, count = items.Count });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to scroll points for collection {Name}", name);
                return Results.Problem($"Failed to browse vectors: {ex.Message}");
            }
        });

        // DELETE /api/v1/admin/qdrant/collections/{name}/points - Delete by filter
        qdrantGroup.MapDelete("/collections/{name}/points", async (
            string name,
            string? gameId,
            string? pdfId,
            bool? confirmed,
            IQdrantClientAdapter qdrantClient,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (confirmed != true)
            {
                return Results.BadRequest(new { error = "Must pass confirmed=true to delete vectors" });
            }

            if (string.IsNullOrEmpty(gameId) && string.IsNullOrEmpty(pdfId))
            {
                return Results.BadRequest(new { error = "Must specify gameId or pdfId filter" });
            }

            try
            {
                var conditions = new List<Condition>();
                if (!string.IsNullOrEmpty(gameId))
                {
                    conditions.Add(new Condition { Field = new FieldCondition
                    {
                        Key = "game_id",
                        Match = new Match { Keyword = gameId },
                    }});
                }
                if (!string.IsNullOrEmpty(pdfId))
                {
                    conditions.Add(new Condition { Field = new FieldCondition
                    {
                        Key = "pdf_id",
                        Match = new Match { Keyword = pdfId },
                    }});
                }

                var filter = new Filter { Must = { conditions } };

                // Count before delete
                var countBefore = await qdrantClient.CountAsync(name, filter, ct).ConfigureAwait(false);

                await qdrantClient.DeleteAsync(name, filter, ct).ConfigureAwait(false);
                logger.LogWarning(
                    "Admin deleted {Count} vectors from {Collection} (gameId={GameId}, pdfId={PdfId})",
                    countBefore, name, gameId, pdfId);

                return Results.Ok(new { deleted = (long)countBefore, collection = name, gameId, pdfId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete vectors from {Name}", name);
                return Results.Problem($"Failed to delete vectors: {ex.Message}");
            }
        });

        // POST /api/v1/admin/qdrant/collections/{name}/rebuild - Rebuild index
        qdrantGroup.MapPost("/collections/{name}/rebuild", async (
            string name,
            bool? confirmed,
            IQdrantClientAdapter qdrantClient,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (confirmed != true)
            {
                return Results.BadRequest(new { error = "Must pass confirmed=true to rebuild index" });
            }

            try
            {
                var info = await qdrantClient.GetCollectionInfoAsync(name, ct).ConfigureAwait(false);

                // Re-apply existing HNSW config to trigger reindexing
                var hnswConfig = info.Config?.HnswConfig;
                if (hnswConfig != null)
                {
                    await qdrantClient.UpdateCollectionConfigAsync(
                        name,
                        hnswConfig: hnswConfig,
                        cancellationToken: ct).ConfigureAwait(false);
                }

                logger.LogInformation("Rebuild triggered for collection {Name}", name);
                return Results.Ok(new { rebuilding = true, collection = name });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to rebuild collection {Name}", name);
                return Results.Problem($"Failed to rebuild: {ex.Message}");
            }
        });

        return group;
    }
}

internal record AdminSearchRequest(string Query, int? Limit = 10, string? GameId = null);

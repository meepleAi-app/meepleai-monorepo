using Grpc.Core;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;

namespace Api.Services.Qdrant;

/// <summary>
/// Handles vector search operations in Qdrant
/// </summary>
public class QdrantVectorSearcher : IQdrantVectorSearcher
{
    private readonly IQdrantClientAdapter _clientAdapter;
    private readonly ILogger<QdrantVectorSearcher> _logger;

    public QdrantVectorSearcher(
        IQdrantClientAdapter clientAdapter,
        ILogger<QdrantVectorSearcher> logger)
    {
        _clientAdapter = clientAdapter;
        _logger = logger;
    }

    /// <summary>
    /// Search for similar vectors using the query embedding and filter
    /// </summary>
    public async Task<List<ScoredPoint>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        Filter? filter = null,
        int limit = 5,
        CancellationToken ct = default)
    {
        try
        {
            var searchResults = await _clientAdapter.SearchAsync(
                collectionName: collectionName,
                vector: queryEmbedding,
                filter: filter,
                limit: (ulong)limit,
                cancellationToken: ct
            );

            _logger.LogDebug("Search in collection {CollectionName} returned {Count} results",
                collectionName, searchResults.Count);

            return searchResults.ToList();
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "Null argument during search in collection {CollectionName}", collectionName);
            throw;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument during search in collection {CollectionName}", collectionName);
            throw;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC error during search in collection {CollectionName}: {Status}",
                collectionName, ex.Status);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during search in collection {CollectionName}", collectionName);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Operation cancelled during search in collection {CollectionName}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Convert Qdrant search results to domain model
    /// </summary>
    public List<SearchResultItem> ConvertToSearchResults(IEnumerable<ScoredPoint> scoredPoints)
    {
        return scoredPoints.Select(r => new SearchResultItem
        {
            Score = r.Score,
            Text = r.Payload["text"].StringValue,
            PdfId = r.Payload.ContainsKey("pdf_id") ? r.Payload["pdf_id"].StringValue : "",
            Page = (int)r.Payload["page"].IntegerValue,
            ChunkIndex = (int)r.Payload["chunk_index"].IntegerValue
        }).ToList();
    }

    /// <summary>
    /// Build a filter for game ID
    /// </summary>
    public Filter BuildGameFilter(string gameId)
    {
        return new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "game_id",
                        Match = new Match { Keyword = gameId }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Build a filter for game ID and language
    /// </summary>
    public Filter BuildGameLanguageFilter(string gameId, string language)
    {
        return new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "game_id",
                        Match = new Match { Keyword = gameId }
                    }
                },
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "language",
                        Match = new Match { Keyword = language }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Build a filter for category
    /// </summary>
    public Filter BuildCategoryFilter(string category)
    {
        return new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "category",
                        Match = new Match { Keyword = category }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Build a filter for PDF ID
    /// </summary>
    public Filter BuildPdfFilter(string pdfId)
    {
        return new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "pdf_id",
                        Match = new Match { Keyword = pdfId }
                    }
                }
            }
        };
    }
}

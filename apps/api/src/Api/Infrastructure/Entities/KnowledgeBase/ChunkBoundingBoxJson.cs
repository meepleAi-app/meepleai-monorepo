using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

// Namespace matches TextChunkEntity (Api.Infrastructure.Entities, not the folder) so the helper is
// in scope wherever the entity is constructed, without an extra using at the 4 insert sites.
namespace Api.Infrastructure.Entities;

/// <summary>
/// SP-B (#3406, epic #3403): serializes a chunk's normalized region to the
/// <c>text_chunks.bounding_boxes_json</c> column. Format is a JSON array of page-aware boxes
/// <c>[{page,x,y,width,height}]</c> — MVP emits 0 or 1 box per chunk (the union computed upstream by
/// <c>ExtractedDocumentFactory</c>); the array shape leaves room for future multi-region chunks.
/// Returns <c>null</c> when the chunk carries no region, so the column stays NULL for the
/// pre-coordinate corpus (backfilled by SP-E re-extraction).
/// </summary>
internal static class ChunkBoundingBoxJson
{
    public static string? Serialize(BoundingBox? bbox, int page)
    {
        if (bbox is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(new[]
        {
            new { page, x = bbox.X, y = bbox.Y, width = bbox.Width, height = bbox.Height },
        });
    }
}

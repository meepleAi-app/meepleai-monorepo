using System.Collections.Generic;

namespace Api.Services;

internal interface ITextChunkingService
{
    List<TextChunk> ChunkText(string text, int chunkSize = 1024, int overlap = 150);

    List<DocumentChunkInput> PrepareForEmbedding(string text, int chunkSize = 1024, int overlap = 150);
}

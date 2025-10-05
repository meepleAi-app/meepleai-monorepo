using System.Collections.Generic;

namespace Api.Services;

public interface ITextChunkingService
{
    List<TextChunk> ChunkText(string text, int chunkSize = 512, int overlap = 50);

    List<DocumentChunkInput> PrepareForEmbedding(string text, int chunkSize = 512, int overlap = 50);
}

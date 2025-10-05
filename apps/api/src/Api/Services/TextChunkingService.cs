namespace Api.Services;

/// <summary>
/// Service for chunking text documents into smaller segments for embedding
/// </summary>
public class TextChunkingService : ITextChunkingService
{
    private readonly ILogger<TextChunkingService> _logger;
    private const int DefaultChunkSize = 512; // characters
    private const int DefaultOverlap = 50; // characters overlap between chunks

    public TextChunkingService(ILogger<TextChunkingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Split text into overlapping chunks
    /// </summary>
    public List<TextChunk> ChunkText(
        string text,
        int chunkSize = DefaultChunkSize,
        int overlap = DefaultOverlap)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<TextChunk>();
        }

        var chunks = new List<TextChunk>();
        var textLength = text.Length;
        var currentPosition = 0;
        var chunkIndex = 0;

        while (currentPosition < textLength)
        {
            var remainingLength = textLength - currentPosition;
            var actualChunkSize = Math.Min(chunkSize, remainingLength);

            // Try to break at sentence or word boundary if possible
            var chunkEnd = currentPosition + actualChunkSize;
            if (chunkEnd < textLength && actualChunkSize == chunkSize)
            {
                // Look for sentence boundary (. ! ?)
                var sentenceEnd = FindSentenceBoundary(text, currentPosition, chunkEnd);
                if (sentenceEnd > currentPosition)
                {
                    chunkEnd = sentenceEnd;
                }
                else
                {
                    // Fall back to word boundary
                    var wordEnd = FindWordBoundary(text, currentPosition, chunkEnd);
                    if (wordEnd > currentPosition)
                    {
                        chunkEnd = wordEnd;
                    }
                }
            }

            var chunkText = text.Substring(currentPosition, chunkEnd - currentPosition).Trim();

            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                chunks.Add(new TextChunk
                {
                    Text = chunkText,
                    Index = chunkIndex,
                    CharStart = currentPosition,
                    CharEnd = chunkEnd,
                    Page = EstimatePageNumber(currentPosition, textLength)
                });

                chunkIndex++;
            }

            // Move position forward, accounting for overlap
            currentPosition = chunkEnd;
            if (currentPosition < textLength)
            {
                currentPosition = Math.Max(currentPosition - overlap, currentPosition);
            }
        }

        _logger.LogInformation("Chunked {TextLength} characters into {ChunkCount} chunks", textLength, chunks.Count);
        return chunks;
    }

    /// <summary>
    /// Find the nearest sentence boundary (. ! ? followed by space/newline)
    /// </summary>
    private int FindSentenceBoundary(string text, int start, int end)
    {
        // Look backward from end for sentence terminators
        for (int i = end - 1; i > start; i--)
        {
            var c = text[i];
            if ((c == '.' || c == '!' || c == '?') && i + 1 < text.Length)
            {
                var next = text[i + 1];
                if (char.IsWhiteSpace(next))
                {
                    return i + 1; // Include the punctuation
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Find the nearest word boundary (whitespace)
    /// </summary>
    private int FindWordBoundary(string text, int start, int end)
    {
        // Look backward from end for whitespace
        for (int i = end - 1; i > start; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i + 1; // Return position after whitespace
            }
        }

        return -1;
    }

    /// <summary>
    /// Estimate page number based on character position
    /// Assumes ~2000 characters per page (rough estimate)
    /// </summary>
    private int EstimatePageNumber(int charPosition, int totalLength)
    {
        const int charsPerPage = 2000;
        return (charPosition / charsPerPage) + 1;
    }

    /// <summary>
    /// Chunk text and prepare for embedding (returns chunks without embeddings)
    /// </summary>
    public List<DocumentChunkInput> PrepareForEmbedding(string text, int chunkSize = DefaultChunkSize, int overlap = DefaultOverlap)
    {
        var textChunks = ChunkText(text, chunkSize, overlap);

        return textChunks.Select(chunk => new DocumentChunkInput
        {
            Text = chunk.Text,
            Page = chunk.Page,
            CharStart = chunk.CharStart,
            CharEnd = chunk.CharEnd
        }).ToList();
    }
}

/// <summary>
/// Text chunk with metadata (no embedding yet)
/// </summary>
public record TextChunk
{
    public string Text { get; init; } = string.Empty;
    public int Index { get; init; }
    public int CharStart { get; init; }
    public int CharEnd { get; init; }
    public int Page { get; init; }
}

/// <summary>
/// Document chunk input ready for embedding
/// </summary>
public record DocumentChunkInput
{
    public string Text { get; init; } = string.Empty;
    public int Page { get; init; }
    public int CharStart { get; init; }
    public int CharEnd { get; init; }
}

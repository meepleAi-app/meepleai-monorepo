using Api.Constants;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Service for chunking text documents into smaller segments for embedding
/// </summary>
public class TextChunkingService : ITextChunkingService
{
    private readonly ILogger<TextChunkingService> _logger;
    private const int DefaultChunkSize = ChunkingConstants.DefaultChunkSize;
    private const int DefaultOverlap = ChunkingConstants.DefaultChunkOverlap;

    // PERF-07: Adaptive chunking parameters
    private const int MinChunkSize = ChunkingConstants.MinChunkSize;
    private const int MaxChunkSize = ChunkingConstants.MaxChunkSize;

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

            // PERF-07: Adaptive chunking - try to find natural break points
            var chunkEnd = currentPosition + actualChunkSize;
            if (chunkEnd < textLength && actualChunkSize == chunkSize)
            {
                // Priority 1: Look for paragraph break (double newline) - strongest semantic boundary
                var paragraphEnd = FindParagraphBoundary(text, currentPosition, Math.Min(currentPosition + MaxChunkSize, textLength));
                if (paragraphEnd > currentPosition && paragraphEnd >= currentPosition + MinChunkSize)
                {
                    chunkEnd = paragraphEnd;
                }
                // Priority 2: Look for sentence boundary within chunk
                else
                {
                    var sentenceEnd = FindSentenceBoundary(text, currentPosition, chunkEnd);
                    if (sentenceEnd > currentPosition)
                    {
                        chunkEnd = sentenceEnd;
                    }
                    // Priority 3: Extend chunk if we can fit complete sentence within MaxChunkSize
                    else if (chunkEnd < textLength)
                    {
                        var extendedEnd = Math.Min(currentPosition + MaxChunkSize, textLength);
                        sentenceEnd = FindSentenceBoundary(text, chunkEnd, extendedEnd);
                        if (sentenceEnd > chunkEnd)
                        {
                            chunkEnd = sentenceEnd; // Extend to complete the sentence
                        }
                        else
                        {
                            // Priority 4: Fall back to word boundary
                            var wordEnd = FindWordBoundary(text, currentPosition, chunkEnd);
                            if (wordEnd > currentPosition)
                            {
                                chunkEnd = wordEnd;
                            }
                        }
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
                currentPosition = Math.Max(0, currentPosition - overlap);
            }
        }

        _logger.LogInformation("Chunked {TextLength} characters into {ChunkCount} chunks", textLength, chunks.Count);
        return chunks;
    }

    /// <summary>
    /// PERF-07: Find paragraph boundary (double newline or section markers)
    /// Paragraphs are the strongest semantic boundaries for chunking
    /// </summary>
    private int FindParagraphBoundary(string text, int start, int end)
    {
        // Look for double newline (paragraph break)
        for (int i = start; i < end - 1; i++)
        {
            if (text[i] == '\n' && i + 1 < end && text[i + 1] == '\n')
            {
                // Skip the double newline and any additional whitespace
                int boundaryEnd = i + 2;
                while (boundaryEnd < end && char.IsWhiteSpace(text[boundaryEnd]))
                {
                    boundaryEnd++;
                }
                return boundaryEnd;
            }
            // Also check for \r\n\r\n (Windows line endings)
            if (text[i] == '\r' && i + 3 < end &&
                text[i + 1] == '\n' && text[i + 2] == '\r' && text[i + 3] == '\n')
            {
                int boundaryEnd = i + 4;
                while (boundaryEnd < end && char.IsWhiteSpace(text[boundaryEnd]))
                {
                    boundaryEnd++;
                }
                return boundaryEnd;
            }
        }

        return -1; // No paragraph boundary found
    }

    /// <summary>
    /// Find the nearest sentence boundary (. ! ? followed by space/newline)
    /// PERF-07: Enhanced sentence detection with abbreviation and decimal handling
    /// </summary>
    private int FindSentenceBoundary(string text, int start, int end)
    {
        // Common abbreviations that don't end sentences
        var commonAbbreviations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mr", "mrs", "ms", "dr", "prof", "sr", "jr",
            "inc", "ltd", "corp", "co", "etc", "vs", "e.g", "i.e",
            "pg", "pp", "vol", "fig", "no", "approx"
        };

        // Look backward from end for sentence terminators
        for (int i = end - 1; i > start; i--)
        {
            var c = text[i];
            if ((c == '.' || c == '!' || c == '?') && i + 1 < text.Length)
            {
                var next = text[i + 1];

                // Check if this is likely a sentence boundary
                if (char.IsWhiteSpace(next) || next == '\n' || next == '\r')
                {
                    // PERF-07: Check for false positives (abbreviations, decimals, etc.)
                    if (c == '.')
                    {
                        // Check for decimal number (e.g., "3.5")
                        if (IsNumberDecimalPoint(text, i, start))
                        {
                            continue; // Skip decimal points
                        }

                        // Check for abbreviations (e.g., "Mr.", "Inc.")
                        var wordStart = FindWordStart(text, start, i);
                        if (wordStart >= 0 && wordStart < i)
                        {
                            var word = text.Substring(wordStart, i - wordStart).ToLowerInvariant();
                            if (commonAbbreviations.Contains(word))
                            {
                                continue; // Skip abbreviations
                            }
                        }

                        // Check for ellipsis (...)
                        if (i >= 2 && text[i - 1] == '.' && text[i - 2] == '.')
                        {
                            return i + 1; // End after ellipsis
                        }
                    }

                    // Check for newline after punctuation (paragraph break = strong sentence boundary)
                    if (i + 1 < text.Length && (text[i + 1] == '\n' || text[i + 1] == '\r'))
                    {
                        return i + 1; // Strong boundary at paragraph break
                    }

                    // Check for capital letter after punctuation (indicates new sentence)
                    if (i + 2 < text.Length && char.IsUpper(text[i + 2]))
                    {
                        return i + 1; // Likely sentence boundary
                    }

                    // Default: accept as sentence boundary
                    return i + 1;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// PERF-07: Find the start of the current word (for abbreviation detection)
    /// </summary>
    private int FindWordStart(string text, int searchStart, int position)
    {
        for (int i = position - 1; i >= searchStart; i--)
        {
            if (!char.IsLetter(text[i]))
            {
                return i + 1;
            }
        }
        return searchStart;
    }

    /// <summary>
    /// Checks if a period is part of a decimal number (e.g., "3.5").
    /// </summary>
    private static bool IsNumberDecimalPoint(string text, int position, int start)
    {
        return position > start
            && char.IsDigit(text[position - 1])
            && position + 1 < text.Length
            && char.IsDigit(text[position + 1]);
    }

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

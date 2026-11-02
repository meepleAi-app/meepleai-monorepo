using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// ADR-016 Phase 1: Advanced hierarchical chunking service implementation.
/// Creates parent/child chunk relationships using sentence-based baseline strategy.
/// </summary>
internal sealed class AdvancedChunkingService : IAdvancedChunkingService
{
    private readonly ITextChunkingService _textChunkingService;
    private readonly ChunkingStrategySelector _strategySelector;
    private readonly ILogger<AdvancedChunkingService> _logger;

    public AdvancedChunkingService(
        ITextChunkingService textChunkingService,
        ChunkingStrategySelector strategySelector,
        ILogger<AdvancedChunkingService> logger)
    {
        _textChunkingService = textChunkingService;
        _strategySelector = strategySelector;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<HierarchicalChunk>> ChunkDocumentAsync(
        ExtractedDocument document,
        ChunkingConfiguration? config = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(document);

        // Step 1: Auto-select strategy if not provided
        var elementTypes = document.Sections.Select(s => s.ElementType).ToList();
        config ??= _strategySelector.SelectStrategy(document.Content, elementTypes);

        _logger.LogInformation(
            "Chunking document {DocumentId} with strategy {Strategy} (size: {Size} tokens, overlap: {Overlap}%)",
            document.Id, config.Name, config.ChunkSizeTokens, config.OverlapPercentage * 100);

        // Step 2: Process sections if available, otherwise treat as single section
        List<HierarchicalChunk> chunks;
        if (document.Sections.Count > 0)
        {
            chunks = ProcessSections(document, config);
        }
        else
        {
            chunks = ProcessPlainText(document.Content, document.Id, document.GameId, config);
        }

        _logger.LogInformation(
            "Created {ChunkCount} chunks from document {DocumentId} ({ParentCount} parents, {ChildCount} children)",
            chunks.Count, document.Id,
            chunks.Count(c => c.IsRoot),
            chunks.Count(c => !c.IsRoot));

        return Task.FromResult(chunks);
    }

    /// <inheritdoc />
    public Task<List<HierarchicalChunk>> ChunkTextAsync(
        string text,
        Guid documentId,
        ChunkingConfiguration? config = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(new List<HierarchicalChunk>());

        config ??= _strategySelector.SelectStrategy(text);

        var chunks = ProcessPlainText(text, documentId, gameId: null, config);

        return Task.FromResult(chunks);
    }

    /// <summary>
    /// Processes document sections to create hierarchical chunks.
    /// Each section becomes a parent chunk, with child chunks for sentences.
    /// </summary>
    private List<HierarchicalChunk> ProcessSections(
        ExtractedDocument document,
        ChunkingConfiguration config)
    {
        var allChunks = new List<HierarchicalChunk>();

        foreach (var section in document.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Content))
                continue;

            // Create parent chunk for the section
            var parentMetadata = new ChunkMetadata
            {
                Page = PageOf(document.PageSpans, section.Content, 0, section.CharStart, section.Page),
                Heading = section.Heading,
                ElementType = section.ElementType,
                GameId = document.GameId,
                DocumentId = document.Id,
                CharStart = section.CharStart,
                CharEnd = section.CharEnd,
                BBox = section.BBox
            };

            var parentChunk = HierarchicalChunk.CreateParent(section.Content, parentMetadata);
            allChunks.Add(parentChunk);

            // Create child chunks using sentence-based splitting
            var childChunks = CreateChildChunks(
                section.Content,
                parentChunk.Id,
                document.Id,
                document.GameId,
                section.Page,
                section.Heading,
                section.CharStart,
                section.BBox,
                config,
                document.PageSpans);

            // Link children to parent
            foreach (var child in childChunks)
            {
                parentChunk.AddChild(child.Id);
            }

            allChunks.AddRange(childChunks);
        }

        return allChunks;
    }

    /// <summary>
    /// Processes plain text without section information.
    /// Creates synthetic sections based on paragraph breaks.
    /// Handles both Unix (\n\n) and Windows (\r\n\r\n) line endings correctly.
    /// </summary>
    private List<HierarchicalChunk> ProcessPlainText(
        string text,
        Guid documentId,
        Guid? gameId,
        ChunkingConfiguration config)
    {
        var allChunks = new List<HierarchicalChunk>();

        // Detect paragraph separator length (Windows \r\n\r\n = 4, Unix \n\n = 2)
        var paragraphSeparatorLength = text.Contains("\r\n\r\n") ? 4 : 2;

        // Split into paragraphs as synthetic sections
        var paragraphs = SplitIntoParagraphs(text);
        var charPosition = 0;

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                charPosition += paragraph.Length + paragraphSeparatorLength;
                continue;
            }

            // Find actual position in original text for accurate char tracking
            var actualStart = text.IndexOf(paragraph, charPosition, StringComparison.Ordinal);
            if (actualStart >= 0)
            {
                charPosition = actualStart;
            }

            var paragraphStart = charPosition;
            var paragraphEnd = charPosition + paragraph.Length;

            // Create parent chunk for the paragraph
            var parentMetadata = new ChunkMetadata
            {
                Page = (charPosition / 2000) + 1,
                Heading = null,
                ElementType = "text",
                GameId = gameId,
                DocumentId = documentId,
                CharStart = paragraphStart,
                CharEnd = paragraphEnd
            };

            var parentChunk = HierarchicalChunk.CreateParent(paragraph, parentMetadata);
            allChunks.Add(parentChunk);

            // Create child chunks if paragraph is larger than chunk size
            if (paragraph.Length > config.ChunkSizeChars)
            {
                var childChunks = CreateChildChunks(
                    paragraph,
                    parentChunk.Id,
                    documentId,
                    gameId,
                    parentMetadata.Page,
                    heading: null,
                    parentMetadata.CharStart,
                    baseBBox: null,
                    config: config);

                foreach (var child in childChunks)
                {
                    parentChunk.AddChild(child.Id);
                }

                allChunks.AddRange(childChunks);
            }

            charPosition = paragraphEnd + paragraphSeparatorLength;
        }

        return allChunks;
    }

    /// <summary>
    /// Creates child chunks from content using the underlying TextChunkingService.
    /// </summary>
    private List<HierarchicalChunk> CreateChildChunks(
        string content,
        string parentId,
        Guid documentId,
        Guid? gameId,
        int basePage,
        string? heading,
        int baseCharStart,
        BoundingBox? baseBBox,
        ChunkingConfiguration config,
        IReadOnlyList<PageSpan>? pageSpans = null)
    {
        var childChunks = new List<HierarchicalChunk>();

        // Use existing TextChunkingService for sentence-based chunking
        var textChunks = _textChunkingService.ChunkText(
            content,
            config.ChunkSizeChars,
            config.OverlapChars);

        foreach (var textChunk in textChunks)
        {
            var childMetadata = new ChunkMetadata
            {
                Page = PageOf(pageSpans, content, textChunk.CharStart, baseCharStart, basePage),
                Heading = heading,
                ElementType = "text",
                GameId = gameId,
                DocumentId = documentId,
                CharStart = baseCharStart + textChunk.CharStart,
                CharEnd = baseCharStart + textChunk.CharEnd,
                BBox = baseBBox  // SP-B (#3406): children inherit the parent section's region
            };

            var childChunk = HierarchicalChunk.CreateChild(
                textChunk.Text,
                level: 2, // Sentence level
                childMetadata,
                parentId);

            childChunks.Add(childChunk);
        }

        return childChunks;
    }

    /// <summary>
    /// The page of a chunk (#4782). With a page map (a flat document whose extractor measured its pages) it is the page
    /// that holds the first non-blank character of the chunk; without one it is <paramref name="fallbackPage"/>, the
    /// page of the section, which is how every chunk got its page before.
    /// </summary>
    /// <param name="pageSpans">The page map of the document, or null when there is none.</param>
    /// <param name="source">The text the chunk was cut from (the content of its section).</param>
    /// <param name="offsetInSource">Where the cut span begins in <paramref name="source"/>.</param>
    /// <param name="sourceStart">Where <paramref name="source"/> begins in the text of the document, which is what the map is over.</param>
    /// <param name="fallbackPage">The page to use when there is no map.</param>
    /// <remarks>
    /// The blanks are skipped in <paramref name="source"/>, not in the text of the chunk: TextChunkingService trims the
    /// text of a chunk but reports the start of the span before trimming, so a chunk cut inside the blank line between
    /// two pages has a start on the previous page and a text that no longer begins with a blank.
    /// </remarks>
    private static int PageOf(
        IReadOnlyList<PageSpan>? pageSpans, string source, int offsetInSource, int sourceStart, int fallbackPage)
    {
        if (pageSpans is null)
        {
            return fallbackPage;
        }

        var first = offsetInSource;
        while (first < source.Length && char.IsWhiteSpace(source[first]))
        {
            first++;
        }

        return PageSpans.PageAt(pageSpans, sourceStart + first);
    }

    private static readonly string[] ParagraphSeparators = { "\r\n\r\n", "\n\n" };

    /// <summary>
    /// Splits text into paragraphs based on double newlines.
    /// </summary>
    private static List<string> SplitIntoParagraphs(string text)
    {
        // Split on double newlines (paragraph breaks)
        var paragraphs = text.Split(
            ParagraphSeparators,
            StringSplitOptions.None);

        return paragraphs
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }
}


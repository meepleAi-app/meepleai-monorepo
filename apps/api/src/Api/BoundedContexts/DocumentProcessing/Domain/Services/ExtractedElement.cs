namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// A raw partition element from PDF extraction, preserving its structural category.
/// ElementType carries the raw Unstructured category ("Title"/"NarrativeText"/"Table"/…),
/// coalesced null/whitespace to "NarrativeText" (never null). Published contract: consumed
/// by KnowledgeBase's ExtractedDocumentFactory (SP1) to build heading-aware sections.
/// </summary>
public record ExtractedElement(
    string Text,
    int PageNumber,
    string ElementType);

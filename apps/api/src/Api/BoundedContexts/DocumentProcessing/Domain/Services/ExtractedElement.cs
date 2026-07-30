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
    string ElementType,
    ElementBoundingBox? BoundingBox = null);

/// <summary>
/// Normalized bounding box of a PDF element (SP-B #3406): [0,1] fractions of the page
/// layout, top-left origin (y grows downward), independent of strategy/DPI/zoom. Null when
/// the extractor emitted no coordinates. Defined here in DocumentProcessing (the published
/// extraction contract); KnowledgeBase maps it to its own BoundingBox VO — the dependency
/// arrow is KB → DocumentProcessing, never the reverse. Optional trailing param keeps the
/// record backward-compatible with StructuredElementsJson already persisted (member absent
/// deserializes to null).
/// </summary>
public sealed record ElementBoundingBox(float X, float Y, float Width, float Height);

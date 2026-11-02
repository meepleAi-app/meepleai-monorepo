using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// SP1: builds an <see cref="ExtractedDocument"/> with heading-aware <see cref="DocumentSection"/>s
/// by grouping raw extraction elements. A "Title" element opens a section (its text becomes the
/// heading); elements from the Title up to the next Title form the section content. Elements before
/// the first Title become a null-heading preamble. When no structured elements are available
/// (degradation: SmolDocling/Docnet/malformed response) a single preamble section carries the flat
/// text so the document content is never lost downstream. When the extractor measured its pages and a page
/// map fits the flat text (#4782), the flat text gets one section per page instead, so its chunks sit on their
/// true pages.
/// </summary>
internal static class ExtractedDocumentFactory
{
    private const string ElementSeparator = "\n\n";
    private const string TitleCategory = "Title";

    /// <summary>Builds the document the chunker works on (see the class summary).</summary>
    /// <param name="documentId">The document the sections belong to.</param>
    /// <param name="gameId">The game the document belongs to, if any.</param>
    /// <param name="structuredElements">The raw extraction elements; null or empty when the extractor gave none.</param>
    /// <param name="flatText">The extracted text, used when there are no structured elements.</param>
    /// <param name="pageSpans">
    /// Where each page of the PDF begins in <paramref name="flatText"/> (#4782). Used only on the flat path, and only
    /// when it fits the text (<see cref="PageSpans.IsConsistentWith"/>): the flat path then gets one section per
    /// page instead of a single section on page 1.
    /// </param>
    public static ExtractedDocument FromExtraction(
        Guid documentId,
        Guid? gameId,
        IReadOnlyList<ExtractedElement>? structuredElements,
        string flatText,
        IReadOnlyList<PageSpan>? pageSpans = null)
    {
        if (structuredElements is null || structuredElements.Count == 0)
        {
            flatText ??= string.Empty;
            return PageSpans.IsConsistentWith(pageSpans, flatText)
                ? PagedFlatDocument(documentId, gameId, flatText, pageSpans!)
                : NullPathDocument(documentId, gameId, flatText);
        }

        var sections = new List<DocumentSection>();
        var content = new System.Text.StringBuilder();
        var groups = GroupByTitle(structuredElements);

        foreach (var group in groups)
        {
            var sectionStart = content.Length;
            for (var i = 0; i < group.Elements.Count; i++)
            {
                if (i > 0) content.Append(ElementSeparator);
                content.Append(group.Elements[i].Text);
            }
            var sectionEnd = content.Length;

            sections.Add(new DocumentSection
            {
                Heading = group.Heading,
                Content = content.ToString(sectionStart, sectionEnd - sectionStart),
                Page = group.Elements[0].PageNumber,
                ElementType = NormalizeElementType(group.Elements[0].ElementType),
                CharStart = sectionStart,
                CharEnd = sectionEnd,
                BBox = ComputeSectionBBox(group.Elements),
            });

            // Inter-section separator: lands in the GAP between this section's CharEnd and the
            // next section's CharStart, so doc.Content is fully "\n\n"-separated (§6.2) while the
            // substring invariant still holds (each section.Content excludes the trailing seam).
            if (!ReferenceEquals(group, groups[^1]))
            {
                content.Append(ElementSeparator);
            }
        }

        return new ExtractedDocument
        {
            Id = documentId,
            GameId = gameId,
            Content = content.ToString(),
            Sections = sections,
            PageCount = structuredElements.Max(e => e.PageNumber),
        };
    }

    private static ExtractedDocument NullPathDocument(Guid documentId, Guid? gameId, string flatText)
    {
        return new ExtractedDocument
        {
            Id = documentId,
            GameId = gameId,
            Content = flatText,
            PageCount = 1,
            Sections = new List<DocumentSection>
            {
                new()
                {
                    Heading = null,
                    Content = flatText,
                    Page = 1,
                    ElementType = "text",
                    CharStart = 0,
                    CharEnd = flatText.Length,
                },
            },
        };
    }

    /// <summary>
    /// The flat text of a document whose pages were measured: one heading-less section per page, on that page. The
    /// chunker gives every chunk the page of its section, so this is what lets a flat document tell its pages apart;
    /// the single section of <see cref="NullPathDocument"/> puts every chunk on page 1. The sections keep the layout
    /// of the structured path: content is a substring of <c>Content</c> at [CharStart, CharEnd), and the separator
    /// between two pages sits in the gap.
    /// </summary>
    private static ExtractedDocument PagedFlatDocument(
        Guid documentId,
        Guid? gameId,
        string flatText,
        IReadOnlyList<PageSpan> pageSpans)
    {
        var sections = new List<DocumentSection>(pageSpans.Count);
        for (var i = 0; i < pageSpans.Count; i++)
        {
            var start = pageSpans[i].Start;
            var end = PageSpans.EndOf(pageSpans, i, flatText.Length);
            sections.Add(new DocumentSection
            {
                Heading = null,
                Content = flatText[start..end],
                Page = pageSpans[i].Page,
                ElementType = "text",
                CharStart = start,
                CharEnd = end,
            });
        }

        return new ExtractedDocument
        {
            Id = documentId,
            GameId = gameId,
            Content = flatText,
            Sections = sections,
            PageCount = pageSpans.Max(s => s.Page),
        };
    }

    private sealed record SectionGroup(string? Heading, List<ExtractedElement> Elements);

    private static List<SectionGroup> GroupByTitle(IReadOnlyList<ExtractedElement> elements)
    {
        var groups = new List<SectionGroup>();
        SectionGroup? current = null;

        foreach (var el in elements)
        {
            if (string.Equals(el.ElementType, TitleCategory, StringComparison.Ordinal))
            {
                current = new SectionGroup(el.Text, new List<ExtractedElement> { el });
                groups.Add(current);
            }
            else
            {
                if (current is null)
                {
                    current = new SectionGroup(null, new List<ExtractedElement>());
                    groups.Add(current);
                }
                current.Elements.Add(el);
            }
        }

        return groups;
    }

    private static string NormalizeElementType(string rawCategory) => rawCategory switch
    {
        "Title" => "heading",
        "Table" => "table",
        "ListItem" => "list",
        _ => "text",
    };

    /// <summary>
    /// SP-B (#3406): the section's region = union (min/max) of its elements' normalized boxes on the
    /// section start page. Elements without coordinates — or on later pages of a multi-page section —
    /// are skipped; returns null when none carry a box. Single box per section (MVP): multi-page
    /// sections are anchored to the start page. Maps the DocumentProcessing ElementBoundingBox to the
    /// KnowledgeBase BoundingBox VO (dependency arrow KB → DocumentProcessing).
    /// </summary>
    private static BoundingBox? ComputeSectionBBox(IReadOnlyList<ExtractedElement> elements)
    {
        var startPage = elements[0].PageNumber;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        var found = false;
        foreach (var el in elements)
        {
            if (el.PageNumber != startPage || el.BoundingBox is null)
            {
                continue;
            }
            var b = el.BoundingBox;
            found = true;
            if (b.X < minX) minX = b.X;
            if (b.Y < minY) minY = b.Y;
            if (b.X + b.Width > maxX) maxX = b.X + b.Width;
            if (b.Y + b.Height > maxY) maxY = b.Y + b.Height;
        }

        return found ? BoundingBox.FromCoordinates(minX, minY, maxX - minX, maxY - minY) : null;
    }
}

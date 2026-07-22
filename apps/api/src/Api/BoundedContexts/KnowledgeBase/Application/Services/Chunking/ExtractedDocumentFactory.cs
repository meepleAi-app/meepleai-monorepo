using Api.BoundedContexts.DocumentProcessing.Domain.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// SP1: builds an <see cref="ExtractedDocument"/> with heading-aware <see cref="DocumentSection"/>s
/// by grouping raw extraction elements. A "Title" element opens a section (its text becomes the
/// heading); elements from the Title up to the next Title form the section content. Elements before
/// the first Title become a null-heading preamble. When no structured elements are available
/// (degradation: SmolDocling/Docnet/malformed response) a single preamble section carries the flat
/// text so the document content is never lost downstream.
/// </summary>
internal static class ExtractedDocumentFactory
{
    private const string ElementSeparator = "\n\n";
    private const string TitleCategory = "Title";

    public static ExtractedDocument FromExtraction(
        Guid documentId,
        Guid? gameId,
        IReadOnlyList<ExtractedElement>? structuredElements,
        string flatText)
    {
        if (structuredElements is null || structuredElements.Count == 0)
        {
            return NullPathDocument(documentId, gameId, flatText ?? string.Empty);
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
}

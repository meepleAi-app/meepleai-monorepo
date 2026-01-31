using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Guards;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Number of pages in a PDF document.
/// Represents a valid page count (minimum 1).
/// </summary>
internal sealed class PageCount : ValueObject
{
    /// <summary>
    /// Number of pages (minimum 1)
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Creates a page count with the specified value.
    /// </summary>
    /// <param name="value">Number of pages (must be >= 1)</param>
    /// <exception cref="DomainException">Thrown if page count is invalid</exception>
    public PageCount(int value)
    {
        Guard.AgainstTooSmall(value, nameof(value), PageCountCategories.MinimumPageCount);
        Value = value;
    }

    /// <summary>
    /// Checks if the page count is within the specified limit.
    /// </summary>
    /// <param name="maxPages">Maximum allowed pages</param>
    /// <returns>True if page count &lt;= maxPages</returns>
    public bool IsWithinLimit(int maxPages)
    {
        if (maxPages < 1)
            throw new ValidationException("Maximum page limit must be at least 1");

        return Value <= maxPages;
    }

    /// <summary>
    /// Checks if this is a single-page document.
    /// </summary>
    /// <remarks>
    /// Single-page documents typically include quick reference cards, summary sheets, or cheat sheets.
    /// These documents have minimal processing overhead and can be handled with optimized workflows.
    /// </remarks>
    /// <example>
    /// <code>
    /// var pageCount = new PageCount(1);
    /// if (pageCount.IsSinglePage)
    ///     await ProcessQuickReferenceCardAsync(document);
    /// </code>
    /// </example>
    public bool IsSinglePage => Value == 1;

    /// <summary>
    /// Checks if this is a large PDF (business definition: &gt; 100 pages).
    /// </summary>
    /// <remarks>
    /// Large PDFs (&gt; 100 pages) represent comprehensive rulebooks or compendiums requiring
    /// extended processing time and chunked extraction strategies.
    /// Threshold set at 100 pages based on typical board game manual complexity and processing capacity.
    /// These documents may require multiple OCR passes and enhanced quality validation.
    /// </remarks>
    /// <example>
    /// <code>
    /// var pageCount = new PageCount(150);
    /// if (pageCount.IsLargePdf)
    ///     await ProcessWithChunkedExtractionAsync(document, chunkSize: 50);
    /// </code>
    /// </example>
    public bool IsLargePdf => Value > PageCountCategories.MediumPdfThreshold;

    /// <summary>
    /// Checks if this is a small PDF (business definition: &lt;= 10 pages).
    /// </summary>
    /// <remarks>
    /// Small PDFs (&lt;= 10 pages) represent concise rulebooks or game summaries.
    /// These documents can be processed quickly (&lt; 30 seconds) and benefit from synchronous workflows.
    /// Threshold based on user expectations for responsive upload feedback and processing time analysis.
    /// </remarks>
    /// <example>
    /// <code>
    /// var pageCount = new PageCount(8);
    /// if (pageCount.IsSmallPdf)
    ///     await ProcessSynchronouslyAsync(document);
    /// </code>
    /// </example>
    public bool IsSmallPdf => Value <= PageCountCategories.SmallPdfThreshold;

    /// <summary>
    /// Checks if this is a medium PDF (business definition: 11-100 pages).
    /// </summary>
    /// <remarks>
    /// Medium PDFs (11-100 pages) represent typical board game rulebooks with moderate complexity.
    /// These documents require asynchronous processing (30-180 seconds) to maintain responsive UX.
    /// This is the most common category for uploaded board game manuals based on BGG data analysis.
    /// </remarks>
    /// <example>
    /// <code>
    /// var pageCount = new PageCount(45);
    /// if (pageCount.IsMediumPdf)
    ///     await QueueAsyncProcessingAsync(document);
    /// </code>
    /// </example>
    public bool IsMediumPdf => Value > PageCountCategories.SmallPdfThreshold && Value <= PageCountCategories.MediumPdfThreshold;

    /// <summary>
    /// Returns the page count as a string.
    /// </summary>
    public override string ToString() => $"{Value} page(s)";

    /// <summary>
    /// Equality comparison based on page count value.
    /// </summary>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Implicit conversion to int for convenience.
    /// </summary>
    public static implicit operator int(PageCount pageCount) => pageCount.Value;

    /// <summary>
    /// Common page counts as static constants.
    /// </summary>
    public static readonly PageCount SinglePage = new(PageCountCategories.MinimumPageCount);
    public static readonly PageCount TwoPages = new(2);
}

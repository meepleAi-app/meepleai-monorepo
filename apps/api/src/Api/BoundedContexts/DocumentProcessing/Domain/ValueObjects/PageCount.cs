using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

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
        if (value < 1)
            throw new DomainException("Page count must be at least 1");

        Value = value;
    }

    /// <summary>
    /// Checks if the page count is within the specified limit.
    /// </summary>
    /// <param name="maxPages">Maximum allowed pages</param>
    /// <returns>True if page count <= maxPages</returns>
    public bool IsWithinLimit(int maxPages)
    {
        if (maxPages < 1)
            throw new ArgumentException("Maximum page limit must be at least 1", nameof(maxPages));

        return Value <= maxPages;
    }

    /// <summary>
    /// Checks if this is a single-page document.
    /// </summary>
    public bool IsSinglePage => Value == 1;

    /// <summary>
    /// Checks if this is a large PDF (business definition: > 100 pages).
    /// </summary>
    public bool IsLargePdf => Value > 100;

    /// <summary>
    /// Checks if this is a small PDF (business definition: <= 10 pages).
    /// </summary>
    public bool IsSmallPdf => Value <= 10;

    /// <summary>
    /// Checks if this is a medium PDF (business definition: 11-100 pages).
    /// </summary>
    public bool IsMediumPdf => Value > 10 && Value <= 100;

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
    public static readonly PageCount SinglePage = new(1);
    public static readonly PageCount TwoPages = new(2);

    public int ToInt32()
    {
        throw new NotImplementedException();
    }
}

using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated game title with normalization capabilities.
/// </summary>
public sealed class GameTitle : ValueObject
{
    private const int MaxLength = 200;
    private const int MinLength = 1;

    public string Value { get; }
    public string NormalizedValue { get; }
    public string Slug { get; }

    public GameTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("Game title cannot be empty");

        var trimmed = title.Trim();

        if (trimmed.Length < MinLength)
            throw new ValidationException($"Game title must be at least {MinLength} character");

        if (trimmed.Length > MaxLength)
            throw new ValidationException($"Game title cannot exceed {MaxLength} characters");

        Value = trimmed;
        NormalizedValue = Normalize(trimmed);
        Slug = GenerateSlug(trimmed);
    }

    /// <summary>
    /// Normalizes title for comparison (lowercase, trimmed, single spaces).
    /// </summary>
    private static string Normalize(string title)
    {
        // Remove extra whitespace
        var normalized = Regex.Replace(title, @"\s+", " ");
        return normalized.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Generates URL-friendly slug from title.
    /// </summary>
    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();

        // Replace spaces with hyphens
        slug = Regex.Replace(slug, @"\s+", "-");

        // Remove non-alphanumeric characters (except hyphens)
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");

        // Remove duplicate hyphens
        slug = Regex.Replace(slug, @"-+", "-");

        // Trim hyphens from start and end
        slug = slug.Trim('-');

        return slug;
    }

    /// <summary>
    /// Generates deterministic ID from title (for idempotent game creation).
    /// </summary>
    public Guid GenerateId()
    {
        // Use namespace UUID v5 with normalized title
        return GenerateGuidFromString(NormalizedValue);
    }

    private static Guid GenerateGuidFromString(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return NormalizedValue;
    }

    public override string ToString() => Value;

    public static implicit operator string(GameTitle title) => title.Value;
}

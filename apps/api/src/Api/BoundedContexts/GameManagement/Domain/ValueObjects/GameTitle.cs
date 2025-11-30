using System;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Validation;
using System.Security.Cryptography;
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
        Value = title
            .NotNullOrWhiteSpace(nameof(GameTitle), "Game title cannot be empty")
            .Then(t => t.Trim().MinLength(MinLength, nameof(GameTitle), $"Game title must be at least {MinLength} character"))
            .Then(t => t.MaxLength(MaxLength, nameof(GameTitle), $"Game title cannot exceed {MaxLength} characters"))
            .ThrowIfFailure(nameof(GameTitle));

        NormalizedValue = Normalize(Value);
        Slug = GenerateSlug(Value);
    }

    /// <summary>
    /// Normalizes title for comparison (lowercase, trimmed, single spaces).
    /// </summary>
    private static string Normalize(string title)
    {
        // Remove extra whitespace
        // FIX MA0009: Add timeout to prevent ReDoS attacks
        var normalized = Regex.Replace(title, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1));
        return normalized.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Generates URL-friendly slug from title.
    /// </summary>
    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();

        // Replace spaces with hyphens
        // FIX MA0009: Add timeout to prevent ReDoS attacks
        slug = Regex.Replace(slug, @"\s+", "-", RegexOptions.None, TimeSpan.FromSeconds(1));

        // Remove non-alphanumeric characters (except hyphens)
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "", RegexOptions.None, TimeSpan.FromSeconds(1));

        // Remove duplicate hyphens
        slug = Regex.Replace(slug, @"-+", "-", RegexOptions.None, TimeSpan.FromSeconds(1));

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
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return NormalizedValue;
    }

    public override string ToString() => Value;

    public static implicit operator string(GameTitle title) => title.Value;
}

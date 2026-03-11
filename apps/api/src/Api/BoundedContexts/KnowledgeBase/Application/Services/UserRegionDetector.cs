using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Detects user region from the Accept-Language HTTP header.
/// Issue #27: Multi-region preparation — extracts primary language tag as region hint.
/// Returns the first language tag (e.g., "en-US", "it-IT") or null if not available.
/// </summary>
internal sealed class UserRegionDetector : IUserRegionDetector
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserRegionDetector(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc/>
    public string? DetectRegion()
    {
        var acceptLanguage = _httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(acceptLanguage))
            return null;

        // Parse first language tag from Accept-Language header
        // Format: "en-US,en;q=0.9,it;q=0.8" → extract "en-US"
        var firstTag = acceptLanguage.Split(',', StringSplitOptions.TrimEntries)[0];

        // Strip quality factor if present: "en-US;q=0.9" → "en-US"
        var semiIndex = firstTag.IndexOf(';');
        if (semiIndex >= 0)
            firstTag = firstTag[..semiIndex].Trim();

        // Validate it looks like a language tag (2-10 chars, letters and hyphens only)
        if (firstTag.Length < 2 || firstTag.Length > 10)
            return null;

        foreach (var c in firstTag)
        {
            if (!char.IsLetter(c) && c != '-')
                return null;
        }

        return firstTag;
    }
}

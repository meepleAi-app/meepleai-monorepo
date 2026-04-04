namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Copyright license type for PDF documents.
/// Determines how RAG citations are displayed (verbatim vs paraphrased).
/// RAG Copyright KB Cards: tier-aware citation rendering.
/// </summary>
public enum LicenseType
{
    /// <summary>Default — publisher-owned, copyright protected.</summary>
    Copyrighted = 0,

    /// <summary>Creative Commons license — freely usable.</summary>
    CreativeCommons = 1,

    /// <summary>Public domain — no copyright restrictions.</summary>
    PublicDomain = 2
}

public static class LicenseTypeExtensions
{
    /// <summary>Returns true if content can be cited verbatim without ownership check.</summary>
    public static bool IsCopyrightFree(this LicenseType type)
        => type is LicenseType.CreativeCommons or LicenseType.PublicDomain;
}

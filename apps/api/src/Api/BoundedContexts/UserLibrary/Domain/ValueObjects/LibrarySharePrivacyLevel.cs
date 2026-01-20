namespace Api.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Privacy level for shared library links.
/// </summary>
public enum LibrarySharePrivacyLevel
{
    /// <summary>
    /// Library is discoverable in public listings.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Library is only accessible via direct link.
    /// </summary>
    Unlisted = 1
}

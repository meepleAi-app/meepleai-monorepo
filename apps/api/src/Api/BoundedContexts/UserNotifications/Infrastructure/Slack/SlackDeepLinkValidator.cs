namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Validates deep link paths for use in Slack Block Kit button URLs.
/// Prevents URL injection by accepting only absolute relative paths (e.g. "/notifications/123").
/// </summary>
internal static class SlackDeepLinkValidator
{
    /// <summary>
    /// Returns <paramref name="deepLinkPath"/> if it is a valid absolute relative path, null otherwise.
    /// Valid: non-empty, starts with '/', contains no "://" scheme separator.
    /// </summary>
    public static string? Validate(string? deepLinkPath)
    {
        if (string.IsNullOrWhiteSpace(deepLinkPath))
            return null;

        if (deepLinkPath[0] != '/')
            return null;

        // Reject protocol-relative URLs like //evil.com/path
        if (deepLinkPath.Length > 1 && deepLinkPath[1] == '/')
            return null;

        if (deepLinkPath.Contains("://", StringComparison.Ordinal))
            return null;

        return deepLinkPath;
    }
}

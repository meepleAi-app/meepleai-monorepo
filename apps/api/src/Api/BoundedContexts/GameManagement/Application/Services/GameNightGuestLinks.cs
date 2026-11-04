namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// The links in the emails a game night guest receives, written in one place so the web routes they point at and
/// the handlers that compose them cannot drift apart (#4846).
/// </summary>
internal static class GameNightGuestLinks
{
    /// <summary>
    /// The page that stops game night emails to the address behind the invitation:
    /// <c>apps/web/src/app/(public)/invites/unsubscribe/[key]</c>. The key is the credential, and it is the
    /// invitation's <c>UnsubscribeKey</c>, never its RSVP token (the organizer holds that one).
    /// </summary>
    public static string Unsubscribe(string baseUrl, string unsubscribeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(unsubscribeKey);

        return $"{baseUrl}/invites/unsubscribe/{unsubscribeKey}";
    }
}

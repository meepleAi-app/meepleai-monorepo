namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// The links in the emails a game night guest receives, written in one place so the web route they point at and
/// the handlers that compose them cannot drift apart (#4846).
/// </summary>
internal static class GameNightGuestLinks
{
    /// <summary>
    /// The page that stops game night emails to the address behind the invitation:
    /// <c>apps/web/src/app/(public)/invites/[token]/unsubscribe</c>. The token is the credential.
    /// </summary>
    public static string Unsubscribe(string baseUrl, string invitationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(invitationToken);

        return $"{baseUrl}/invites/{invitationToken}/unsubscribe";
    }
}

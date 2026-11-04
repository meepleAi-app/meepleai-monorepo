namespace Api.BoundedContexts.Authentication.Domain.Constants;

/// <summary>
/// The display name an account gets when nobody chose one (#4775): <c>Player-</c> followed by seven hex digits.
/// It is opaque on purpose — R3 (auth security fixes) replaced the old fallback, the local part of the email
/// address, because that exposed the address to anyone who could see the profile. The user can change it later
/// from the profile.
/// </summary>
/// <remarks>
/// The name is made in two places (the register endpoint and the OAuth callback) and recognized in a third (the
/// words of the account emails, which must not greet a person with «Hello Player-1a2b3c4»). All three go through
/// here, so that «generated» is said once, by the code that generates, and not guessed by whoever reads the name.
/// A person who types exactly this shape for themselves is greeted without a name, which costs nothing.
/// </remarks>
internal static class GeneratedDisplayName
{
    private const string Prefix = "Player-";
    private const int SuffixLength = 7;

    /// <summary>A new opaque name: <c>Player-</c> and the first seven hex digits of a fresh GUID.</summary>
    public static string Create() =>
        string.Concat(Prefix, Guid.NewGuid().ToString("N").AsSpan(0, SuffixLength));

    /// <summary>
    /// Whether <paramref name="displayName"/> is exactly the shape <see cref="Create"/> produces. Not trimmed and
    /// case-sensitive: it judges the stored value.
    /// </summary>
    public static bool IsGenerated(string? displayName)
    {
        if (displayName is null
            || displayName.Length != Prefix.Length + SuffixLength
            || !displayName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = Prefix.Length; i < displayName.Length; i++)
        {
            var c = displayName[i];
            if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}

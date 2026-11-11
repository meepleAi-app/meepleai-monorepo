namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// #4991: the <c>error</c> code of a refused login. Before, every refusal was a
/// <c>DomainException</c> → 400 <c>domain_error</c>, and the client could tell a wrong password from
/// a locked account only by reading the English sentence, which it then showed to the user. The
/// status stays 400 and the message stays the sentence for the logs; the code is what the client reads.
/// </summary>
internal static class LoginErrorCodes
{
    /// <summary>Unknown email or wrong password: the same code for both, so it does not reveal which.</summary>
    public const string InvalidCredentials = "invalid_credentials";

    /// <summary>Locked by too many failed attempts, already or by this one.</summary>
    public const string AccountLocked = "account_locked";

    /// <summary>Pending, suspended or banned.</summary>
    public const string AccountUnavailable = "account_unavailable";
}

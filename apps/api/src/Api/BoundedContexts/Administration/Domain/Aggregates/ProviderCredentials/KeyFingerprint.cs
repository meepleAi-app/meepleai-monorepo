namespace Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;

/// <summary>
/// Value object representing the masked fingerprint of an API key.
/// Format: <c>{first 5 chars}..{last 4 chars}</c> — never leaks the secret middle.
/// Used in audit logs, UI display, and DB <c>provider_credentials.key_fingerprint</c>.
/// </summary>
public sealed record KeyFingerprint
{
    private const int MinKeyLength = 10;

    public string Value { get; }

    private KeyFingerprint(string value) => Value = value;

    /// <summary>
    /// Builds a fingerprint from the plaintext key. The full key never leaves this method.
    /// </summary>
    /// <exception cref="ArgumentException">When the key is shorter than 10 characters.</exception>
    public static KeyFingerprint FromPlaintext(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < MinKeyLength)
            throw new ArgumentException(
                $"API key must be at least {MinKeyLength} characters for fingerprint generation",
                nameof(apiKey));

        var fingerprint = $"{apiKey[..5]}..{apiKey[^4..]}";
        return new KeyFingerprint(fingerprint);
    }

    /// <summary>
    /// Re-hydrates a fingerprint from its stored masked form (already masked, no validation).
    /// Used by EF Core value-converter during materialization. Not for application code.
    /// </summary>
    internal static KeyFingerprint FromStorage(string maskedValue) => new(maskedValue);

    public override string ToString() => Value;
}

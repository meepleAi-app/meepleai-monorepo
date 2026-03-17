using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Validates Slack request signatures using HMAC-SHA256.
/// Slack signs every interaction payload with the app's signing secret.
/// See: https://api.slack.com/authentication/verifying-requests-from-slack
/// </summary>
internal class SlackSignatureValidator
{
    private readonly string _signingSecret;

    public SlackSignatureValidator(IOptions<SlackNotificationConfiguration> config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _signingSecret = config.Value.SigningSecret;
    }

    /// <summary>
    /// Validates the Slack request signature against the computed HMAC-SHA256 hash.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    /// <param name="timestamp">The X-Slack-Request-Timestamp header value.</param>
    /// <param name="body">The raw request body.</param>
    /// <param name="signature">The X-Slack-Signature header value (e.g., "v0=abc123...").</param>
    /// <returns>True if the signature is valid; false otherwise.</returns>
    public bool Validate(string timestamp, string body, string signature)
    {
        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
            return false;

        // Replay protection: reject requests older than 5 minutes
        if (!long.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture, out var ts))
            return false;

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(ts);
        var now = DateTimeOffset.UtcNow;
        if (Math.Abs((now - requestTime).TotalSeconds) > 300)
            return false;

        var baseString = $"v0:{timestamp}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        var computed = "v0=" + Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(signature));
    }
}

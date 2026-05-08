using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.Authentication.Application.Services;

/// <summary>
/// Reads <c>STAGING_ALLOWED_EMAILS</c> from <see cref="IConfiguration"/> at construction time
/// and caches the parsed allowlist as an immutable <see cref="HashSet{String}"/>.
/// </summary>
internal sealed class StagingAccessGuard : IStagingAccessGuard
{
    private readonly HashSet<string> _allowedEmails;

    public StagingAccessGuard(IConfiguration configuration)
    {
        var raw = configuration["STAGING_ALLOWED_EMAILS"] ?? string.Empty;
        _allowedEmails = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsEmailAllowed(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        if (_allowedEmails.Count == 0)
        {
            return true;
        }

        return _allowedEmails.Contains(email.Trim());
    }

    public bool HasNonEmptyAllowlist => _allowedEmails.Count > 0;
}

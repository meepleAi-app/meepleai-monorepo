using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Api.Services;

/// <summary>
/// Protects API key values before they are stored inside cookies and unprotects them on reads.
/// Prevents plaintext API keys from leaking via Set-Cookie headers, logs, or support captures.
/// </summary>
public class ApiKeyCookieService
{
    private const string ProtectorPurpose = "MeepleAI.ApiKeyCookie";
    private readonly IDataProtector _protector;
    private readonly ILogger<ApiKeyCookieService> _logger;

    public ApiKeyCookieService(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ApiKeyCookieService> logger)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _logger = logger;
    }

    public string Protect(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return _protector.Protect(apiKey);
    }

    public bool TryUnprotect(string? protectedValue, out string apiKey)
    {
        apiKey = string.Empty;
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return false;
        }

        try
        {
            apiKey = _protector.Unprotect(protectedValue);
            return true;
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect API key cookie payload");
            return false;
        }
    }
}

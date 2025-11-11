namespace Api.Models;

using Microsoft.AspNetCore.Http;

public record SessionCookieConfiguration
{
    public string? Name { get; init; } = "meeple_session";

    public string? Domain { get; init; }

    public string Path { get; init; } = "/";

    public bool HttpOnly { get; init; } = true;

    /// <summary>
    /// Explicitly sets the secure flag on the session cookie. When omitted the application forces secure cookies
    /// even for HTTP requests (e.g. behind reverse proxies or during in-memory tests) unless disabled explicitly.
    /// </summary>
    public bool? Secure { get; init; }

    /// <summary>
    /// Sets the SameSite mode. If the secure flag is forced by the application this value will be coerced to
    /// <see cref="SameSiteMode.None"/> to support cross-site scenarios.
    /// </summary>
    public SameSiteMode? SameSite { get; init; }

    public bool UseForwardedProto { get; init; } = true;
}

namespace Api.Models;

using Microsoft.AspNetCore.Http;

public record SessionCookieConfiguration
{
    public string? Name { get; init; } = "meeple_session";

    public string? Domain { get; init; }

    public string Path { get; init; } = "/";

    public bool HttpOnly { get; init; } = true;

    public bool? Secure { get; init; }

    public SameSiteMode? SameSite { get; init; }

    public bool UseForwardedProto { get; init; } = true;
}

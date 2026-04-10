using System.Collections.Generic;

namespace Api.DevTools.Http;

/// <summary>Response shape for GET /dev/toggles and POST /dev/toggles/reset.</summary>
internal sealed record GetTogglesResponse
{
    public required IReadOnlyDictionary<string, bool> Toggles { get; init; }
    public required IReadOnlyList<string> KnownServices { get; init; }
}

/// <summary>Request body for PATCH /dev/toggles. Partial update.</summary>
internal sealed record PatchTogglesRequest
{
    public required Dictionary<string, bool> Toggles { get; init; }
}

/// <summary>Response shape for PATCH /dev/toggles.</summary>
internal sealed record PatchTogglesResponse
{
    public required IReadOnlyList<string> Updated { get; init; }
    public required IReadOnlyDictionary<string, bool> Toggles { get; init; }
}

/// <summary>Error response shape for 4xx/5xx.</summary>
internal sealed record DevToolsErrorResponse
{
    public required string Error { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<string>? UnknownKeys { get; init; }
    public string? TraceId { get; init; }
}

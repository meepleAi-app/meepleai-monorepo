using System.Text.Json;

namespace Api.Infrastructure.Serialization;

/// <summary>
/// Shared JSON serializer options for all SSE (Server-Sent Events) endpoints.
/// Uses camelCase property naming with numeric enum values (not string).
/// Frontend parsers expect: {"type":0,"data":{...},"timestamp":"..."}
/// </summary>
internal static class SseJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // NO JsonStringEnumConverter: frontend expects numeric enums (type: 0, not "StateUpdate")
    };
}

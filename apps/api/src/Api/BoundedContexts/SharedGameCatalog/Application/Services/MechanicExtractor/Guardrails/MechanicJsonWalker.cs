using System.Text.Json;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

/// <summary>
/// Shared recursive JSON walker used by the guardrails. Mirrors the original walk in the
/// M1.2 <c>MechanicOutputValidator</c> stub: visits every object node with its JSONPath-ish path.
/// </summary>
internal static class MechanicJsonWalker
{
    public static void ForEachObject(JsonElement el, string path, Action<JsonElement, string> visit)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                visit(el, path);
                foreach (var p in el.EnumerateObject())
                {
                    ForEachObject(p.Value, $"{path}.{p.Name}", visit);
                }
                break;

            case JsonValueKind.Array:
                var idx = 0;
                foreach (var item in el.EnumerateArray())
                {
                    ForEachObject(item, $"{path}[{idx}]", visit);
                    idx++;
                }
                break;
        }
    }
}

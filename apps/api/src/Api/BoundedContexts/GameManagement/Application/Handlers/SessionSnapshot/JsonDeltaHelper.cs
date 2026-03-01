using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;

/// <summary>
/// Computes and applies JSON deltas using a simplified RFC 6902 JSON Patch format.
/// Supports: replace, add, remove operations at any nesting depth.
/// Issue #4755: SessionSnapshot - Delta-based History + State Reconstruction.
/// </summary>
internal static class JsonDeltaHelper
{
    /// <summary>
    /// Computes the delta (JSON Patch operations) from previousState to currentState.
    /// Returns a JSON array of RFC 6902 operations.
    /// </summary>
    public static string ComputeDelta(string previousStateJson, string currentStateJson)
    {
        var previous = JsonNode.Parse(previousStateJson);
        var current = JsonNode.Parse(currentStateJson);

        var operations = new JsonArray();
        ComputeDiff(previous, current, "", operations);

        return operations.ToJsonString();
    }

    /// <summary>
    /// Applies a JSON Patch delta to a base state, returning the resulting state.
    /// </summary>
    public static string ApplyDelta(string baseStateJson, string deltaJson)
    {
        var state = JsonNode.Parse(baseStateJson);
        var operations = JsonNode.Parse(deltaJson)?.AsArray();

        if (operations == null || operations.Count == 0)
            return baseStateJson;

        foreach (var op in operations)
        {
            if (op == null) continue;
            var opType = op["op"]?.GetValue<string>();
            var path = op["path"]?.GetValue<string>();

            if (string.IsNullOrEmpty(opType) || path == null) continue;

            switch (opType)
            {
                case "replace":
                case "add":
                    SetValueAtPath(state, path, op["value"]?.DeepClone());
                    break;
                case "remove":
                    RemoveAtPath(state, path);
                    break;
            }
        }

        return state?.ToJsonString() ?? "{}";
    }

    /// <summary>
    /// Applies multiple deltas sequentially to reconstruct state.
    /// </summary>
    public static string ReconstructState(string checkpointJson, IEnumerable<string> deltas)
    {
        var state = checkpointJson;
        foreach (var delta in deltas)
        {
            state = ApplyDelta(state, delta);
        }
        return state;
    }

    private static void ComputeDiff(JsonNode? previous, JsonNode? current, string path, JsonArray operations)
    {
        if (previous == null && current == null) return;

        if (previous == null)
        {
            operations.Add(CreateOperation("add", path, current?.DeepClone()));
            return;
        }

        if (current == null)
        {
            operations.Add(CreateOperation("remove", path, null));
            return;
        }

        // Different node types → replace
        if (previous.GetValueKind() != current.GetValueKind())
        {
            operations.Add(CreateOperation("replace", path, current.DeepClone()));
            return;
        }

        // Both are objects → diff properties
        if (previous is JsonObject prevObj && current is JsonObject currObj)
        {
            // Check removed and changed properties
            foreach (var prop in prevObj)
            {
                var childPath = $"{path}/{EscapeJsonPointer(prop.Key)}";
                if (!currObj.ContainsKey(prop.Key))
                {
                    operations.Add(CreateOperation("remove", childPath, null));
                }
                else
                {
                    ComputeDiff(prop.Value, currObj[prop.Key], childPath, operations);
                }
            }

            // Check added properties
            foreach (var prop in currObj)
            {
                if (!prevObj.ContainsKey(prop.Key))
                {
                    var childPath = $"{path}/{EscapeJsonPointer(prop.Key)}";
                    operations.Add(CreateOperation("add", childPath, prop.Value?.DeepClone()));
                }
            }
            return;
        }

        // Both are arrays → replace entire array if different
        if (previous is JsonArray prevArr && current is JsonArray currArr)
        {
            if (!AreSameArray(prevArr, currArr))
            {
                operations.Add(CreateOperation("replace", path, current.DeepClone()));
            }
            return;
        }

        // Both are values → compare
        if (!AreSameValue(previous, current))
        {
            operations.Add(CreateOperation("replace", path, current.DeepClone()));
        }
    }

    private static JsonObject CreateOperation(string op, string path, JsonNode? value)
    {
        var operation = new JsonObject
        {
            ["op"] = op,
            ["path"] = path
        };
        if (value != null && !string.Equals(op, "remove", StringComparison.Ordinal))
        {
            operation["value"] = value;
        }
        return operation;
    }

    private static void SetValueAtPath(JsonNode? root, string path, JsonNode? value)
    {
        if (root == null) return;
        if (string.IsNullOrEmpty(path) || string.Equals(path, "/", StringComparison.Ordinal))
        {
            // Can't replace root in-place, but this shouldn't happen with our diff
            return;
        }

        var segments = ParsePath(path);
        var current = root;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (current is JsonObject obj)
            {
                if (!obj.ContainsKey(segments[i]))
                {
                    obj[segments[i]] = new JsonObject();
                }
                current = obj[segments[i]];
            }
            else
            {
                return; // Can't traverse further
            }
        }

        if (current is JsonObject parentObj)
        {
            parentObj[segments[^1]] = value;
        }
    }

    private static void RemoveAtPath(JsonNode? root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return;

        var segments = ParsePath(path);
        var current = root;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (current is JsonObject obj && obj.ContainsKey(segments[i]))
            {
                current = obj[segments[i]];
            }
            else
            {
                return;
            }
        }

        if (current is JsonObject parentObj)
        {
            parentObj.Remove(segments[^1]);
        }
    }

    private static string[] ParsePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return [];
        // Remove leading /
        var trimmed = path.StartsWith('/') ? path[1..] : path;
        return trimmed.Split('/').Select(UnescapeJsonPointer).ToArray();
    }

    private static string EscapeJsonPointer(string segment)
        => segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static string UnescapeJsonPointer(string segment)
        => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);

    private static bool AreSameValue(JsonNode a, JsonNode b)
    {
        try
        {
            return string.Equals(a.ToJsonString(), b.ToJsonString(), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static bool AreSameArray(JsonArray a, JsonArray b)
    {
        if (a.Count != b.Count) return false;
        return string.Equals(a.ToJsonString(), b.ToJsonString(), StringComparison.Ordinal);
    }
}

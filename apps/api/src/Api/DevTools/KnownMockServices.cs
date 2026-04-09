using System.Collections.Generic;

namespace Api.DevTools;

/// <summary>
/// Registry of all service identifiers that can be toggled via the
/// <c>MOCK_&lt;NAME&gt;</c> environment variable convention.
/// </summary>
/// <remarks>
/// Keep this list in sync with:
/// <list type="bullet">
///   <item><description><c>.env.dev.local.example</c></description></item>
///   <item><description><c>DevToolsServiceCollectionExtensions.AddMeepleDevTools()</c></description></item>
/// </list>
/// </remarks>
internal static class KnownMockServices
{
    /// <summary>
    /// All service identifiers that can be toggled via <c>MOCK_&lt;NAME&gt;</c> env var.
    /// Must stay in sync with <c>.env.dev.local.example</c> and registered services in
    /// <c>DevToolsServiceCollectionExtensions.AddMeepleDevTools()</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        "llm",
        "embedding",
        "reranker",
        "smoldocling",
        "unstructured",
        "bgg",
        "s3",
        "n8n",
    };
}

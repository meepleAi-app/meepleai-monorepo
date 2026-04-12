using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Api.Tests.Integration.DevTools;

/// <summary>
/// Reflection-based verification that the Release build of Api.dll
/// contains zero types in the Api.DevTools namespace.
/// Skipped by default — only runs when manually built in Release.
/// CI uses .github/workflows/dev-tools-isolation.yml for the same check.
/// </summary>
public class NoMockInReleaseTests
{
    [Fact(Skip = "Only runs in Release build via CI workflow; local Debug always includes DevTools")]
    public void ReleaseAssembly_HasNoDevToolsTypes()
    {
        // Walk up from test bin to Api Release build directory
        var candidate = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Api", "bin", "Release", "net9.0", "Api.dll");

        Assert.True(File.Exists(candidate), $"Release Api.dll not found at {candidate}");

        var assembly = Assembly.LoadFrom(candidate);
        var devToolsTypes = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Api.DevTools", StringComparison.Ordinal) == true)
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(devToolsTypes);
    }
}

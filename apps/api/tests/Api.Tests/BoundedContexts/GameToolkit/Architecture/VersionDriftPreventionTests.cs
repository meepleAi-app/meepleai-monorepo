using System.Text.RegularExpressions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Architecture;

/// <summary>
/// Issue #1144 / spec AC8 (Fowler panel CRITICAL) — drift-prevention guard
/// against divergence between the legacy <c>Version int</c> and the new
/// <c>VersionSemver</c> column on <c>GameToolkitEntity</c>.
///
/// Both columns must be written together for the duration of the dual-column
/// period documented in spec §13. This test fails the build if production
/// code outside <c>Infrastructure/Migrations/</c> writes <c>entity.Version</c>
/// (or <c>toolkit.Version</c>) without an adjacent <c>VersionSemver</c>
/// assignment in the same statement block.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class VersionDriftPreventionTests
{
    /// <summary>
    /// Matches any production-code write to a <c>Version</c> property that is
    /// almost certainly the GameToolkit legacy column. We intentionally allow
    /// false positives in unrelated entities by anchoring the regex to common
    /// names (<c>toolkit.Version =</c>, <c>entity.Version =</c>) used in the
    /// GameToolkit aggregate. The accompanying allow-list narrows further.
    /// </summary>
    /// <remarks>
    /// Known coverage gaps (acknowledged per #1156 spec-panel review):
    /// <list type="bullet">
    ///   <item>Bare <c>Version = …</c> assignments inside the
    ///         <c>GameToolkit</c> domain aggregate constructor (no receiver
    ///         qualifier). Currently safe because the domain aggregate has
    ///         no <c>VersionSemver</c> property — when it gains one,
    ///         widen this regex to also match the bare form, scoped to
    ///         <c>BoundedContexts/GameToolkit/Domain/</c>.</item>
    ///   <item>Reflection writes via <c>SetPrivateProperty(toolkit, "Version", …)</c>
    ///         in <c>GameToolkitRepository.MapToDomain</c>. The repository
    ///         is the single persistence boundary; its write path is
    ///         exercised by the paired-write assertion in
    ///         <c>GetToolkitDetailQueryHandlerTests.Version_int_and_semver_stay_in_sync_after_persistence</c>.</item>
    /// </list>
    /// </remarks>
    private static readonly Regex VersionWriteRegex = new(
        @"\b(?:toolkit|entity)\.Version\s*=",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex VersionSemverWriteRegex = new(
        @"\bVersionSemver\s*=",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void Production_Version_writes_must_be_paired_with_VersionSemver_writes()
    {
        var apiSrc = LocateApiSrc();
        var violations = new List<string>();

        foreach (var path in Directory.EnumerateFiles(apiSrc, "*.cs", SearchOption.AllDirectories))
        {
            // Skip migrations folder (legitimately writes both columns in
            // generated SQL; the migration scaffolding doesn't count).
            if (path.Contains(Path.Combine("Infrastructure", "Migrations"), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!VersionWriteRegex.IsMatch(line))
                {
                    continue;
                }

                // Look in a 3-line window (current + next 2) for the paired
                // VersionSemver write. The handler/repository pattern emits
                // both on adjacent lines within the same object initializer.
                var window = string.Join("\n", lines.Skip(i).Take(3));
                if (!VersionSemverWriteRegex.IsMatch(window))
                {
                    var rel = Path.GetRelativePath(apiSrc, path).Replace('\\', '/');
                    violations.Add($"{rel}:{i + 1}  {line.Trim()}");
                }
            }
        }

        violations.Should().BeEmpty(
            "production code must write VersionSemver alongside the legacy " +
            "Version int (spec D-5 / AC8). Violations:\n" +
            string.Join('\n', violations));
    }

    private static string LocateApiSrc()
    {
        // Walk upwards from the test binary's directory to the repo root,
        // then descend into apps/api/src/Api. The path resolves identically
        // whether the test runs from the IDE or `dotnet test`.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        var apiSrc = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiSrc).Should().BeTrue($"Api source must exist at {apiSrc}");
        return apiSrc;
    }
}

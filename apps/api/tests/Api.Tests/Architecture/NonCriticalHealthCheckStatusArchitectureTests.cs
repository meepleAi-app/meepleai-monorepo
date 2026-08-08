using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #3618 — architecture gate: <b>a NonCritical health check must never report Unhealthy</b>.
/// <para>
/// The <c>failureStatus</c> argument passed at registration (<c>AddCheck&lt;T&gt;("x",
/// HealthStatus.Degraded, …)</c>) is NOT a cap on the reported status. ASP.NET Core applies it only
/// when a check throws an <i>unhandled</i> exception; a check that catches its own exception and
/// returns <c>HealthCheckResult.Unhealthy(...)</c> bypasses it entirely and that value propagates
/// straight into the aggregate report.
/// </para>
/// <para>
/// Because <c>/health</c> uses the default <c>ResultStatusCodes</c> map — Healthy/Degraded → 200,
/// Unhealthy → 503 — a single NonCritical check returning Unhealthy 503s the whole endpoint even
/// though the API is serving fine. That was the root cause behind #3339 (orchestrator not deployed
/// in staging) and the reason the staging smoke gate had to tolerate a 503 and could therefore not
/// detect a genuinely broken deploy.
/// </para>
/// <para>
/// Returning Degraded does not lose the signal: <c>HealthStateMachine</c> treats every non-Healthy
/// result as a failure and runs its own Healthy → Degraded → Unhealthy escalation on consecutive
/// failure counts, so email alerting is unaffected.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "3618")]
public sealed class NonCriticalHealthCheckStatusArchitectureTests
{
    /// <summary>
    /// The only checks allowed to report Unhealthy, each with the reason. Everything else is
    /// NonCritical (or Optional) and must degrade instead of 503-ing the aggregate endpoint.
    /// <para>
    /// Adding an entry here is a deliberate operational decision: it means "this failure SHOULD take
    /// /health to 503 and block a staging deploy".
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> MayReportUnhealthy = new(StringComparer.Ordinal)
    {
        ["ConfigurationHealthCheck"] =
            "startup configuration is missing/invalid — the deploy is genuinely broken and must not "
            + "be promoted silently (registered in ObservabilityServiceExtensions)",
        ["SharedGameCatalogHealthCheck"] =
            "shared-catalog full-text search is unusable — a core read path of the product "
            + "(registered in ObservabilityServiceExtensions)",
    };

    private const string UnhealthyFactory = "HealthCheckResult.Unhealthy";
    private const string HealthCheckInterface = ": IHealthCheck";

    [Fact]
    public void NonCriticalHealthChecks_DoNotReportUnhealthy()
    {
        var offenders = ScanHealthChecks()
            .Where(c => !MayReportUnhealthy.ContainsKey(c.TypeName))
            .Where(c => c.Text.Contains(UnhealthyFactory, StringComparison.Ordinal))
            .Select(c => $"{c.Location}  {c.TypeName}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a NonCritical health check returning Unhealthy takes the aggregate /health to 503 even "
            + "though the API is serving (#3618, root cause of #3339). Return "
            + "HealthCheckResult.Degraded instead — alerting still fires via HealthStateMachine. If "
            + "the failure genuinely SHOULD block a deploy, add the check to MayReportUnhealthy with "
            + "a reason and tag it Critical at registration. Offenders:\n"
            + string.Join('\n', offenders));
    }

    /// <summary>
    /// Keeps the allowlist honest: an entry that no longer matches a real check is stale and would
    /// silently grant an exemption to a future type that reuses the name.
    /// </summary>
    [Fact]
    public void MayReportUnhealthyAllowlist_HasNoStaleEntries()
    {
        var discovered = ScanHealthChecks()
            .Select(c => c.TypeName)
            .ToHashSet(StringComparer.Ordinal);

        var stale = MayReportUnhealthy.Keys
            .Where(name => !discovered.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            "every allowlist entry must correspond to an existing IHealthCheck implementation. "
            + "Stale:\n" + string.Join('\n', stale));
    }

    [Fact]
    public void Scanner_FindsTheHealthChecks()
    {
        // Guards the gate itself: a scanner that silently matches nothing would make both tests
        // above pass vacuously.
        ScanHealthChecks().Should().HaveCountGreaterThan(10,
            "the API defines ~20 IHealthCheck implementations; finding almost none means the "
            + "scanner broke, not that the codebase is clean");
    }

    // -----------------------------------------------------------------------
    // Source scanning
    // -----------------------------------------------------------------------

    private sealed record HealthCheckSource(string TypeName, string Text, string Location);

    private static List<HealthCheckSource> ScanHealthChecks()
    {
        var apiSrc = LocateApiSrc();
        var sources = new List<HealthCheckSource>();

        foreach (var path in Directory.EnumerateFiles(apiSrc, "*HealthCheck.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(apiSrc, path).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal)
                || relative.StartsWith("obj/", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            if (!text.Contains(HealthCheckInterface, StringComparison.Ordinal))
            {
                continue;
            }

            sources.Add(new HealthCheckSource(
                Path.GetFileNameWithoutExtension(path),
                text,
                relative));
        }

        return sources;
    }

    private static string LocateApiSrc()
    {
        // `.git` is a directory in a normal clone and a FILE inside a git worktree — probe both so
        // the gate runs identically in CI and in a developer worktree.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
            && !Directory.Exists(Path.Combine(dir.FullName, ".git"))
            && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        var apiSrc = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiSrc).Should().BeTrue($"Api source must exist at {apiSrc}");
        return apiSrc;
    }
}

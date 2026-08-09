using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #3622 — pins the set of <c>Category</c> trait values against the CI gate filters.
///
/// <para>
/// The fast gate selects tests by EXCLUSION (<c>Category!=Integration&amp;Category!=E2E&amp;…</c>)
/// so that a test which forgets its trait still runs. That inversion made the default safe, but it
/// moved the fragility one step: the deny-list is a literal string in two workflow files, and the
/// day someone introduces a new category — say <c>Chaos</c> — its tests silently join the fast
/// gate. If they need Docker or take a minute each, the blocking gate on every PR breaks or blows
/// its SLO, and nothing here would have warned anyone.
/// </para>
/// <para>
/// So this test enumerates every <c>Category</c> actually used in the assembly and fails on any
/// value that is not classified below. It does not judge whether a category belongs in the fast
/// gate — that is a human call. It only guarantees the call gets made, once, deliberately, instead
/// of being inherited by accident.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public sealed class TestCategoryGateArchitectureTests
{
    /// <summary>
    /// Categories the fast gate EXCLUDES. Must stay byte-identical to the <c>--filter</c> in
    /// <c>.github/workflows/dev-fast.yml</c> and <c>.github/workflows/ci.yml</c>.
    /// </summary>
    private static readonly HashSet<string> ExcludedFromFastGate = new(StringComparer.Ordinal)
    {
        "Integration",  // real infrastructure (Testcontainers, databases)
        "E2E",          // full stack through WebApplicationFactory + containers
        "Performance",  // throughput/latency benchmarks, 10-30s per test
        "Manual",       // run on demand by a human, never in CI
        "Slow",         // declared in TestCategories; reserved for long-running work
    };

    /// <summary>
    /// Categories that DO run in the fast gate. Each one is here because it was checked to need no
    /// external service: Security and Unit are in-process; Contract spins up an in-process
    /// WireMock; PDF and CrossContext always appear alongside Integration, which already excludes
    /// them (they are listed for completeness, not as an independent grant).
    /// </summary>
    private static readonly HashSet<string> AllowedInFastGate = new(StringComparer.Ordinal)
    {
        "Unit",
        "Security",
        "Contract",
        "PDF",
        "CrossContext",
    };

    [Fact]
    public void EveryCategoryInUse_IsClassifiedAgainstTheFastGate()
    {
        var unclassified = CategoriesInUse()
            .Where(c => !ExcludedFromFastGate.Contains(c) && !AllowedInFastGate.Contains(c))
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        unclassified.Should().BeEmpty(
            "a new test Category must be classified before it can reach CI. Decide whether " +
            "'{0}' belongs in the blocking fast gate: if it needs infrastructure or is slow, add " +
            "it to the --filter in dev-fast.yml AND ci.yml and to ExcludedFromFastGate; otherwise " +
            "add it to AllowedInFastGate. Leaving it unclassified means its tests join the " +
            "blocking gate by accident (#3622).",
            string.Join(", ", unclassified));
    }

    [Fact]
    public void ExcludedAndAllowed_DoNotOverlap()
    {
        // An overlap would make the intent ambiguous and the next reader's fix a coin flip.
        ExcludedFromFastGate.Intersect(AllowedInFastGate, StringComparer.Ordinal)
            .Should().BeEmpty("a category is either excluded from the fast gate or allowed in it");
    }

    /// <summary>
    /// Every distinct <c>Category</c> value declared on a test class in this assembly.
    ///
    /// Read through <see cref="CustomAttributeData"/> rather than <c>GetCustomAttributes</c>:
    /// xUnit's <c>TraitAttribute</c> exposes no public properties, so the value is only reachable
    /// as a constructor argument. Matching on the attribute's simple name also keeps this working
    /// for traits declared via the <c>TestCategories</c> constants, which compile down to the same
    /// literal.
    /// </summary>
    private static IEnumerable<string> CategoriesInUse() =>
        typeof(TestCategoryGateArchitectureTests).Assembly
            .GetTypes()
            .SelectMany(t => t.GetCustomAttributesData())
            .Where(a => a.AttributeType.Name == "TraitAttribute" && a.ConstructorArguments.Count == 2)
            .Where(a => (a.ConstructorArguments[0].Value as string) == "Category")
            .Select(a => a.ConstructorArguments[1].Value as string)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.Ordinal);
}

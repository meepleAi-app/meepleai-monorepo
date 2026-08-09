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
    /// Categories that some workflow actually selects, with the workflow that selects them.
    /// Being excluded from the fast gate is not the same as being run somewhere else: that gap is
    /// what #3625 was.
    /// </summary>
    private static readonly Dictionary<string, string> SelectedByAGate = new(StringComparer.Ordinal)
    {
        ["Unit"] = "dev-fast.yml + ci.yml (deny-list gate)",
        ["Security"] = "dev-fast.yml + ci.yml (deny-list gate)",
        ["Contract"] = "dev-fast.yml + ci.yml (deny-list gate)",
        ["PDF"] = "dev-fast.yml + ci.yml (deny-list gate)",
        ["CrossContext"] = "dev-fast.yml + ci.yml (deny-list gate)",
        ["Integration"] = "dev-async.yml + ci.yml (Category=Integration shards)",
        ["E2E"] = "backend-e2e-tests.yml",
    };

    /// <summary>
    /// Issue #3625 — no test class may be invisible to every gate.
    ///
    /// <para>
    /// #3622 pinned each category as classified-or-not against the fast gate. That check passes
    /// happily for a category which is excluded from the fast gate and selected by nothing else:
    /// <c>Performance</c> was in exactly that state, and 58 tests sat in it. Four of them had been
    /// failing for months — three <c>ArbitroBenchmarkTests</c> broken at construction, one Moq
    /// callback left behind by a signature change — with no gate to say so.
    /// </para>
    /// <para>
    /// So: a class whose categories are all unselected must be explicitly skipped. Deliberately
    /// parked tests are fine (<c>PasswordHashBaselineTests</c> is a local benchmark baseline, every
    /// fact carrying a Skip reason). Silently parked ones are not.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryTestClass_IsSelectedByAGate_OrIsExplicitlySkipped()
    {
        var invisible = typeof(TestCategoryGateArchitectureTests).Assembly
            .GetTypes()
            .Select(t => new { Type = t, Categories = CategoriesOf(t) })
            .Where(x => x.Categories.Count > 0)
            .Where(x => !x.Categories.Any(SelectedByAGate.ContainsKey))
            .Where(x => !AllTestsAreSkipped(x.Type))
            .Select(x => $"{x.Type.FullName} [{string.Join(", ", x.Categories.OrderBy(c => c, StringComparer.Ordinal))}]")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        invisible.Should().BeEmpty(
            "every test class must be reachable by some gate. These declare only categories no " +
            "workflow selects, and are not skipped either, so they never run and nothing reports " +
            "on them: {0}. Fix by adding a category a gate selects (Integration for tests needing " +
            "containers, Unit for in-process ones), by giving every fact an explicit Skip reason, " +
            "or by deleting them (#3625).",
            string.Join("; ", invisible));
    }

    private static IReadOnlyList<string> CategoriesOf(Type type) =>
        type.GetCustomAttributesData()
            .Where(a => a.AttributeType.Name == "TraitAttribute" && a.ConstructorArguments.Count == 2)
            .Where(a => (a.ConstructorArguments[0].Value as string) == "Category")
            .Select(a => a.ConstructorArguments[1].Value as string)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// True when the class declares at least one fact and every one of them carries a non-empty
    /// <c>Skip</c>. Read from <see cref="CustomAttributeData"/> so it works for both
    /// <c>[Fact(Skip = "…")]</c> and <c>[Theory(Skip = "…")]</c>.
    /// </summary>
    private static bool AllTestsAreSkipped(Type type)
    {
        var facts = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.GetCustomAttributesData()
                .FirstOrDefault(a => a.AttributeType.Name is "FactAttribute" or "TheoryAttribute"))
            .Where(a => a is not null)
            .ToList();

        return facts.Count > 0 && facts.TrueForAll(HasSkipReason);
    }

    private static bool HasSkipReason(CustomAttributeData? fact) =>
        fact!.NamedArguments.Any(n =>
            n.MemberName == "Skip" && !string.IsNullOrWhiteSpace(n.TypedValue.Value as string));

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

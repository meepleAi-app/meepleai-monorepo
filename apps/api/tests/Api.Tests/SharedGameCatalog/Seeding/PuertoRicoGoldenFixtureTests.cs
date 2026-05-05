using System.Text.Json;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Seeding;

/// <summary>
/// Coverage invariants for the Puerto Rico golden claim fixture (Sprint 2 / ADR-051 M2.0).
///
/// Locks in two structural properties that the spec requires:
///   1. ≥ 50 claims, with at least one claim per section id 0..5 (6 sections total).
///   2. Page coverage: distinct expectedPage values / total PDF page count ≥ 0.80.
///
/// PDF metadata (verified during Task 1 fixture curation):
///   - SHA256:    d55058bde864f4216eecd14e2d3fabd244955972f3176e1b3bf64e5ed7afa697
///   - Page count: 16
///
/// TDD note: these tests would fail against an empty / partial fixture (e.g. &lt; 50 claims,
/// missing sections, or page coverage below 80%). They currently pass against the curated
/// 75-claim fixture (15/16 distinct pages = 93.75%, all 6 sections present).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class PuertoRicoGoldenFixtureTests
{
    /// <summary>
    /// Total page count of the Puerto Rico rulebook PDF
    /// (sha256 d55058bde864f4216eecd14e2d3fabd244955972f3176e1b3bf64e5ed7afa697).
    /// Used as the denominator for the page-coverage invariant.
    /// </summary>
    private const int PuertoRicoPdfPageCount = 16;

    /// <summary>
    /// Minimum number of claims required by the spec.
    /// </summary>
    private const int MinClaimCount = 50;

    /// <summary>
    /// Section ids that must each have ≥ 1 claim.
    /// </summary>
    private static readonly int[] RequiredSectionIds = { 0, 1, 2, 3, 4, 5 };

    /// <summary>
    /// Minimum fraction of distinct PDF pages that must be referenced by at least one claim.
    /// </summary>
    private const double MinPageCoverageRatio = 0.80;

    [Fact]
    public void Fixture_HasMinClaimsAndAllSections()
    {
        // Arrange
        var claims = LoadClaims();

        // Act
        var sections = claims
            .EnumerateArray()
            .Select(c => c.GetProperty("section").GetInt32())
            .Distinct()
            .OrderBy(s => s)
            .ToArray();

        // Assert
        claims.GetArrayLength().Should().BeGreaterThanOrEqualTo(
            MinClaimCount,
            "spec requires at least {0} curated claims to give the matcher meaningful coverage",
            MinClaimCount);

        sections.Should().BeEquivalentTo(
            RequiredSectionIds,
            "every rulebook section (0..5) must be represented by at least one claim");
    }

    [Fact]
    public void Fixture_PageCoverageAtLeast80Percent()
    {
        // Arrange
        var claims = LoadClaims();

        // Act
        var distinctPages = claims
            .EnumerateArray()
            .Select(c => c.GetProperty("expectedPage").GetInt32())
            .Distinct()
            .Count();

        var coverage = (double)distinctPages / PuertoRicoPdfPageCount;

        // Assert
        coverage.Should().BeGreaterThanOrEqualTo(
            MinPageCoverageRatio,
            "claims must touch ≥ {0:P0} of the {1}-page PDF; got {2}/{1} = {3:P2}",
            MinPageCoverageRatio,
            PuertoRicoPdfPageCount,
            distinctPages,
            coverage);
    }

    /// <summary>
    /// Load the golden-claims.json fixture and return the "claims" array element.
    /// Walks up from the test runner CWD until the repo root is found, matching the
    /// convention used elsewhere in this project (see RagQualityValidationTests).
    /// </summary>
    private static JsonElement LoadClaims()
    {
        var path = FindFixturePath();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        // The JsonDocument is disposed after this method returns; clone the claims array
        // so callers receive a self-contained JsonElement.
        return doc.RootElement.GetProperty("claims").Clone();
    }

    private static string FindFixturePath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        for (int levels = 1; levels <= 10; levels++)
        {
            var upPath = string.Join(Path.DirectorySeparatorChar.ToString(),
                Enumerable.Repeat("..", levels));
            var testPath = Path.GetFullPath(Path.Combine(currentDir, upPath,
                "data", "rulebook", "golden", "puerto-rico", "golden-claims.json"));

            if (File.Exists(testPath))
                return testPath;
        }

        throw new InvalidOperationException(
            $"Could not find data/rulebook/golden/puerto-rico/golden-claims.json searching up from: {currentDir}");
    }
}

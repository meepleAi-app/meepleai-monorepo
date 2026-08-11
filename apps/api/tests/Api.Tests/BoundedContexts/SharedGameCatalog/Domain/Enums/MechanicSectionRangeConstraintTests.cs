using System;
using System.Linq;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// #2974 regression guard. The EF check constraints
/// <c>ck_mechanic_{section_runs,claims,golden_claims}_section_range</c> use
/// <c>section BETWEEN 0 AND 8</c>. If <see cref="MechanicSection"/> grows without updating
/// those constraints (+ a migration), the pipeline crashes at persistence time with
/// Npgsql 23514 on any section &gt; the stale bound (that is exactly how a 0-5 bound
/// silently broke every real analysis once Setup/Components/EndgameScoring were added).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicSectionRangeConstraintTests
{
    // Must match the upper bound hardcoded in the three *EntityConfiguration files.
    private const int CheckConstraintUpperBound = 8;

    [Fact]
    public void MechanicSection_upper_bound_matches_check_constraints()
    {
        var maxSectionIndex = Enum.GetValues<MechanicSection>().Cast<int>().Max();

        maxSectionIndex.Should().Be(
            CheckConstraintUpperBound,
            "the ck_mechanic_*_section_range CHECK constraints allow only 'section BETWEEN 0 AND {0}'; " +
            "if MechanicSection changed, update all three EntityConfiguration files and add a migration (#2974)",
            CheckConstraintUpperBound);
    }

    [Fact]
    public void MechanicSection_values_are_contiguous_from_zero()
    {
        // A "BETWEEN 0 AND N" constraint assumes contiguous 0..N values with no gaps.
        var values = Enum.GetValues<MechanicSection>().Cast<int>().OrderBy(x => x).ToArray();

        values.Should().Equal(
            Enumerable.Range(0, values.Length).ToArray(),
            "the section range CHECK constraint assumes contiguous enum values starting at 0");
    }
}

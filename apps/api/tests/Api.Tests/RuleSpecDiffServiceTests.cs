using System;
using System.Collections.Generic;
using System.Linq;
using Api.Models;
using Api.Services;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Unit tests for RuleSpecDiffService which computes differences between RuleSpec versions.
///
/// Tests cover:
/// - Atom additions, modifications, deletions
/// - Field-level change detection
/// - Diff summary generation
/// - Edge cases (empty specs, null fields, identical specs)
/// </summary>
public class RuleSpecDiffServiceTests
{
    private readonly ITestOutputHelper _output;

    private readonly RuleSpecDiffService _service;

    public RuleSpecDiffServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _service = new RuleSpecDiffService();
    }

    #region Diff Computation Tests

    /// <summary>
    /// Scenario: Diff with no changes
    ///   Given two identical RuleSpecs
    ///   When computing diff
    ///   Then all atoms are marked unchanged
    ///   And summary shows zero changes
    /// </summary>
    [Fact]
    public void ComputeDiff_WithIdenticalSpecs_ReturnsNoChanges()
    {
        // Arrange: Two identical specs
        var atoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text 1", "Setup", "1", "5"),
            new RuleAtom("atom2", "Rule text 2", "Gameplay", "2", "10")
        };
        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, atoms);
        var to = new RuleSpec("chess", "1.0", DateTime.UtcNow, atoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: No changes detected
        diff.Summary.TotalChanges.Should().Be(0);
        diff.Summary.Added.Should().Be(0);
        diff.Summary.Modified.Should().Be(0);
        diff.Summary.Deleted.Should().Be(0);
        diff.Summary.Unchanged.Should().Be(2);

        // All changes should be marked as Unchanged
        diff.Changes.Should().OnlyContain(c => c.Type == ChangeType.Unchanged);
    }

    /// <summary>
    /// Scenario: Atom added in new version
    ///   Given from version has 2 atoms and to version has 3 atoms
    ///   When computing diff
    ///   Then one atom is marked as Added
    ///   And summary reflects the addition
    /// </summary>
    [Fact]
    public void ComputeDiff_WithNewAtom_DetectsAddition()
    {
        // Arrange: From has 2 atoms, To has 3 atoms
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text 1", "Setup", "1", "5"),
            new RuleAtom("atom2", "Rule text 2", "Gameplay", "2", "10")
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text 1", "Setup", "1", "5"),
            new RuleAtom("atom2", "Rule text 2", "Gameplay", "2", "10"),
            new RuleAtom("atom3", "New rule text", "Scoring", "3", "15")
        };

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: One addition detected
        diff.Summary.Added.Should().Be(1);
        diff.Summary.Modified.Should().Be(0);
        diff.Summary.Deleted.Should().Be(0);
        diff.Summary.Unchanged.Should().Be(2);
        diff.Summary.TotalChanges.Should().Be(1);

        // Find the added atom
        var addedChange = diff.Changes.FirstOrDefault(c => c.Type == ChangeType.Added);
        addedChange.Should().NotBeNull();
        addedChange!.NewAtom.Should().Be("atom3");
        addedChange.OldAtom.Should().BeNull();
        addedChange.NewValue?.text.Should().Be("New rule text");
    }

    /// <summary>
    /// Scenario: Atom deleted in new version
    ///   Given from version has 3 atoms and to version has 2 atoms
    ///   When computing diff
    ///   Then one atom is marked as Deleted
    ///   And summary reflects the deletion
    /// </summary>
    [Fact]
    public void ComputeDiff_WithDeletedAtom_DetectsDeletion()
    {
        // Arrange: From has 3 atoms, To has 2 atoms
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text 1", "Setup", "1", "5"),
            new RuleAtom("atom2", "Rule text 2", "Gameplay", "2", "10"),
            new RuleAtom("atom3", "Deleted rule", "Scoring", "3", "15")
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text 1", "Setup", "1", "5"),
            new RuleAtom("atom2", "Rule text 2", "Gameplay", "2", "10")
        };

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: One deletion detected
        diff.Summary.Added.Should().Be(0);
        diff.Summary.Modified.Should().Be(0);
        diff.Summary.Deleted.Should().Be(1);
        diff.Summary.Unchanged.Should().Be(2);
        diff.Summary.TotalChanges.Should().Be(1);

        // Find the deleted atom
        var deletedChange = diff.Changes.FirstOrDefault(c => c.Type == ChangeType.Deleted);
        deletedChange.Should().NotBeNull();
        deletedChange!.OldAtom.Should().Be("atom3");
        deletedChange.NewAtom.Should().BeNull();
        deletedChange.OldValue?.text.Should().Be("Deleted rule");
    }

    /// <summary>
    /// Scenario: Atom text modified
    ///   Given atom exists in both versions with different text
    ///   When computing diff
    ///   Then atom is marked as Modified
    ///   And field change shows text difference
    /// </summary>
    [Fact]
    public void ComputeDiff_WithModifiedText_DetectsModification()
    {
        // Arrange: Atom with modified text
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Original rule text", "Setup", "1", "5")
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Modified rule text", "Setup", "1", "5")
        };

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: One modification detected
        diff.Summary.Added.Should().Be(0);
        diff.Summary.Modified.Should().Be(1);
        diff.Summary.Deleted.Should().Be(0);
        diff.Summary.Unchanged.Should().Be(0);
        diff.Summary.TotalChanges.Should().Be(1);

        // Verify field change details
        var modifiedChange = diff.Changes.First(c => c.Type == ChangeType.Modified);
        modifiedChange.FieldChanges.Should().NotBeNull();
        modifiedChange.FieldChanges!.Should().ContainSingle();

        var fieldChange = modifiedChange.FieldChanges.First();
        fieldChange.FieldName.Should().Be("text");
        fieldChange.OldValue.Should().Be("Original rule text");
        fieldChange.NewValue.Should().Be("Modified rule text");
    }

    /// <summary>
    /// Scenario: Atom section modified
    ///   Given atom exists in both versions with different section
    ///   When computing diff
    ///   Then atom is marked as Modified
    ///   And field change shows section difference
    /// </summary>
    [Fact]
    public void ComputeDiff_WithModifiedSection_DetectsModification()
    {
        // Arrange: Atom with modified section
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text", "Setup", "1", "5")
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text", "Gameplay", "1", "5")
        };

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: Modification with section change
        diff.Summary.Modified.Should().Be(1);

        var modifiedChange = diff.Changes.First(c => c.Type == ChangeType.Modified);
        var fieldChange = modifiedChange.FieldChanges!.First();
        fieldChange.FieldName.Should().Be("section");
        fieldChange.OldValue.Should().Be("Setup");
        fieldChange.NewValue.Should().Be("Gameplay");
    }

    /// <summary>
    /// Scenario: Multiple field modifications on same atom
    ///   Given atom has changes to text, section, page, and line
    ///   When computing diff
    ///   Then all field changes are captured
    /// </summary>
    [Fact]
    public void ComputeDiff_WithMultipleFieldChanges_DetectsAllChanges()
    {
        // Arrange: Atom with multiple field changes
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Original text", "Setup", "1", "5")
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Modified text", "Gameplay", "2", "10")
        };

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: All field changes detected
        var modifiedChange = diff.Changes.First(c => c.Type == ChangeType.Modified);
        modifiedChange.FieldChanges!.Count.Should().Be(4);

        modifiedChange.FieldChanges.Should().Contain(fc => fc.FieldName == "text" && fc.OldValue == "Original text" && fc.NewValue == "Modified text");
        modifiedChange.FieldChanges.Should().Contain(fc => fc.FieldName == "section" && fc.OldValue == "Setup" && fc.NewValue == "Gameplay");
        modifiedChange.FieldChanges.Should().Contain(fc => fc.FieldName == "page" && fc.OldValue == "1" && fc.NewValue == "2");
        modifiedChange.FieldChanges.Should().Contain(fc => fc.FieldName == "line" && fc.OldValue == "5" && fc.NewValue == "10");
    }

    /// <summary>
    /// Scenario: Mixed additions, modifications, and deletions
    ///   Given complex diff with all change types
    ///   When computing diff
    ///   Then summary counts all changes correctly
    /// </summary>
    [Fact]
    public void ComputeDiff_WithMixedChanges_CountsAllChangesCorrectly()
    {
        // Arrange: Complex scenario with additions, modifications, deletions
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Unchanged text", "Setup", "1", "5"),
            new RuleAtom("atom2", "Original text", "Gameplay", "2", "10"),
            new RuleAtom("atom3", "To be deleted", "Scoring", "3", "15")
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Unchanged text", "Setup", "1", "5"),
            new RuleAtom("atom2", "Modified text", "Gameplay", "2", "10"),
            new RuleAtom("atom4", "New atom", "EndGame", "4", "20")
        };

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: All change types counted
        diff.Summary.Added.Should().Be(1);    // atom4 added
        diff.Summary.Modified.Should().Be(1); // atom2 modified
        diff.Summary.Deleted.Should().Be(1);  // atom3 deleted
        diff.Summary.Unchanged.Should().Be(1); // atom1 unchanged
        diff.Summary.TotalChanges.Should().Be(3); // 1 added + 1 modified + 1 deleted

        diff.Changes.Count.Should().Be(4); // Total changes including unchanged
    }

    /// <summary>
    /// Scenario: Empty from version (all additions)
    ///   Given from version has no atoms
    ///   When computing diff against version with atoms
    ///   Then all atoms are marked as Added
    /// </summary>
    [Fact]
    public void ComputeDiff_WithEmptyFromVersion_AllAtomsAreAdded()
    {
        // Arrange: Empty from version
        var fromAtoms = new List<RuleAtom>();
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule 1", "Setup", "1", "5"),
            new RuleAtom("atom2", "Rule 2", "Gameplay", "2", "10")
        };

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: All atoms added
        diff.Summary.Added.Should().Be(2);
        diff.Summary.Modified.Should().Be(0);
        diff.Summary.Deleted.Should().Be(0);
        diff.Summary.Unchanged.Should().Be(0);
        diff.Summary.TotalChanges.Should().Be(2);
    }

    /// <summary>
    /// Scenario: Empty to version (all deletions)
    ///   Given to version has no atoms
    ///   When computing diff from version with atoms
    ///   Then all atoms are marked as Deleted
    /// </summary>
    [Fact]
    public void ComputeDiff_WithEmptyToVersion_AllAtomsAreDeleted()
    {
        // Arrange: Empty to version
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule 1", "Setup", "1", "5"),
            new RuleAtom("atom2", "Rule 2", "Gameplay", "2", "10")
        };
        var toAtoms = new List<RuleAtom>();

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: All atoms deleted
        diff.Summary.Added.Should().Be(0);
        diff.Summary.Modified.Should().Be(0);
        diff.Summary.Deleted.Should().Be(2);
        diff.Summary.Unchanged.Should().Be(0);
        diff.Summary.TotalChanges.Should().Be(2);
    }

    /// <summary>
    /// Scenario: Both versions empty
    ///   Given both versions have no atoms
    ///   When computing diff
    ///   Then summary shows no changes
    /// </summary>
    [Fact]
    public void ComputeDiff_WithBothVersionsEmpty_ReturnsNoChanges()
    {
        // Arrange: Both empty
        var fromAtoms = new List<RuleAtom>();
        var toAtoms = new List<RuleAtom>();

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: No changes
        diff.Summary.TotalChanges.Should().Be(0);
        diff.Changes.Should().BeEmpty();
    }

    /// <summary>
    /// Scenario: Null optional fields handling
    ///   Given atoms with null section, page, and line fields
    ///   When computing diff
    ///   Then null fields are handled correctly
    /// </summary>
    [Fact]
    public void ComputeDiff_WithNullOptionalFields_HandlesNullsCorrectly()
    {
        // Arrange: Atoms with null optional fields
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule with nulls", null, null, null)
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule with nulls", null, null, null)
        };

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: No changes detected despite null fields
        diff.Summary.TotalChanges.Should().Be(0);
        diff.Changes.Should().ContainSingle();
        diff.Changes.First().Type.Should().Be(ChangeType.Unchanged);
    }

    /// <summary>
    /// Scenario: Null to non-null field transition
    ///   Given atom changes from null section to non-null section
    ///   When computing diff
    ///   Then modification is detected with null to value change
    /// </summary>
    [Fact]
    public void ComputeDiff_WithNullToNonNullTransition_DetectsChange()
    {
        // Arrange: Null to non-null transition
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text", null, null, null)
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text", "Setup", "1", "5")
        };

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: Modifications detected
        diff.Summary.Modified.Should().Be(1);

        var modifiedChange = diff.Changes.First(c => c.Type == ChangeType.Modified);
        modifiedChange.FieldChanges!.Count.Should().Be(3);
        modifiedChange.FieldChanges.Should().Contain(fc => fc.FieldName == "section" && fc.OldValue == null && fc.NewValue == "Setup");
        modifiedChange.FieldChanges.Should().Contain(fc => fc.FieldName == "page" && fc.OldValue == null && fc.NewValue == "1");
        modifiedChange.FieldChanges.Should().Contain(fc => fc.FieldName == "line" && fc.OldValue == null && fc.NewValue == "5");
    }

    /// <summary>
    /// Scenario: Atom ordering in diff output
    ///   Given atoms added/modified/deleted in random order
    ///   When computing diff
    ///   Then changes are ordered by atom ID
    /// </summary>
    [Fact]
    public void ComputeDiff_OrdersChangesByAtomId()
    {
        // Arrange: Atoms in non-alphabetical order
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom3", "Rule 3", "Setup", "1", "5"),
            new RuleAtom("atom1", "Rule 1", "Gameplay", "2", "10")
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom2", "Rule 2", "Scoring", "3", "15"),
            new RuleAtom("atom1", "Rule 1", "Gameplay", "2", "10")
        };

        var from = new RuleSpec("chess", "1.0", DateTime.UtcNow, fromAtoms);
        var to = new RuleSpec("chess", "2.0", DateTime.UtcNow.AddDays(1), toAtoms);

        // Act: Compute diff
        var diff = _service.ComputeDiff(from, to);

        // Assert: Changes are ordered by atom ID
        var atomIds = diff.Changes.Select(c => c.OldAtom ?? c.NewAtom).ToList();
        atomIds.Should().BeEquivalentTo(new[] { "atom1", "atom2", "atom3" });
    }

    #endregion

    #region Diff Summary Generation Tests

    /// <summary>
    /// Scenario: Generate summary for diff with no changes
    ///   Given diff with no changes
    ///   When generating summary
    ///   Then summary shows zero changes and no change details
    /// </summary>
    [Fact]
    public void GenerateDiffSummary_WithNoChanges_ReturnsBasicSummary()
    {
        // Arrange: Diff with no changes
        var atoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text", "Setup", "1", "5")
        };
        var from = new RuleSpec("chess", "1.0", new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), atoms);
        var to = new RuleSpec("chess", "1.0", new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), atoms);

        var diff = _service.ComputeDiff(from, to);

        // Act: Generate summary
        var summary = _service.GenerateDiffSummary(diff);

        // Assert: Summary shows no changes
        summary.Should().Contain("Difference between 1.0 and 1.0");
        summary.Should().Contain("Total changes: 0");
        summary.Should().Contain("Added: 0");
        summary.Should().Contain("Modified: 0");
        summary.Should().Contain("Deleted: 0");
        summary.Should().Contain("Unchanged: 1");
        summary.Should().NotContain("Changes:"); // No detailed changes section
    }

    /// <summary>
    /// Scenario: Generate summary for diff with additions
    ///   Given diff with added atoms
    ///   When generating summary
    ///   Then summary includes addition details
    /// </summary>
    [Fact]
    public void GenerateDiffSummary_WithAdditions_IncludesAdditionDetails()
    {
        // Arrange: Diff with addition
        var fromAtoms = new List<RuleAtom>();
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "New rule text", "Setup", "1", "5")
        };

        var from = new RuleSpec("chess", "1.0", new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), fromAtoms);
        var to = new RuleSpec("chess", "2.0", new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc), toAtoms);

        var diff = _service.ComputeDiff(from, to);

        // Act: Generate summary
        var summary = _service.GenerateDiffSummary(diff);

        // Assert: Summary includes addition
        summary.Should().Contain("Total changes: 1");
        summary.Should().Contain("Added: 1");
        summary.Should().Contain("Changes:");
        summary.Should().Contain("+ Added: atom1");
        summary.Should().Contain("Text: New rule text");
        summary.Should().Contain("Section: Setup");
    }

    /// <summary>
    /// Scenario: Generate summary for diff with modifications
    ///   Given diff with modified atoms
    ///   When generating summary
    ///   Then summary includes field-level change details
    /// </summary>
    [Fact]
    public void GenerateDiffSummary_WithModifications_IncludesFieldChanges()
    {
        // Arrange: Diff with modification
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Original text", "Setup", "1", "5")
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Modified text", "Setup", "1", "5")
        };

        var from = new RuleSpec("chess", "1.0", new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), fromAtoms);
        var to = new RuleSpec("chess", "2.0", new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc), toAtoms);

        var diff = _service.ComputeDiff(from, to);

        // Act: Generate summary
        var summary = _service.GenerateDiffSummary(diff);

        // Assert: Summary includes modification details
        summary.Should().Contain("Modified: 1");
        summary.Should().Contain("~ Modified: atom1");
        summary.Should().Contain("text:");
        summary.Should().Contain("- Original text");
        summary.Should().Contain("+ Modified text");
    }

    /// <summary>
    /// Scenario: Generate summary for diff with deletions
    ///   Given diff with deleted atoms
    ///   When generating summary
    ///   Then summary includes deletion details
    /// </summary>
    [Fact]
    public void GenerateDiffSummary_WithDeletions_IncludesDeletionDetails()
    {
        // Arrange: Diff with deletion
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Deleted text", "Scoring", "3", "15")
        };
        var toAtoms = new List<RuleAtom>();

        var from = new RuleSpec("chess", "1.0", new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), fromAtoms);
        var to = new RuleSpec("chess", "2.0", new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc), toAtoms);

        var diff = _service.ComputeDiff(from, to);

        // Act: Generate summary
        var summary = _service.GenerateDiffSummary(diff);

        // Assert: Summary includes deletion
        summary.Should().Contain("Deleted: 1");
        summary.Should().Contain("- Deleted: atom1");
        summary.Should().Contain("Text: Deleted text");
        summary.Should().Contain("Section: Scoring");
    }

    /// <summary>
    /// Scenario: Generate summary with null section handling
    ///   Given diff with atoms having null sections
    ///   When generating summary
    ///   Then null sections are displayed as "(none)"
    /// </summary>
    [Fact]
    public void GenerateDiffSummary_WithNullSection_DisplaysNone()
    {
        // Arrange: Diff with null section
        var fromAtoms = new List<RuleAtom>();
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Rule text", null, null, null)
        };

        var from = new RuleSpec("chess", "1.0", new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), fromAtoms);
        var to = new RuleSpec("chess", "2.0", new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc), toAtoms);

        var diff = _service.ComputeDiff(from, to);

        // Act: Generate summary
        var summary = _service.GenerateDiffSummary(diff);

        // Assert: Null section shown as "(none)"
        summary.Should().Contain("Section: (none)");
    }

    /// <summary>
    /// Scenario: Generate summary excludes unchanged atoms
    ///   Given diff with unchanged atoms
    ///   When generating summary
    ///   Then unchanged atoms are not listed in changes section
    /// </summary>
    [Fact]
    public void GenerateDiffSummary_ExcludesUnchangedAtomsFromDetails()
    {
        // Arrange: Diff with unchanged and modified atoms
        var fromAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Unchanged text", "Setup", "1", "5"),
            new RuleAtom("atom2", "Original text", "Gameplay", "2", "10")
        };
        var toAtoms = new List<RuleAtom>
        {
            new RuleAtom("atom1", "Unchanged text", "Setup", "1", "5"),
            new RuleAtom("atom2", "Modified text", "Gameplay", "2", "10")
        };

        var from = new RuleSpec("chess", "1.0", new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), fromAtoms);
        var to = new RuleSpec("chess", "2.0", new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc), toAtoms);

        var diff = _service.ComputeDiff(from, to);

        // Act: Generate summary
        var summary = _service.GenerateDiffSummary(diff);

        // Assert: Only modified atom in changes section
        summary.Should().Contain("~ Modified: atom2");
        summary.Split("Changes:")[1].Should().NotContain("atom1"); // atom1 not in detailed changes
    }

    #endregion
}

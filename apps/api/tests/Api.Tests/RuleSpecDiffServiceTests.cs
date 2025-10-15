using System;
using System.Collections.Generic;
using System.Linq;
using Api.Models;
using Api.Services;
using Xunit;

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
    private readonly RuleSpecDiffService _service;

    public RuleSpecDiffServiceTests()
    {
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
        Assert.Equal(0, diff.Summary.TotalChanges);
        Assert.Equal(0, diff.Summary.Added);
        Assert.Equal(0, diff.Summary.Modified);
        Assert.Equal(0, diff.Summary.Deleted);
        Assert.Equal(2, diff.Summary.Unchanged);

        // All changes should be marked as Unchanged
        Assert.All(diff.Changes, c => Assert.Equal(ChangeType.Unchanged, c.Type));
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
        Assert.Equal(1, diff.Summary.Added);
        Assert.Equal(0, diff.Summary.Modified);
        Assert.Equal(0, diff.Summary.Deleted);
        Assert.Equal(2, diff.Summary.Unchanged);
        Assert.Equal(1, diff.Summary.TotalChanges);

        // Find the added atom
        var addedChange = diff.Changes.FirstOrDefault(c => c.Type == ChangeType.Added);
        Assert.NotNull(addedChange);
        Assert.Equal("atom3", addedChange!.NewAtom);
        Assert.Null(addedChange.OldAtom);
        Assert.Equal("New rule text", addedChange.NewValue?.text);
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
        Assert.Equal(0, diff.Summary.Added);
        Assert.Equal(0, diff.Summary.Modified);
        Assert.Equal(1, diff.Summary.Deleted);
        Assert.Equal(2, diff.Summary.Unchanged);
        Assert.Equal(1, diff.Summary.TotalChanges);

        // Find the deleted atom
        var deletedChange = diff.Changes.FirstOrDefault(c => c.Type == ChangeType.Deleted);
        Assert.NotNull(deletedChange);
        Assert.Equal("atom3", deletedChange!.OldAtom);
        Assert.Null(deletedChange.NewAtom);
        Assert.Equal("Deleted rule", deletedChange.OldValue?.text);
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
        Assert.Equal(0, diff.Summary.Added);
        Assert.Equal(1, diff.Summary.Modified);
        Assert.Equal(0, diff.Summary.Deleted);
        Assert.Equal(0, diff.Summary.Unchanged);
        Assert.Equal(1, diff.Summary.TotalChanges);

        // Verify field change details
        var modifiedChange = diff.Changes.First(c => c.Type == ChangeType.Modified);
        Assert.NotNull(modifiedChange.FieldChanges);
        Assert.Single(modifiedChange.FieldChanges!);

        var fieldChange = modifiedChange.FieldChanges.First();
        Assert.Equal("text", fieldChange.FieldName);
        Assert.Equal("Original rule text", fieldChange.OldValue);
        Assert.Equal("Modified rule text", fieldChange.NewValue);
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
        Assert.Equal(1, diff.Summary.Modified);

        var modifiedChange = diff.Changes.First(c => c.Type == ChangeType.Modified);
        var fieldChange = modifiedChange.FieldChanges!.First();
        Assert.Equal("section", fieldChange.FieldName);
        Assert.Equal("Setup", fieldChange.OldValue);
        Assert.Equal("Gameplay", fieldChange.NewValue);
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
        Assert.Equal(4, modifiedChange.FieldChanges!.Count);

        Assert.Contains(modifiedChange.FieldChanges, fc => fc.FieldName == "text" && fc.OldValue == "Original text" && fc.NewValue == "Modified text");
        Assert.Contains(modifiedChange.FieldChanges, fc => fc.FieldName == "section" && fc.OldValue == "Setup" && fc.NewValue == "Gameplay");
        Assert.Contains(modifiedChange.FieldChanges, fc => fc.FieldName == "page" && fc.OldValue == "1" && fc.NewValue == "2");
        Assert.Contains(modifiedChange.FieldChanges, fc => fc.FieldName == "line" && fc.OldValue == "5" && fc.NewValue == "10");
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
        Assert.Equal(1, diff.Summary.Added);    // atom4 added
        Assert.Equal(1, diff.Summary.Modified); // atom2 modified
        Assert.Equal(1, diff.Summary.Deleted);  // atom3 deleted
        Assert.Equal(1, diff.Summary.Unchanged); // atom1 unchanged
        Assert.Equal(3, diff.Summary.TotalChanges); // 1 added + 1 modified + 1 deleted

        Assert.Equal(4, diff.Changes.Count); // Total changes including unchanged
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
        Assert.Equal(2, diff.Summary.Added);
        Assert.Equal(0, diff.Summary.Modified);
        Assert.Equal(0, diff.Summary.Deleted);
        Assert.Equal(0, diff.Summary.Unchanged);
        Assert.Equal(2, diff.Summary.TotalChanges);
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
        Assert.Equal(0, diff.Summary.Added);
        Assert.Equal(0, diff.Summary.Modified);
        Assert.Equal(2, diff.Summary.Deleted);
        Assert.Equal(0, diff.Summary.Unchanged);
        Assert.Equal(2, diff.Summary.TotalChanges);
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
        Assert.Equal(0, diff.Summary.TotalChanges);
        Assert.Empty(diff.Changes);
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
        Assert.Equal(0, diff.Summary.TotalChanges);
        Assert.Single(diff.Changes);
        Assert.Equal(ChangeType.Unchanged, diff.Changes.First().Type);
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
        Assert.Equal(1, diff.Summary.Modified);

        var modifiedChange = diff.Changes.First(c => c.Type == ChangeType.Modified);
        Assert.Equal(3, modifiedChange.FieldChanges!.Count);
        Assert.Contains(modifiedChange.FieldChanges, fc => fc.FieldName == "section" && fc.OldValue == null && fc.NewValue == "Setup");
        Assert.Contains(modifiedChange.FieldChanges, fc => fc.FieldName == "page" && fc.OldValue == null && fc.NewValue == "1");
        Assert.Contains(modifiedChange.FieldChanges, fc => fc.FieldName == "line" && fc.OldValue == null && fc.NewValue == "5");
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
        Assert.Equal(new[] { "atom1", "atom2", "atom3" }, atomIds);
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
        Assert.Contains("Difference between 1.0 and 1.0", summary);
        Assert.Contains("Total changes: 0", summary);
        Assert.Contains("Added: 0", summary);
        Assert.Contains("Modified: 0", summary);
        Assert.Contains("Deleted: 0", summary);
        Assert.Contains("Unchanged: 1", summary);
        Assert.DoesNotContain("Changes:", summary); // No detailed changes section
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
        Assert.Contains("Total changes: 1", summary);
        Assert.Contains("Added: 1", summary);
        Assert.Contains("Changes:", summary);
        Assert.Contains("+ Added: atom1", summary);
        Assert.Contains("Text: New rule text", summary);
        Assert.Contains("Section: Setup", summary);
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
        Assert.Contains("Modified: 1", summary);
        Assert.Contains("~ Modified: atom1", summary);
        Assert.Contains("text:", summary);
        Assert.Contains("- Original text", summary);
        Assert.Contains("+ Modified text", summary);
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
        Assert.Contains("Deleted: 1", summary);
        Assert.Contains("- Deleted: atom1", summary);
        Assert.Contains("Text: Deleted text", summary);
        Assert.Contains("Section: Scoring", summary);
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
        Assert.Contains("Section: (none)", summary);
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
        Assert.Contains("~ Modified: atom2", summary);
        Assert.DoesNotContain("atom1", summary.Split("Changes:")[1]); // atom1 not in detailed changes
    }

    #endregion
}

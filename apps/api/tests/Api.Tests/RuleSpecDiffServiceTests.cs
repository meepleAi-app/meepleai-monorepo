using Api.Models;
using Api.Services;
using Xunit;

public class RuleSpecDiffServiceTests
{
    private readonly RuleSpecDiffService _diffService = new();

    [Fact]
    public void ComputeDiff_WithIdenticalSpecs_ReturnsNoChanges()
    {
        // Arrange
        var spec = CreateTestSpec("v1", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section A", "1", "1"),
            new("r2", "Rule 2", "Section B", "2", "1")
        });

        // Act
        var diff = _diffService.ComputeDiff(spec, spec);

        // Assert
        Assert.Equal("v1", diff.FromVersion);
        Assert.Equal("v1", diff.ToVersion);
        Assert.Equal(0, diff.Summary.TotalChanges);
        Assert.Equal(0, diff.Summary.Added);
        Assert.Equal(0, diff.Summary.Modified);
        Assert.Equal(0, diff.Summary.Deleted);
        Assert.Equal(2, diff.Summary.Unchanged);
        Assert.Equal(2, diff.Changes.Count);
        Assert.All(diff.Changes, c => Assert.Equal(ChangeType.Unchanged, c.Type));
    }

    [Fact]
    public void ComputeDiff_WithAddedAtom_DetectsAddition()
    {
        // Arrange
        var from = CreateTestSpec("v1", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section A", "1", "1")
        });

        var to = CreateTestSpec("v2", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section A", "1", "1"),
            new("r2", "Rule 2", "Section B", "2", "1")
        });

        // Act
        var diff = _diffService.ComputeDiff(from, to);

        // Assert
        Assert.Equal(1, diff.Summary.Added);
        Assert.Equal(0, diff.Summary.Modified);
        Assert.Equal(0, diff.Summary.Deleted);
        Assert.Equal(1, diff.Summary.Unchanged);
        Assert.Equal(1, diff.Summary.TotalChanges);

        var added = diff.Changes.FirstOrDefault(c => c.Type == ChangeType.Added);
        Assert.NotNull(added);
        Assert.Equal("r2", added.NewAtom);
        Assert.Null(added.OldAtom);
        Assert.Equal("Rule 2", added.NewValue?.text);
    }

    [Fact]
    public void ComputeDiff_WithDeletedAtom_DetectsDeletion()
    {
        // Arrange
        var from = CreateTestSpec("v1", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section A", "1", "1"),
            new("r2", "Rule 2", "Section B", "2", "1")
        });

        var to = CreateTestSpec("v2", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section A", "1", "1")
        });

        // Act
        var diff = _diffService.ComputeDiff(from, to);

        // Assert
        Assert.Equal(0, diff.Summary.Added);
        Assert.Equal(0, diff.Summary.Modified);
        Assert.Equal(1, diff.Summary.Deleted);
        Assert.Equal(1, diff.Summary.Unchanged);
        Assert.Equal(1, diff.Summary.TotalChanges);

        var deleted = diff.Changes.FirstOrDefault(c => c.Type == ChangeType.Deleted);
        Assert.NotNull(deleted);
        Assert.Equal("r2", deleted.OldAtom);
        Assert.Null(deleted.NewAtom);
        Assert.Equal("Rule 2", deleted.OldValue?.text);
    }

    [Fact]
    public void ComputeDiff_WithModifiedText_DetectsModification()
    {
        // Arrange
        var from = CreateTestSpec("v1", new List<RuleAtom>
        {
            new("r1", "Rule 1 - original", "Section A", "1", "1")
        });

        var to = CreateTestSpec("v2", new List<RuleAtom>
        {
            new("r1", "Rule 1 - updated", "Section A", "1", "1")
        });

        // Act
        var diff = _diffService.ComputeDiff(from, to);

        // Assert
        Assert.Equal(0, diff.Summary.Added);
        Assert.Equal(1, diff.Summary.Modified);
        Assert.Equal(0, diff.Summary.Deleted);
        Assert.Equal(0, diff.Summary.Unchanged);
        Assert.Equal(1, diff.Summary.TotalChanges);

        var modified = diff.Changes.FirstOrDefault(c => c.Type == ChangeType.Modified);
        Assert.NotNull(modified);
        Assert.Equal("r1", modified.OldAtom);
        Assert.Equal("r1", modified.NewAtom);
        Assert.NotNull(modified.FieldChanges);
        Assert.Single(modified.FieldChanges);
        Assert.Equal("text", modified.FieldChanges[0].FieldName);
        Assert.Equal("Rule 1 - original", modified.FieldChanges[0].OldValue);
        Assert.Equal("Rule 1 - updated", modified.FieldChanges[0].NewValue);
    }

    [Fact]
    public void ComputeDiff_WithModifiedSection_DetectsModification()
    {
        // Arrange
        var from = CreateTestSpec("v1", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section A", "1", "1")
        });

        var to = CreateTestSpec("v2", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section B", "1", "1")
        });

        // Act
        var diff = _diffService.ComputeDiff(from, to);

        // Assert
        Assert.Equal(1, diff.Summary.Modified);
        var modified = diff.Changes.FirstOrDefault(c => c.Type == ChangeType.Modified);
        Assert.NotNull(modified);
        Assert.NotNull(modified.FieldChanges);
        Assert.Single(modified.FieldChanges);
        Assert.Equal("section", modified.FieldChanges[0].FieldName);
        Assert.Equal("Section A", modified.FieldChanges[0].OldValue);
        Assert.Equal("Section B", modified.FieldChanges[0].NewValue);
    }

    [Fact]
    public void ComputeDiff_WithMultipleFieldChanges_DetectsAllChanges()
    {
        // Arrange
        var from = CreateTestSpec("v1", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section A", "1", "1")
        });

        var to = CreateTestSpec("v2", new List<RuleAtom>
        {
            new("r1", "Rule 1 - updated", "Section B", "2", "3")
        });

        // Act
        var diff = _diffService.ComputeDiff(from, to);

        // Assert
        Assert.Equal(1, diff.Summary.Modified);
        var modified = diff.Changes.FirstOrDefault(c => c.Type == ChangeType.Modified);
        Assert.NotNull(modified);
        Assert.NotNull(modified.FieldChanges);
        Assert.Equal(4, modified.FieldChanges.Count);
        Assert.Contains(modified.FieldChanges, fc => fc.FieldName == "text");
        Assert.Contains(modified.FieldChanges, fc => fc.FieldName == "section");
        Assert.Contains(modified.FieldChanges, fc => fc.FieldName == "page");
        Assert.Contains(modified.FieldChanges, fc => fc.FieldName == "line");
    }

    [Fact]
    public void ComputeDiff_WithMixedChanges_DetectsAllTypes()
    {
        // Arrange
        var from = CreateTestSpec("v1", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section A", "1", "1"),
            new("r2", "Rule 2", "Section A", "2", "1"),
            new("r3", "Rule 3", "Section B", "3", "1")
        });

        var to = CreateTestSpec("v2", new List<RuleAtom>
        {
            new("r1", "Rule 1 - updated", "Section A", "1", "1"), // Modified
            new("r3", "Rule 3", "Section B", "3", "1"), // Unchanged
            new("r4", "Rule 4", "Section C", "4", "1") // Added
            // r2 is deleted
        });

        // Act
        var diff = _diffService.ComputeDiff(from, to);

        // Assert
        Assert.Equal(1, diff.Summary.Added);
        Assert.Equal(1, diff.Summary.Modified);
        Assert.Equal(1, diff.Summary.Deleted);
        Assert.Equal(1, diff.Summary.Unchanged);
        Assert.Equal(3, diff.Summary.TotalChanges);

        Assert.Contains(diff.Changes, c => c.Type == ChangeType.Added && c.NewAtom == "r4");
        Assert.Contains(diff.Changes, c => c.Type == ChangeType.Modified && c.OldAtom == "r1");
        Assert.Contains(diff.Changes, c => c.Type == ChangeType.Deleted && c.OldAtom == "r2");
        Assert.Contains(diff.Changes, c => c.Type == ChangeType.Unchanged && c.OldAtom == "r3");
    }

    [Fact]
    public void GenerateDiffSummary_WithChanges_ReturnsFormattedSummary()
    {
        // Arrange
        var from = CreateTestSpec("v1", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section A", "1", "1")
        });

        var to = CreateTestSpec("v2", new List<RuleAtom>
        {
            new("r1", "Rule 1 - updated", "Section A", "1", "1")
        });

        var diff = _diffService.ComputeDiff(from, to);

        // Act
        var summary = _diffService.GenerateDiffSummary(diff);

        // Assert
        Assert.Contains("v1", summary);
        Assert.Contains("v2", summary);
        Assert.Contains("Total changes: 1", summary);
        Assert.Contains("Modified: 1", summary);
        Assert.Contains("~ Modified: r1", summary);
        Assert.Contains("text:", summary);
        Assert.Contains("- Rule 1", summary);
        Assert.Contains("+ Rule 1 - updated", summary);
    }

    [Fact]
    public void GenerateDiffSummary_WithNoChanges_ReturnsMinimalSummary()
    {
        // Arrange
        var spec = CreateTestSpec("v1", new List<RuleAtom>
        {
            new("r1", "Rule 1", "Section A", "1", "1")
        });

        var diff = _diffService.ComputeDiff(spec, spec);

        // Act
        var summary = _diffService.GenerateDiffSummary(diff);

        // Assert
        Assert.Contains("v1", summary);
        Assert.Contains("Total changes: 0", summary);
        Assert.DoesNotContain("Changes:", summary);
    }

    [Fact]
    public void GenerateDiffSummary_WithAddedAtom_ShowsAddition()
    {
        // Arrange
        var from = CreateTestSpec("v1", new List<RuleAtom>());
        var to = CreateTestSpec("v2", new List<RuleAtom>
        {
            new("r1", "New Rule", "Section A", "1", "1")
        });

        var diff = _diffService.ComputeDiff(from, to);

        // Act
        var summary = _diffService.GenerateDiffSummary(diff);

        // Assert
        Assert.Contains("+ Added: r1", summary);
        Assert.Contains("Text: New Rule", summary);
        Assert.Contains("Section: Section A", summary);
    }

    [Fact]
    public void GenerateDiffSummary_WithDeletedAtom_ShowsDeletion()
    {
        // Arrange
        var from = CreateTestSpec("v1", new List<RuleAtom>
        {
            new("r1", "Deleted Rule", "Section A", "1", "1")
        });
        var to = CreateTestSpec("v2", new List<RuleAtom>());

        var diff = _diffService.ComputeDiff(from, to);

        // Act
        var summary = _diffService.GenerateDiffSummary(diff);

        // Assert
        Assert.Contains("- Deleted: r1", summary);
        Assert.Contains("Text: Deleted Rule", summary);
        Assert.Contains("Section: Section A", summary);
    }

    private static RuleSpec CreateTestSpec(string version, List<RuleAtom> atoms)
    {
        return new RuleSpec("test-game", version, DateTime.UtcNow, atoms);
    }
}

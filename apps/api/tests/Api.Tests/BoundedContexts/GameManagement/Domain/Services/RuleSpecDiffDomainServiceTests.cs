using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Services;

/// <summary>
/// Tests for the RuleSpecDiffDomainService.
/// Issue #3025: Backend 90% Coverage Target - Phase 15
/// </summary>
[Trait("Category", "Unit")]
public sealed class RuleSpecDiffDomainServiceTests
{
    private readonly RuleSpecDiffDomainService _service;
    private readonly string _gameId = "game-123";
    private readonly DateTime _fromDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _toDate = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    public RuleSpecDiffDomainServiceTests()
    {
        _service = new RuleSpecDiffDomainService();
    }

    #region ComputeDiff Tests - Basic Scenarios

    [Fact]
    public void ComputeDiff_WithIdenticalSpecs_ReturnsNoChanges()
    {
        // Arrange
        var rules = new List<RuleAtom>
        {
            new("r1", "Rule one"),
            new("r2", "Rule two")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, rules);
        var to = new RuleSpec(_gameId, "v2", _toDate, rules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.TotalChanges.Should().Be(0);
        result.Summary.Added.Should().Be(0);
        result.Summary.Modified.Should().Be(0);
        result.Summary.Deleted.Should().Be(0);
        result.Summary.Unchanged.Should().Be(2);
        result.Changes.Should().HaveCount(2);
        result.Changes.Should().AllSatisfy(c => c.Type.Should().Be(ChangeType.Unchanged));
    }

    [Fact]
    public void ComputeDiff_WithEmptySpecs_ReturnsEmptyDiff()
    {
        // Arrange
        var from = new RuleSpec(_gameId, "v1", _fromDate, Array.Empty<RuleAtom>());
        var to = new RuleSpec(_gameId, "v2", _toDate, Array.Empty<RuleAtom>());

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.TotalChanges.Should().Be(0);
        result.Summary.Unchanged.Should().Be(0);
        result.Changes.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_SetsGameIdAndVersionsCorrectly()
    {
        // Arrange
        var from = new RuleSpec(_gameId, "v1", _fromDate, Array.Empty<RuleAtom>());
        var to = new RuleSpec(_gameId, "v2", _toDate, Array.Empty<RuleAtom>());

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.GameId.Should().Be(_gameId);
        result.FromVersion.Should().Be("v1");
        result.ToVersion.Should().Be("v2");
        result.FromCreatedAt.Should().Be(_fromDate);
        result.ToCreatedAt.Should().Be(_toDate);
    }

    #endregion

    #region ComputeDiff Tests - Added Rules

    [Fact]
    public void ComputeDiff_WithAddedRule_DetectsAddition()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Rule one")
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "Rule one"),
            new("r2", "Rule two") // Added
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.TotalChanges.Should().Be(1);
        result.Summary.Added.Should().Be(1);
        result.Summary.Unchanged.Should().Be(1);

        var addedChange = result.Changes.First(c => c.Type == ChangeType.Added);
        addedChange.NewAtom.Should().Be("r2");
        addedChange.OldAtom.Should().BeNull();
        addedChange.NewValue!.text.Should().Be("Rule two");
        addedChange.OldValue.Should().BeNull();
    }

    [Fact]
    public void ComputeDiff_WithMultipleAdditions_CountsAll()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Rule one")
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "Rule one"),
            new("r2", "Rule two"),
            new("r3", "Rule three")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.Added.Should().Be(2);
        result.Summary.TotalChanges.Should().Be(2);
    }

    [Fact]
    public void ComputeDiff_FromEmptyToRules_AllAreAdded()
    {
        // Arrange
        var toRules = new List<RuleAtom>
        {
            new("r1", "Rule one"),
            new("r2", "Rule two")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, Array.Empty<RuleAtom>());
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.Added.Should().Be(2);
        result.Summary.Deleted.Should().Be(0);
        result.Summary.Modified.Should().Be(0);
        result.Summary.Unchanged.Should().Be(0);
    }

    #endregion

    #region ComputeDiff Tests - Deleted Rules

    [Fact]
    public void ComputeDiff_WithDeletedRule_DetectsDeletion()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Rule one"),
            new("r2", "Rule two") // Will be deleted
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "Rule one")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.TotalChanges.Should().Be(1);
        result.Summary.Deleted.Should().Be(1);
        result.Summary.Unchanged.Should().Be(1);

        var deletedChange = result.Changes.First(c => c.Type == ChangeType.Deleted);
        deletedChange.OldAtom.Should().Be("r2");
        deletedChange.NewAtom.Should().BeNull();
        deletedChange.OldValue!.text.Should().Be("Rule two");
        deletedChange.NewValue.Should().BeNull();
    }

    [Fact]
    public void ComputeDiff_FromRulesToEmpty_AllAreDeleted()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Rule one"),
            new("r2", "Rule two")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, Array.Empty<RuleAtom>());

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.Deleted.Should().Be(2);
        result.Summary.Added.Should().Be(0);
        result.Summary.Modified.Should().Be(0);
        result.Summary.Unchanged.Should().Be(0);
    }

    #endregion

    #region ComputeDiff Tests - Modified Rules

    [Fact]
    public void ComputeDiff_WithModifiedText_DetectsModification()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Original text")
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "Modified text")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.Modified.Should().Be(1);
        result.Summary.TotalChanges.Should().Be(1);

        var modifiedChange = result.Changes.First(c => c.Type == ChangeType.Modified);
        modifiedChange.OldAtom.Should().Be("r1");
        modifiedChange.NewAtom.Should().Be("r1");
        modifiedChange.FieldChanges.Should().HaveCount(1);
        modifiedChange.FieldChanges![0].FieldName.Should().Be("text");
        modifiedChange.FieldChanges[0].OldValue.Should().Be("Original text");
        modifiedChange.FieldChanges[0].NewValue.Should().Be("Modified text");
    }

    [Fact]
    public void ComputeDiff_WithModifiedSection_DetectsModification()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Rule text", "Section A")
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "Rule text", "Section B")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.Modified.Should().Be(1);

        var modifiedChange = result.Changes.First(c => c.Type == ChangeType.Modified);
        modifiedChange.FieldChanges.Should().ContainSingle();
        modifiedChange.FieldChanges![0].FieldName.Should().Be("section");
    }

    [Fact]
    public void ComputeDiff_WithModifiedPage_DetectsModification()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Rule text", null, "1")
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "Rule text", null, "5")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.Modified.Should().Be(1);

        var modifiedChange = result.Changes.First(c => c.Type == ChangeType.Modified);
        modifiedChange.FieldChanges.Should().ContainSingle();
        modifiedChange.FieldChanges![0].FieldName.Should().Be("page");
    }

    [Fact]
    public void ComputeDiff_WithModifiedLine_DetectsModification()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Rule text", null, null, "10")
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "Rule text", null, null, "20")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.Modified.Should().Be(1);

        var modifiedChange = result.Changes.First(c => c.Type == ChangeType.Modified);
        modifiedChange.FieldChanges.Should().ContainSingle();
        modifiedChange.FieldChanges![0].FieldName.Should().Be("line");
    }

    [Fact]
    public void ComputeDiff_WithMultipleFieldChanges_TracksAllFields()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Old text", "Old section", "1", "10")
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "New text", "New section", "2", "20")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.Modified.Should().Be(1);

        var modifiedChange = result.Changes.First(c => c.Type == ChangeType.Modified);
        modifiedChange.FieldChanges.Should().HaveCount(4);
        modifiedChange.FieldChanges!.Select(f => f.FieldName)
            .Should().Contain(new[] { "text", "section", "page", "line" });
    }

    [Fact]
    public void ComputeDiff_WithNullToValueChange_TracksNullTransition()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Rule text", "Section A")
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "Rule text", null) // Section removed
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.Modified.Should().Be(1);

        var modifiedChange = result.Changes.First(c => c.Type == ChangeType.Modified);
        modifiedChange.FieldChanges![0].OldValue.Should().Be("Section A");
        modifiedChange.FieldChanges[0].NewValue.Should().BeNull();
    }

    #endregion

    #region ComputeDiff Tests - Mixed Changes

    [Fact]
    public void ComputeDiff_WithMixedChanges_CountsAllCorrectly()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Unchanged rule"),
            new("r2", "Will be modified"),
            new("r3", "Will be deleted")
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "Unchanged rule"),
            new("r2", "Has been modified"),
            new("r4", "New rule") // Added
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        result.Summary.Added.Should().Be(1);
        result.Summary.Modified.Should().Be(1);
        result.Summary.Deleted.Should().Be(1);
        result.Summary.Unchanged.Should().Be(1);
        result.Summary.TotalChanges.Should().Be(3);
        result.Changes.Should().HaveCount(4); // All atoms including unchanged
    }

    #endregion

    #region ComputeDiff Tests - Ordering

    [Fact]
    public void ComputeDiff_OrdersChangesById()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r3", "Rule three"),
            new("r1", "Rule one")
        };
        var toRules = new List<RuleAtom>
        {
            new("r2", "Rule two"),
            new("r3", "Rule three modified")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);

        // Act
        var result = _service.ComputeDiff(from, to);

        // Assert
        var changeIds = result.Changes.Select(c => c.OldAtom ?? c.NewAtom).ToList();
        changeIds.Should().BeInAscendingOrder();
    }

    #endregion

    #region GenerateDiffSummary Tests

    [Fact]
    public void GenerateDiffSummary_WithNoChanges_GeneratesBasicSummary()
    {
        // Arrange
        var rules = new List<RuleAtom> { new("r1", "Rule one") };
        var from = new RuleSpec(_gameId, "v1", _fromDate, rules);
        var to = new RuleSpec(_gameId, "v2", _toDate, rules);
        var diff = _service.ComputeDiff(from, to);

        // Act
        var summary = _service.GenerateDiffSummary(diff);

        // Assert
        summary.Should().Contain("Difference between v1 and v2");
        summary.Should().Contain("Total changes: 0");
        summary.Should().Contain("Unchanged: 1");
        summary.Should().NotContain("Changes:"); // No changes section when no modifications
    }

    [Fact]
    public void GenerateDiffSummary_WithAddedRule_IncludesAddedSection()
    {
        // Arrange
        var fromRules = new List<RuleAtom>();
        var toRules = new List<RuleAtom> { new("r1", "New rule", "Setup") };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);
        var diff = _service.ComputeDiff(from, to);

        // Act
        var summary = _service.GenerateDiffSummary(diff);

        // Assert
        summary.Should().Contain("+ Added: r1");
        summary.Should().Contain("Text: New rule");
        summary.Should().Contain("Section: Setup");
    }

    [Fact]
    public void GenerateDiffSummary_WithDeletedRule_IncludesDeletedSection()
    {
        // Arrange
        var fromRules = new List<RuleAtom> { new("r1", "Old rule", "Gameplay") };
        var toRules = new List<RuleAtom>();
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);
        var diff = _service.ComputeDiff(from, to);

        // Act
        var summary = _service.GenerateDiffSummary(diff);

        // Assert
        summary.Should().Contain("- Deleted: r1");
        summary.Should().Contain("Text: Old rule");
        summary.Should().Contain("Section: Gameplay");
    }

    [Fact]
    public void GenerateDiffSummary_WithModifiedRule_IncludesFieldChanges()
    {
        // Arrange
        var fromRules = new List<RuleAtom> { new("r1", "Original text") };
        var toRules = new List<RuleAtom> { new("r1", "Updated text") };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);
        var diff = _service.ComputeDiff(from, to);

        // Act
        var summary = _service.GenerateDiffSummary(diff);

        // Assert
        summary.Should().Contain("~ Modified: r1");
        summary.Should().Contain("text:");
        summary.Should().Contain("- Original text");
        summary.Should().Contain("+ Updated text");
    }

    [Fact]
    public void GenerateDiffSummary_IncludesTimestamps()
    {
        // Arrange
        var rules = new List<RuleAtom>();
        var from = new RuleSpec(_gameId, "v1", _fromDate, rules);
        var to = new RuleSpec(_gameId, "v2", _toDate, rules);
        var diff = _service.ComputeDiff(from, to);

        // Act
        var summary = _service.GenerateDiffSummary(diff);

        // Assert
        summary.Should().Contain("From: 2024-01-01");
        summary.Should().Contain("To: 2024-01-02");
    }

    [Fact]
    public void GenerateDiffSummary_WithNullSection_ShowsNone()
    {
        // Arrange
        var fromRules = new List<RuleAtom>();
        var toRules = new List<RuleAtom> { new("r1", "New rule", null) };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);
        var diff = _service.ComputeDiff(from, to);

        // Act
        var summary = _service.GenerateDiffSummary(diff);

        // Assert
        summary.Should().Contain("Section: (none)");
    }

    [Fact]
    public void GenerateDiffSummary_WithNullFieldChange_ShowsNull()
    {
        // Arrange
        var fromRules = new List<RuleAtom> { new("r1", "Text", "Section") };
        var toRules = new List<RuleAtom> { new("r1", "Text", null) };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);
        var diff = _service.ComputeDiff(from, to);

        // Act
        var summary = _service.GenerateDiffSummary(diff);

        // Assert
        summary.Should().Contain("+ (null)");
    }

    [Fact]
    public void GenerateDiffSummary_ExcludesUnchangedFromChangesSection()
    {
        // Arrange
        var fromRules = new List<RuleAtom>
        {
            new("r1", "Unchanged rule"),
            new("r2", "Modified rule")
        };
        var toRules = new List<RuleAtom>
        {
            new("r1", "Unchanged rule"),
            new("r2", "Changed rule")
        };
        var from = new RuleSpec(_gameId, "v1", _fromDate, fromRules);
        var to = new RuleSpec(_gameId, "v2", _toDate, toRules);
        var diff = _service.ComputeDiff(from, to);

        // Act
        var summary = _service.GenerateDiffSummary(diff);

        // Assert
        summary.Should().Contain("~ Modified: r2");
        summary.Should().NotContain("Unchanged: r1");
    }

    #endregion
}

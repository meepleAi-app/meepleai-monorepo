using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Services.Pdf;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Domain service tests for TableToAtomicRuleConverter
/// Tests pure business logic without infrastructure dependencies
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class TableToAtomicRuleConverterTests
{
    private readonly TableToAtomicRuleConverter _converter;

    public TableToAtomicRuleConverterTests()
    {
        _converter = new TableToAtomicRuleConverter();
    }
    [Fact]
    public void ConvertTableToAtomicRules_WithValidTable_ReturnsAtomicRules()
    {
        // Arrange
        var table = new PdfTable
        {
            PageNumber = 1,
            Headers = new List<string> { "Action", "Cost", "Effect" },
            Rows = new List<string[]>
            {
                new[] { "Move", "1", "Move player 1 space" },
                new[] { "Attack", "2", "Deal 1 damage" }
            }
        };

        // Act
        var rules = _converter.ConvertTableToAtomicRules(table);

        // Assert
        Assert.NotNull(rules);
        Assert.Equal(2, rules.Count);
        Assert.Contains("[Table on page 1]", rules[0]);
        Assert.Contains("Action: Move", rules[0]);
        Assert.Contains("Cost: 1", rules[0]);
        Assert.Contains("Effect: Move player 1 space", rules[0]);
    }

    [Fact]
    public void ConvertTableToAtomicRules_WithEmptyHeaders_ReturnsEmptyList()
    {
        // Arrange
        var table = new PdfTable
        {
            PageNumber = 1,
            Headers = new List<string>(),
            Rows = new List<string[]>
            {
                new[] { "Move", "1", "Move player 1 space" }
            }
        };

        // Act
        var rules = _converter.ConvertTableToAtomicRules(table);

        // Assert
        Assert.NotNull(rules);
        Assert.Empty(rules);
    }

    [Fact]
    public void ConvertTableToAtomicRules_WithEmptyRows_ReturnsEmptyList()
    {
        // Arrange
        var table = new PdfTable
        {
            PageNumber = 1,
            Headers = new List<string> { "Action", "Cost", "Effect" },
            Rows = new List<string[]>()
        };

        // Act
        var rules = _converter.ConvertTableToAtomicRules(table);

        // Assert
        Assert.NotNull(rules);
        Assert.Empty(rules);
    }

    [Fact]
    public void ConvertTableToAtomicRules_WithNullOrWhitespaceCells_SkipsEmptyCells()
    {
        // Arrange
        var table = new PdfTable
        {
            PageNumber = 2,
            Headers = new List<string> { "Action", "Cost", "Effect" },
            Rows = new List<string[]>
            {
                new[] { "Move", "", "Move player 1 space" },
                new[] { "Attack", "2", "   " }
            }
        };

        // Act
        var rules = _converter.ConvertTableToAtomicRules(table);

        // Assert
        Assert.NotNull(rules);
        Assert.Equal(2, rules.Count);
        Assert.DoesNotContain("Cost:", rules[0]); // Empty cost skipped
        Assert.DoesNotContain("Effect:", rules[1]); // Whitespace effect skipped
    }

    [Fact]
    public void ConvertTableToAtomicRules_WithMismatchedColumnCount_HandlesGracefully()
    {
        // Arrange
        var table = new PdfTable
        {
            PageNumber = 1,
            Headers = new List<string> { "Action", "Cost", "Effect" },
            Rows = new List<string[]>
            {
                new[] { "Move", "1" }, // Missing Effect column
                new[] { "Attack", "2", "Deal 1 damage", "Extra" } // Extra column
            }
        };

        // Act
        var rules = _converter.ConvertTableToAtomicRules(table);

        // Assert
        Assert.NotNull(rules);
        Assert.Equal(2, rules.Count);
        Assert.DoesNotContain("Effect:", rules[0]); // Missing column not included
        Assert.DoesNotContain("Extra", rules[1]); // Extra column ignored
    }

    [Fact]
    public void ConvertTableToAtomicRules_FormatsRulesWithSemicolonSeparator()
    {
        // Arrange
        var table = new PdfTable
        {
            PageNumber = 3,
            Headers = new List<string> { "Name", "Type", "Value" },
            Rows = new List<string[]>
            {
                new[] { "Gold Coin", "Resource", "5" }
            }
        };

        // Act
        var rules = _converter.ConvertTableToAtomicRules(table);

        // Assert
        Assert.NotNull(rules);
        Assert.Single(rules);
        Assert.Matches(@"\[Table on page \d+\] Name: .+; Type: .+; Value: .+", rules[0]);
    }

    [Fact]
    public void ConvertTableToAtomicRules_IncludesPageNumber()
    {
        // Arrange
        var table = new PdfTable
        {
            PageNumber = 42,
            Headers = new List<string> { "Action" },
            Rows = new List<string[]>
            {
                new[] { "Test" }
            }
        };

        // Act
        var rules = _converter.ConvertTableToAtomicRules(table);

        // Assert
        Assert.NotNull(rules);
        Assert.Single(rules);
        Assert.Contains("[Table on page 42]", rules[0]);
    }
    [Fact]
    public void IsHeaderRow_WithExplicitHeaderKeyword_ReturnsTrue()
    {
        // Arrange
        var row = new[] { "Header Name", "Header Type", "Header Value" };

        // Act
        var isHeader = _converter.IsHeaderRow(row);

        // Assert
        Assert.True(isHeader);
    }

    [Fact]
    public void IsHeaderRow_WithCommonHeaderTerms_ReturnsTrue()
    {
        // Arrange
        var row = new[] { "Name", "Type", "Description" };

        // Act
        var isHeader = _converter.IsHeaderRow(row);

        // Assert
        Assert.True(isHeader);
    }

    [Fact]
    public void IsHeaderRow_WithShortCapitalizedCells_ReturnsTrue()
    {
        // Arrange
        var row = new[] { "Action", "Cost", "Effect" };

        // Act
        var isHeader = _converter.IsHeaderRow(row);

        // Assert
        Assert.True(isHeader);
    }

    [Fact]
    public void IsHeaderRow_WithLongDataCells_ReturnsFalse()
    {
        // Arrange
        var row = new[] {
            "Move your player token to any adjacent space",
            "This action costs 1 movement point",
            "You may move orthogonally or diagonally"
        };

        // Act
        var isHeader = _converter.IsHeaderRow(row);

        // Assert
        Assert.False(isHeader);
    }

    [Fact]
    public void IsHeaderRow_WithNullRow_ReturnsFalse()
    {
        // Arrange
        string[]? row = null;

        // Act
        var isHeader = _converter.IsHeaderRow(row!);

        // Assert
        Assert.False(isHeader);
    }

    [Fact]
    public void IsHeaderRow_WithEmptyRow_ReturnsFalse()
    {
        // Arrange
        var row = Array.Empty<string>();

        // Act
        var isHeader = _converter.IsHeaderRow(row);

        // Assert
        Assert.False(isHeader);
    }
    [Fact]
    public void CategorizeAtomicRule_WithSetupTerms_ReturnsSetupCategory()
    {
        // Arrange
        var rule = "[Table on page 1] Action: Setup; Effect: Place 5 tokens on board";

        // Act
        var category = _converter.CategorizeAtomicRule(rule);

        // Assert
        Assert.Equal("Setup", category);
    }

    [Fact]
    public void CategorizeAtomicRule_WithActionTerms_ReturnsActionCategory()
    {
        // Arrange
        var rule = "[Table on page 2] Action: Move; Cost: 1; Effect: Move player 1 space";

        // Act
        var category = _converter.CategorizeAtomicRule(rule);

        // Assert
        Assert.Equal("Action", category);
    }

    [Fact]
    public void CategorizeAtomicRule_WithScoringTerms_ReturnsScoringCategory()
    {
        // Arrange
        var rule = "[Table on page 3] Condition: Game end; Points: 5 per resource";

        // Act
        var category = _converter.CategorizeAtomicRule(rule);

        // Assert
        Assert.Equal("Scoring", category);
    }

    [Fact]
    public void CategorizeAtomicRule_WithEndGameTerms_ReturnsEndGameCategory()
    {
        // Arrange
        var rule = "[Table on page 4] Condition: Game over when deck is empty";

        // Act
        var category = _converter.CategorizeAtomicRule(rule);

        // Assert
        Assert.Equal("EndGame", category);
    }

    [Fact]
    public void CategorizeAtomicRule_WithComponentTerms_ReturnsComponentsCategory()
    {
        // Arrange
        var rule = "[Table on page 1] Component: Card; Quantity: 52";

        // Act
        var category = _converter.CategorizeAtomicRule(rule);

        // Assert
        Assert.Equal("Components", category);
    }

    [Fact]
    public void CategorizeAtomicRule_WithNoSpecificTerms_ReturnsGeneralCategory()
    {
        // Arrange
        var rule = "[Table on page 5] Rule: Follow all instructions carefully";

        // Act
        var category = _converter.CategorizeAtomicRule(rule);

        // Assert
        Assert.Equal("General", category);
    }

    [Fact]
    public void CategorizeAtomicRule_WithNullOrEmpty_ReturnsUnknownCategory()
    {
        // Arrange & Act
        var category1 = _converter.CategorizeAtomicRule(null!);
        var category2 = _converter.CategorizeAtomicRule("");
        var category3 = _converter.CategorizeAtomicRule("   ");

        // Assert
        Assert.Equal("Unknown", category1);
        Assert.Equal("Unknown", category2);
        Assert.Equal("Unknown", category3);
    }

    [Fact]
    public void CategorizeAtomicRule_IsCaseInsensitive()
    {
        // Arrange
        var rule1 = "[Table on page 1] ACTION: Move";
        var rule2 = "[Table on page 1] action: move";
        var rule3 = "[Table on page 1] AcTiOn: MoVe";

        // Act
        var category1 = _converter.CategorizeAtomicRule(rule1);
        var category2 = _converter.CategorizeAtomicRule(rule2);
        var category3 = _converter.CategorizeAtomicRule(rule3);

        // Assert
        Assert.Equal("Action", category1);
        Assert.Equal("Action", category2);
        Assert.Equal("Action", category3);
    }
    [Fact]
    public void ConvertTableToAtomicRules_WithComplexTableData_PreservesBusinnessLogic()
    {
        // Arrange: Real-world game rule table
        var table = new PdfTable
        {
            PageNumber = 7,
            Headers = new List<string> { "Phase", "Actions Available", "Player Count", "Time Limit" },
            Rows = new List<string[]>
            {
                new[] { "Draw", "Draw 2 cards", "2-4", "30 seconds" },
                new[] { "Action", "Play 1 card, Use 1 ability", "2-4", "2 minutes" },
                new[] { "Cleanup", "Discard to hand limit", "2-4", "30 seconds" }
            }
        };

        // Act
        var rules = _converter.ConvertTableToAtomicRules(table);

        // Assert: Verify business logic preservation
        Assert.Equal(3, rules.Count);
        Assert.All(rules, rule => Assert.Contains("[Table on page 7]", rule));
        Assert.All(rules, rule => Assert.Contains("Phase:", rule));
        Assert.All(rules, rule => Assert.Contains("Actions Available:", rule));
        Assert.All(rules, rule => Assert.Contains("Player Count: 2-4", rule));
    }

    [Fact]
    public void ConvertTableToAtomicRules_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var table = new PdfTable
        {
            PageNumber = 1,
            Headers = new List<string> { "Action", "Symbol", "Effect" },
            Rows = new List<string[]>
            {
                new[] { "Attack", "⚔️", "Deal 2 damage; Then draw 1 card" },
                new[] { "Defend", "🛡️", "Block 3 damage & heal 1 HP" }
            }
        };

        // Act
        var rules = _converter.ConvertTableToAtomicRules(table);

        // Assert
        Assert.Equal(2, rules.Count);
        Assert.Contains("⚔️", rules[0]);
        Assert.Contains("🛡️", rules[1]);
        Assert.Contains("&", rules[1]); // Special characters preserved
    }
}

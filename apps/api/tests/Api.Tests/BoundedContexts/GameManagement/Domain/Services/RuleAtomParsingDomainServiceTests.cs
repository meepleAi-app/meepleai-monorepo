using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Services;

/// <summary>
/// Tests for the RuleAtomParsingDomainService.
/// Issue #3025: Backend 90% Coverage Target - Phase 15
/// </summary>
[Trait("Category", "Unit")]
public sealed class RuleAtomParsingDomainServiceTests
{
    private readonly RuleAtomParsingDomainService _service;

    public RuleAtomParsingDomainServiceTests()
    {
        _service = new RuleAtomParsingDomainService();
    }

    #region ParseAtomicRulesFromJson Tests

    [Fact]
    public void ParseAtomicRulesFromJson_WithValidJsonArray_ReturnsRules()
    {
        // Arrange
        var json = "[\"Rule 1\", \"Rule 2\", \"Rule 3\"]";

        // Act
        var result = _service.ParseAtomicRulesFromJson(json);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be("Rule 1");
        result[1].Should().Be("Rule 2");
        result[2].Should().Be("Rule 3");
    }

    [Fact]
    public void ParseAtomicRulesFromJson_WithNullInput_ReturnsEmptyList()
    {
        // Act
        var result = _service.ParseAtomicRulesFromJson(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseAtomicRulesFromJson_WithEmptyString_ReturnsEmptyList()
    {
        // Act
        var result = _service.ParseAtomicRulesFromJson("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseAtomicRulesFromJson_WithWhitespaceOnly_ReturnsEmptyList()
    {
        // Act
        var result = _service.ParseAtomicRulesFromJson("   ");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseAtomicRulesFromJson_WithInvalidJson_ReturnsEmptyList()
    {
        // Arrange
        var invalidJson = "not valid json";

        // Act
        var result = _service.ParseAtomicRulesFromJson(invalidJson);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseAtomicRulesFromJson_WithEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var json = "[]";

        // Act
        var result = _service.ParseAtomicRulesFromJson(json);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseAtomicRulesFromJson_WithNullInArray_FiltersOutNulls()
    {
        // Arrange
        var json = "[\"Rule 1\", null, \"Rule 2\"]";

        // Act
        var result = _service.ParseAtomicRulesFromJson(json);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("Rule 1");
        result.Should().Contain("Rule 2");
    }

    [Fact]
    public void ParseAtomicRulesFromJson_WithEmptyStringsInArray_FiltersOutEmpty()
    {
        // Arrange
        var json = "[\"Rule 1\", \"\", \"  \", \"Rule 2\"]";

        // Act
        var result = _service.ParseAtomicRulesFromJson(json);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("Rule 1");
        result.Should().Contain("Rule 2");
    }

    [Fact]
    public void ParseAtomicRulesFromJson_WithWhitespaceInRules_TrimsWhitespace()
    {
        // Arrange
        var json = "[\"  Rule 1  \", \"Rule 2   \"]";

        // Act
        var result = _service.ParseAtomicRulesFromJson(json);

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be("Rule 1");
        result[1].Should().Be("Rule 2");
    }

    [Fact]
    public void ParseAtomicRulesFromJson_WithMalformedArray_ReturnsEmptyList()
    {
        // Arrange
        var json = "[\"Rule 1\",";

        // Act
        var result = _service.ParseAtomicRulesFromJson(json);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseAtomicRulesFromJson_WithObjectInsteadOfArray_ReturnsEmptyList()
    {
        // Arrange
        var json = "{\"rule\": \"Rule 1\"}";

        // Act
        var result = _service.ParseAtomicRulesFromJson(json);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ParseRulesFromExtractedText Tests

    [Fact]
    public void ParseRulesFromExtractedText_WithMultipleLines_ReturnsAllLines()
    {
        // Arrange
        var text = "Rule 1\nRule 2\nRule 3";

        // Act
        var result = _service.ParseRulesFromExtractedText(text);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be("Rule 1");
        result[1].Should().Be("Rule 2");
        result[2].Should().Be("Rule 3");
    }

    [Fact]
    public void ParseRulesFromExtractedText_WithNullInput_ReturnsEmptyList()
    {
        // Act
        var result = _service.ParseRulesFromExtractedText(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseRulesFromExtractedText_WithEmptyString_ReturnsEmptyList()
    {
        // Act
        var result = _service.ParseRulesFromExtractedText("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseRulesFromExtractedText_WithWhitespaceOnly_ReturnsEmptyList()
    {
        // Act
        var result = _service.ParseRulesFromExtractedText("   \n   \n   ");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseRulesFromExtractedText_WithWindowsLineEndings_SplitsCorrectly()
    {
        // Arrange
        var text = "Rule 1\r\nRule 2\r\nRule 3";

        // Act
        var result = _service.ParseRulesFromExtractedText(text);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void ParseRulesFromExtractedText_WithMixedLineEndings_SplitsCorrectly()
    {
        // Arrange
        var text = "Rule 1\nRule 2\r\nRule 3\rRule 4";

        // Act
        var result = _service.ParseRulesFromExtractedText(text);

        // Assert
        result.Should().HaveCount(4);
    }

    [Fact]
    public void ParseRulesFromExtractedText_WithEmptyLines_FiltersOutEmpty()
    {
        // Arrange
        var text = "Rule 1\n\nRule 2\n\n\nRule 3";

        // Act
        var result = _service.ParseRulesFromExtractedText(text);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void ParseRulesFromExtractedText_TrimsWhitespaceFromLines()
    {
        // Arrange
        var text = "  Rule 1  \n   Rule 2\nRule 3   ";

        // Act
        var result = _service.ParseRulesFromExtractedText(text);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be("Rule 1");
        result[1].Should().Be("Rule 2");
        result[2].Should().Be("Rule 3");
    }

    [Fact]
    public void ParseRulesFromExtractedText_WithSingleLine_ReturnsSingleItem()
    {
        // Arrange
        var text = "Single rule";

        // Act
        var result = _service.ParseRulesFromExtractedText(text);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be("Single rule");
    }

    #endregion

    #region CreateRuleAtom Tests

    [Fact]
    public void CreateRuleAtom_WithSimpleText_CreatesAtomWithNoPage()
    {
        // Arrange
        var text = "Players take turns clockwise.";

        // Act
        var result = _service.CreateRuleAtom(text, 0);

        // Assert
        result.Id.Should().Be("r0");
        result.Text.Should().Be("Players take turns clockwise.");
        result.Page.Should().BeNull();
        result.Section.Should().BeNull();
        result.Line.Should().BeNull();
    }

    [Fact]
    public void CreateRuleAtom_WithTablePagePattern_ExtractsPageNumber()
    {
        // Arrange
        var text = "[Table on page 5] Player setup chart.";

        // Act
        var result = _service.CreateRuleAtom(text, 1);

        // Assert
        result.Id.Should().Be("r1");
        result.Text.Should().Be("Player setup chart.");
        result.Page.Should().Be("5");
    }

    [Fact]
    public void CreateRuleAtom_WithPageReferenceInText_ExtractsPageNumber()
    {
        // Arrange
        var text = "See the scoring rules on page 12 for details.";

        // Act
        var result = _service.CreateRuleAtom(text, 2);

        // Assert
        result.Id.Should().Be("r2");
        result.Text.Should().Be("See the scoring rules on page 12 for details.");
        result.Page.Should().Be("12");
    }

    [Fact]
    public void CreateRuleAtom_WithCaseInsensitiveTablePattern_ExtractsPage()
    {
        // Arrange
        var text = "[TABLE ON PAGE 3] Component list.";

        // Act
        var result = _service.CreateRuleAtom(text, 0);

        // Assert
        result.Page.Should().Be("3");
        result.Text.Should().Be("Component list.");
    }

    [Fact]
    public void CreateRuleAtom_WithCaseInsensitivePagePattern_ExtractsPage()
    {
        // Arrange
        var text = "Refer to PAGE 7 for examples.";

        // Act
        var result = _service.CreateRuleAtom(text, 0);

        // Assert
        result.Page.Should().Be("7");
    }

    [Fact]
    public void CreateRuleAtom_WithMultiplePageReferences_ExtractsFirst()
    {
        // Arrange
        var text = "See page 3 and page 5 for details.";

        // Act
        var result = _service.CreateRuleAtom(text, 0);

        // Assert
        result.Page.Should().Be("3");
    }

    [Fact]
    public void CreateRuleAtom_IncrementingIndex_GeneratesCorrectIds()
    {
        // Arrange & Act
        var result0 = _service.CreateRuleAtom("Rule one", 0);
        var result1 = _service.CreateRuleAtom("Rule two", 1);
        var result99 = _service.CreateRuleAtom("Rule hundred", 99);

        // Assert
        result0.Id.Should().Be("r0");
        result1.Id.Should().Be("r1");
        result99.Id.Should().Be("r99");
    }

    [Fact]
    public void CreateRuleAtom_TrimsWhitespace()
    {
        // Arrange
        var text = "   Trimmed rule text   ";

        // Act
        var result = _service.CreateRuleAtom(text, 0);

        // Assert
        result.Text.Should().Be("Trimmed rule text");
    }

    [Fact]
    public void CreateRuleAtom_WithTablePatternAndExtraWhitespace_TrimsResult()
    {
        // Arrange
        var text = "[Table on page 1]    Spaced content   ";

        // Act
        var result = _service.CreateRuleAtom(text, 0);

        // Assert
        result.Text.Should().Be("Spaced content");
        result.Page.Should().Be("1");
    }

    [Fact]
    public void CreateRuleAtom_WithPagePatternButNoMatch_PageIsNull()
    {
        // Arrange
        var text = "No page reference here.";

        // Act
        var result = _service.CreateRuleAtom(text, 0);

        // Assert
        result.Page.Should().BeNull();
    }

    [Fact]
    public void CreateRuleAtom_WithMultiDigitPageNumber_ExtractsCorrectly()
    {
        // Arrange
        var text = "[Table on page 123] Large rulebook reference.";

        // Act
        var result = _service.CreateRuleAtom(text, 0);

        // Assert
        result.Page.Should().Be("123");
    }

    [Fact]
    public void CreateRuleAtom_TablePatternPrioritizedOverPagePattern()
    {
        // Arrange - Table pattern at start should be extracted and removed
        var text = "[Table on page 5] Content referencing page 10.";

        // Act
        var result = _service.CreateRuleAtom(text, 0);

        // Assert
        result.Page.Should().Be("5"); // Table pattern takes priority
        result.Text.Should().Be("Content referencing page 10.");
    }

    #endregion
}

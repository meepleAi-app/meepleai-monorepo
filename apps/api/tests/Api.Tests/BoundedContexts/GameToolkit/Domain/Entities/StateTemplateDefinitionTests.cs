using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class StateTemplateDefinitionTests
{
    [Fact]
    public void Constructor_WithRequiredParams_CreatesTemplate()
    {
        var template = new StateTemplateDefinition("Chess Setup", TemplateCategory.Strategy, "{\"tools\":[]}");

        template.Name.Should().Be("Chess Setup");
        template.Category.Should().Be(TemplateCategory.Strategy);
        template.SchemaJson.Should().Be("{\"tools\":[]}");
        template.Description.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParams_CreatesFullTemplate()
    {
        var template = new StateTemplateDefinition(
            "Party Game Setup",
            TemplateCategory.Party,
            "{\"tools\":[{\"type\":\"timer\"}]}",
            "A fun party game template");

        template.Name.Should().Be("Party Game Setup");
        template.Category.Should().Be(TemplateCategory.Party);
        template.SchemaJson.Should().Be("{\"tools\":[{\"type\":\"timer\"}]}");
        template.Description.Should().Be("A fun party game template");
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        var act = () =>
            new StateTemplateDefinition("", TemplateCategory.Strategy, "{}");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsArgumentException()
    {
        var act2 = () =>
            new StateTemplateDefinition("   ", TemplateCategory.Strategy, "{}");
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithLongName_ThrowsArgumentException()
    {
        var longName = new string('A', 201);
        var act3 = () =>
            new StateTemplateDefinition(longName, TemplateCategory.Strategy, "{}");
        act3.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_With200CharName_Succeeds()
    {
        var name = new string('A', 200);
        var template = new StateTemplateDefinition(name, TemplateCategory.Strategy, "{}");
        template.Name.Length.Should().Be(200);
    }

    [Fact]
    public void Constructor_WithEmptySchema_ThrowsArgumentException()
    {
        var act4 = () =>
            new StateTemplateDefinition("Test", TemplateCategory.Strategy, "");
        act4.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceSchema_ThrowsArgumentException()
    {
        var act5 = () =>
            new StateTemplateDefinition("Test", TemplateCategory.Strategy, "   ");
        act5.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithInvalidJson_ThrowsArgumentException()
    {
        var act6 = () =>
            new StateTemplateDefinition("Test", TemplateCategory.Strategy, "not valid json");
        act6.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithValidComplexJson_Succeeds()
    {
        var json = "{\"tools\":[{\"type\":\"dice\",\"count\":2}],\"rules\":{\"maxPlayers\":4}}";
        var template = new StateTemplateDefinition("Test", TemplateCategory.Strategy, json);
        template.SchemaJson.Should().Be(json);
    }

    [Fact]
    public void Constructor_TrimsNameAndDescription()
    {
        var template = new StateTemplateDefinition(
            "  My Template  ", TemplateCategory.CardGames, "{}", "  A description  ");

        template.Name.Should().Be("My Template");
        template.Description.Should().Be("A description");
    }

    [Fact]
    public void Constructor_WithNullDescription_AllowsNull()
    {
        var template = new StateTemplateDefinition("Test", TemplateCategory.Cooperative, "{}");
        template.Description.Should().BeNull();
    }

    [Fact]
    public void Constructor_AllCategories_Succeed()
    {
        foreach (var category in Enum.GetValues<TemplateCategory>())
        {
            var template = new StateTemplateDefinition("Test", category, "{}");
            template.Category.Should().Be(category);
        }
    }
}

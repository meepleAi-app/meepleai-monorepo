using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class StateTemplateDefinitionTests
{
    [Fact]
    public void Constructor_WithRequiredParams_CreatesTemplate()
    {
        var template = new StateTemplateDefinition("Chess Setup", TemplateCategory.Strategy, "{\"tools\":[]}");

        Assert.Equal("Chess Setup", template.Name);
        Assert.Equal(TemplateCategory.Strategy, template.Category);
        Assert.Equal("{\"tools\":[]}", template.SchemaJson);
        Assert.Null(template.Description);
    }

    [Fact]
    public void Constructor_WithAllParams_CreatesFullTemplate()
    {
        var template = new StateTemplateDefinition(
            "Party Game Setup",
            TemplateCategory.Party,
            "{\"tools\":[{\"type\":\"timer\"}]}",
            "A fun party game template");

        Assert.Equal("Party Game Setup", template.Name);
        Assert.Equal(TemplateCategory.Party, template.Category);
        Assert.Equal("{\"tools\":[{\"type\":\"timer\"}]}", template.SchemaJson);
        Assert.Equal("A fun party game template", template.Description);
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new StateTemplateDefinition("", TemplateCategory.Strategy, "{}"));
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new StateTemplateDefinition("   ", TemplateCategory.Strategy, "{}"));
    }

    [Fact]
    public void Constructor_WithLongName_ThrowsArgumentException()
    {
        var longName = new string('A', 201);
        Assert.Throws<ArgumentException>(() =>
            new StateTemplateDefinition(longName, TemplateCategory.Strategy, "{}"));
    }

    [Fact]
    public void Constructor_With200CharName_Succeeds()
    {
        var name = new string('A', 200);
        var template = new StateTemplateDefinition(name, TemplateCategory.Strategy, "{}");
        Assert.Equal(200, template.Name.Length);
    }

    [Fact]
    public void Constructor_WithEmptySchema_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new StateTemplateDefinition("Test", TemplateCategory.Strategy, ""));
    }

    [Fact]
    public void Constructor_WithWhitespaceSchema_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new StateTemplateDefinition("Test", TemplateCategory.Strategy, "   "));
    }

    [Fact]
    public void Constructor_WithInvalidJson_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new StateTemplateDefinition("Test", TemplateCategory.Strategy, "not valid json"));
    }

    [Fact]
    public void Constructor_WithValidComplexJson_Succeeds()
    {
        var json = "{\"tools\":[{\"type\":\"dice\",\"count\":2}],\"rules\":{\"maxPlayers\":4}}";
        var template = new StateTemplateDefinition("Test", TemplateCategory.Strategy, json);
        Assert.Equal(json, template.SchemaJson);
    }

    [Fact]
    public void Constructor_TrimsNameAndDescription()
    {
        var template = new StateTemplateDefinition(
            "  My Template  ", TemplateCategory.CardGames, "{}", "  A description  ");

        Assert.Equal("My Template", template.Name);
        Assert.Equal("A description", template.Description);
    }

    [Fact]
    public void Constructor_WithNullDescription_AllowsNull()
    {
        var template = new StateTemplateDefinition("Test", TemplateCategory.Cooperative, "{}");
        Assert.Null(template.Description);
    }

    [Fact]
    public void Constructor_AllCategories_Succeed()
    {
        foreach (var category in Enum.GetValues<TemplateCategory>())
        {
            var template = new StateTemplateDefinition("Test", category, "{}");
            Assert.Equal(category, template.Category);
        }
    }
}

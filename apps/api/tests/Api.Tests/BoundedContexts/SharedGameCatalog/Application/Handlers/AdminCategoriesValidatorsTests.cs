using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AdminCategories;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Validator tests for the admin-categories CRUD commands (#1440).
/// Covers AC-5: Name 1..50, Slug pattern, Emoji 1..16, Color hex.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1440")]
public sealed class AdminCategoriesValidatorsTests
{
    private static CreateGameCategoryCommand ValidCreate(
        string name = "Strategy",
        string slug = "strategy",
        string? emoji = "🎯",
        string? color = "#ef4444",
        Guid? actor = null) =>
        new(name, slug, emoji, color, actor ?? Guid.NewGuid());

    private static UpdateGameCategoryCommand ValidUpdate(
        Guid? id = null,
        string name = "Strategy",
        string slug = "strategy",
        string? emoji = "🎯",
        string? color = "#ef4444",
        Guid? actor = null) =>
        new(id ?? Guid.NewGuid(), name, slug, emoji, color, actor ?? Guid.NewGuid());

    // ── CreateGameCategoryCommandValidator ─────────────────────────────

    [Fact]
    public void Create_WithValidCommand_PassesValidation()
    {
        var validator = new CreateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidCreate());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_FailsValidation(string name)
    {
        var validator = new CreateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidCreate(name: name));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Create_WithNameOver50Chars_FailsValidation()
    {
        var validator = new CreateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidCreate(name: new string('a', 51)));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("CamelCase")]
    [InlineData("with spaces")]
    [InlineData("With_Underscore")]
    [InlineData("starts-with-hyphen-")]
    public void Create_WithInvalidSlugFormat_FailsValidation(string slug)
    {
        var validator = new CreateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidCreate(slug: slug));
        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Theory]
    [InlineData("strategy")]
    [InlineData("deck-building")]
    [InlineData("euro123")]
    public void Create_WithValidSlugFormat_PassesValidation(string slug)
    {
        var validator = new CreateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidCreate(slug: slug));
        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }

    [Theory]
    [InlineData("#ef4444")]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    public void Create_WithValidHexColor_PassesValidation(string color)
    {
        var validator = new CreateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidCreate(color: color));
        result.ShouldNotHaveValidationErrorFor(x => x.Color);
    }

    [Theory]
    [InlineData("ef4444")]       // missing #
    [InlineData("#ef44")]         // too short
    [InlineData("#GGGGGG")]       // not hex
    [InlineData("red")]            // colour name
    public void Create_WithInvalidColor_FailsValidation(string color)
    {
        var validator = new CreateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidCreate(color: color));
        result.ShouldHaveValidationErrorFor(x => x.Color);
    }

    [Fact]
    public void Create_WithNullEmojiAndColor_PassesValidation()
    {
        var validator = new CreateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidCreate(emoji: null, color: null));
        result.ShouldNotHaveValidationErrorFor(x => x.Emoji);
        result.ShouldNotHaveValidationErrorFor(x => x.Color);
    }

    [Fact]
    public void Create_WithEmptyActor_FailsValidation()
    {
        var validator = new CreateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidCreate(actor: Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.ActorUserId);
    }

    // ── UpdateGameCategoryCommandValidator ─────────────────────────────

    [Fact]
    public void Update_WithValidCommand_PassesValidation()
    {
        var validator = new UpdateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidUpdate());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_WithEmptyId_FailsValidation()
    {
        var validator = new UpdateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidUpdate(id: Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Update_WithInvalidColor_FailsValidation()
    {
        var validator = new UpdateGameCategoryCommandValidator();
        var result = validator.TestValidate(ValidUpdate(color: "not-a-color"));
        result.ShouldHaveValidationErrorFor(x => x.Color);
    }

    // ── DeleteGameCategoryCommandValidator ─────────────────────────────

    [Fact]
    public void Delete_WithValidCommand_PassesValidation()
    {
        var validator = new DeleteGameCategoryCommandValidator();
        var result = validator.TestValidate(new DeleteGameCategoryCommand(Guid.NewGuid(), Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Delete_WithEmptyId_FailsValidation()
    {
        var validator = new DeleteGameCategoryCommandValidator();
        var result = validator.TestValidate(new DeleteGameCategoryCommand(Guid.Empty, Guid.NewGuid()));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Delete_WithEmptyActor_FailsValidation()
    {
        var validator = new DeleteGameCategoryCommandValidator();
        var result = validator.TestValidate(new DeleteGameCategoryCommand(Guid.NewGuid(), Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.ActorUserId);
    }
}

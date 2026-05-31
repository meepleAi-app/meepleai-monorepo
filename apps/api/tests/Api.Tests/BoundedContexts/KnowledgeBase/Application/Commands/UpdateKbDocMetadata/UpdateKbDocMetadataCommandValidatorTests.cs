using Api.BoundedContexts.KnowledgeBase.Application.Commands.UpdateKbDocMetadata;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands.UpdateKbDocMetadata;

/// <summary>
/// Issue #1687 Task 5 — validator unit tests for <see cref="UpdateKbDocMetadataCommand"/>.
///
/// Each field is gated by <c>.When(x => x.Field is not null)</c> so a JSON null
/// (partial update) is a no-op at validation time (D-4). The handler downstream
/// distinguishes "absent" (no-op) from "explicit empty" (clear) via the actual
/// nullable string / empty-list semantics.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1687")]
public class UpdateKbDocMetadataCommandValidatorTests
{
    private readonly UpdateKbDocMetadataCommandValidator _validator = new();

    private static UpdateKbDocMetadataCommand Cmd(
        Guid? docId = null,
        Guid? editorId = null,
        string editorRole = "Owner",
        string? title = null,
        string? documentType = null,
        string? language = null,
        IReadOnlyList<string>? tags = null) => new(
            DocId: docId ?? Guid.NewGuid(),
            EditorUserId: editorId ?? Guid.NewGuid(),
            EditorRole: editorRole,
            Title: title,
            DocumentType: documentType,
            Language: language,
            Tags: tags);

    [Fact]
    public void DocId_Empty_Fails()
    {
        var result = _validator.TestValidate(Cmd(docId: Guid.Empty));
        result.ShouldHaveValidationErrorFor(c => c.DocId);
    }

    [Fact]
    public void EditorUserId_Empty_Fails()
    {
        var result = _validator.TestValidate(Cmd(editorId: Guid.Empty));
        result.ShouldHaveValidationErrorFor(c => c.EditorUserId);
    }

    [Fact]
    public void AllFieldsNull_Passes_NoOp()
    {
        var result = _validator.TestValidate(Cmd());
        result.IsValid.Should().BeTrue("D-4: JSON null = no-op, valid command at validator level");
    }

    // ── Title ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Title_Null_Passes()
    {
        var result = _validator.TestValidate(Cmd(title: null));
        result.ShouldNotHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void Title_Over200_Fails()
    {
        var result = _validator.TestValidate(Cmd(title: new string('a', 201)));
        result.ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void Title_AllWhitespace_Passes()
    {
        // Handler interprets whitespace as "clear" (SetTitle("   ", ...) → Title=null).
        // The validator does NOT reject because '<=200 chars' is satisfied.
        var result = _validator.TestValidate(Cmd(title: "   "));
        result.ShouldNotHaveValidationErrorFor(c => c.Title);
    }

    // ── DocumentType ───────────────────────────────────────────────────────────

    [Fact]
    public void DocumentType_Null_Passes()
    {
        var result = _validator.TestValidate(Cmd(documentType: null));
        result.ShouldNotHaveValidationErrorFor(c => c.DocumentType);
    }

    [Fact]
    public void DocumentType_NotInEnum_Fails()
    {
        var result = _validator.TestValidate(Cmd(documentType: "FAQ"));
        result.ShouldHaveValidationErrorFor(c => c.DocumentType);
    }

    [Fact]
    public void DocumentType_GuideIsAlsoInvalid_Fails()
    {
        var result = _validator.TestValidate(Cmd(documentType: "Guide"));
        result.ShouldHaveValidationErrorFor(c => c.DocumentType);
    }

    [Fact]
    public void DocumentType_CanonicalForm_Passes()
    {
        var result = _validator.TestValidate(Cmd(documentType: "Rulebook"));
        result.ShouldNotHaveValidationErrorFor(c => c.DocumentType);
    }

    [Fact]
    public void DocumentType_CaseInsensitive_Passes()
    {
        // D-6: case-insensitive accept; handler canonicalizes to PascalCase form.
        var result = _validator.TestValidate(Cmd(documentType: "rulebook"));
        result.ShouldNotHaveValidationErrorFor(c => c.DocumentType);
    }

    // ── Language ───────────────────────────────────────────────────────────────

    [Fact]
    public void Language_Null_Passes()
    {
        var result = _validator.TestValidate(Cmd(language: null));
        result.ShouldNotHaveValidationErrorFor(c => c.Language);
    }

    [Fact]
    public void Language_3Char_Fails()
    {
        var result = _validator.TestValidate(Cmd(language: "eng"));
        result.ShouldHaveValidationErrorFor(c => c.Language);
    }

    [Fact]
    public void Language_NotInWhitelist_Fails()
    {
        var result = _validator.TestValidate(Cmd(language: "ru"));
        result.ShouldHaveValidationErrorFor(c => c.Language);
    }

    [Fact]
    public void Language_CaseInsensitive_Passes()
    {
        var result = _validator.TestValidate(Cmd(language: "IT"));
        result.ShouldNotHaveValidationErrorFor(c => c.Language);
    }

    [Fact]
    public void Language_Canonical_Passes()
    {
        var result = _validator.TestValidate(Cmd(language: "en"));
        result.ShouldNotHaveValidationErrorFor(c => c.Language);
    }

    // ── Tags ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Tags_Null_Passes()
    {
        var result = _validator.TestValidate(Cmd(tags: null));
        result.ShouldNotHaveValidationErrorFor(c => c.Tags);
    }

    [Fact]
    public void Tags_Empty_Passes()
    {
        // Explicit empty = clear all (D-4 / D-8); validator must accept.
        var result = _validator.TestValidate(Cmd(tags: Array.Empty<string>()));
        result.ShouldNotHaveValidationErrorFor(c => c.Tags);
    }

    [Fact]
    public void Tags_21Items_Fails()
    {
        var tags = Enumerable.Range(0, 21).Select(i => $"tag-{i}").ToArray();
        var result = _validator.TestValidate(Cmd(tags: tags));
        result.ShouldHaveValidationErrorFor(c => c.Tags);
    }

    [Fact]
    public void Tags_TagOver50_Fails()
    {
        var result = _validator.TestValidate(Cmd(tags: new[] { "ok", new string('a', 51) }));
        // The error attaches to the indexed element; ShouldHaveAnyValidationError
        // is the safest assertion in FluentValidation's TestHelper.
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Tags_WhitespaceTag_Fails()
    {
        var result = _validator.TestValidate(Cmd(tags: new[] { "valid", "  " }));
        result.IsValid.Should().BeFalse();
    }
}

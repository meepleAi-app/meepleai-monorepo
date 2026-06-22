using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Validators;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class TranslateGamebookTextRequestValidatorTests
{
    private readonly TranslateGamebookTextRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_DoesNotErr()
    {
        var req = new TranslateGamebookTextRequest("Hello world.", "EN", "IT", Guid.NewGuid());
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyText_ShouldHaveError()
    {
        var req = new TranslateGamebookTextRequest("", "EN", "IT", Guid.NewGuid());
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.Text);
    }

    [Fact]
    public void Validate_WithWhitespaceOnlyText_ShouldHaveError()
    {
        var req = new TranslateGamebookTextRequest("   ", "EN", "IT", Guid.NewGuid());
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.Text);
    }

    [Fact]
    public void Validate_WithTextAt2000Chars_DoesNotErr()
    {
        var req = new TranslateGamebookTextRequest(new string('a', 2000), "EN", "IT", Guid.NewGuid());
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(r => r.Text);
    }

    [Fact]
    public void Validate_WithTextAt2001Chars_ShouldHaveError()
    {
        var req = new TranslateGamebookTextRequest(new string('a', 2001), "EN", "IT", Guid.NewGuid());
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.Text);
    }

    [Theory]
    [InlineData("EN")]
    [InlineData("FR")]
    [InlineData("DE")]
    [InlineData("ES")]
    [InlineData("IT")]
    public void Validate_WithValidSourceLang_DoesNotErr(string lang)
    {
        var req = new TranslateGamebookTextRequest("Hello.", lang, "IT", Guid.NewGuid());
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(r => r.SourceLang);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("ZZ")]
    [InlineData("PL")]
    [InlineData("")]
    public void Validate_WithInvalidSourceLang_ShouldHaveError(string lang)
    {
        var req = new TranslateGamebookTextRequest("Hello.", lang, "IT", Guid.NewGuid());
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.SourceLang);
    }

    [Fact]
    public void Validate_WithSourceLangLowercase_IsAccepted()
    {
        // Validator should accept lowercase per case-insensitive policy (LanguageCodes uses OrdinalIgnoreCase)
        var req = new TranslateGamebookTextRequest("Hello.", "en", "IT", Guid.NewGuid());
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(r => r.SourceLang);
    }

    [Fact]
    public void Validate_WithTargetLangNotIT_ShouldHaveError()
    {
        var req = new TranslateGamebookTextRequest("Hello.", "EN", "EN", Guid.NewGuid());
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.TargetLang);
    }

    [Fact]
    public void Validate_WithTargetLangIT_DoesNotErr()
    {
        var req = new TranslateGamebookTextRequest("Hello.", "EN", "IT", Guid.NewGuid());
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(r => r.TargetLang);
    }

    [Fact]
    public void Validate_WithEmptyGameBookId_ShouldHaveError()
    {
        var req = new TranslateGamebookTextRequest("Hello.", "EN", "IT", Guid.Empty);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.GameBookId);
    }
}

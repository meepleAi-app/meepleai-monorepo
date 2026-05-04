using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Unit tests for UploadPhotoBatchCommandValidator.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.3.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class UploadPhotoBatchCommandValidatorTests
{
    private readonly UploadPhotoBatchCommandValidator _validator = new();

    private static string MakeBase64(int sizeBytes) =>
        Convert.ToBase64String(new byte[sizeBytes]);

    [Fact]
    public void Validate_EmptyPhotoList_ShouldHaveError()
    {
        var cmd = new UploadPhotoBatchCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            SourceLanguage: "en",
            Photos: Array.Empty<PhotoUploadDto>());

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Photos);
    }

    [Fact]
    public void Validate_TooManyPhotos_ShouldHaveError()
    {
        var photos = Enumerable.Range(0, 201)
            .Select(i => new PhotoUploadDto($"p{i}.jpg", MakeBase64(1024)))
            .ToArray();
        var cmd = new UploadPhotoBatchCommand(Guid.NewGuid(), Guid.NewGuid(), "en", photos);

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Photos);
    }

    [Fact]
    public void Validate_PhotoExceeds10MB_ShouldHaveError()
    {
        var bigPhoto = new PhotoUploadDto("p1.jpg", MakeBase64(11 * 1024 * 1024));
        var cmd = new UploadPhotoBatchCommand(Guid.NewGuid(), Guid.NewGuid(), "en", new[] { bigPhoto });

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor("Photos[0].Base64Content");
    }

    [Fact]
    public void Validate_UnsupportedLanguage_ShouldHaveError()
    {
        var cmd = new UploadPhotoBatchCommand(
            Guid.NewGuid(), Guid.NewGuid(), "ja",
            new[] { new PhotoUploadDto("p1.jpg", MakeBase64(1024)) });

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.SourceLanguage);
    }

    [Fact]
    public void Validate_ItalianAsSourceLang_ShouldNotHaveError()
    {
        var cmd = new UploadPhotoBatchCommand(
            Guid.NewGuid(), Guid.NewGuid(), "it",
            new[] { new PhotoUploadDto("p1.jpg", MakeBase64(1024)) });

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(c => c.SourceLanguage);
        // Italian is valid as source — translation will be no-op for narrative paragraphs
    }

    [Fact]
    public void Validate_InvalidBase64_ShouldHaveError()
    {
        var cmd = new UploadPhotoBatchCommand(
            Guid.NewGuid(), Guid.NewGuid(), "en",
            new[] { new PhotoUploadDto("p1.jpg", "not!valid!base64!") });

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor("Photos[0].Base64Content");
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        var cmd = new UploadPhotoBatchCommand(
            Guid.NewGuid(), Guid.NewGuid(), "en",
            new[] { new PhotoUploadDto("p1.jpg", MakeBase64(50_000)) });

        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}

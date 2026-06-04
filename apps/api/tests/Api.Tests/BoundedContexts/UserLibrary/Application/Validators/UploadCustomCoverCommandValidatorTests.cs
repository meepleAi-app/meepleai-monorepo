using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Unit tests for UploadCustomCoverCommandValidator.
/// Issue #1824 (umbrella #1821, cover stack L3).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class UploadCustomCoverCommandValidatorTests
{
    private readonly UploadCustomCoverCommandValidator _validator = new();

    [Fact]
    public void Validate_EmptyUserId_Fails()
    {
        var cmd = CreateCommand(userId: Guid.Empty);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void Validate_EmptyGameId_Fails()
    {
        var cmd = CreateCommand(gameId: Guid.Empty);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.GameId);
    }

    [Fact]
    public void Validate_FileSizeOverLimit_Fails()
    {
        var cmd = CreateCommand(fileSizeBytes: UploadCustomCoverCommandValidator.MaxFileSizeBytes + 1);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.FileSizeBytes);
    }

    [Fact]
    public void Validate_FileSizeZero_Fails()
    {
        var cmd = CreateCommand(fileSizeBytes: 0);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.FileSizeBytes);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/heic")]
    public void Validate_ValidMimeType_Passes(string mime)
    {
        var cmd = CreateCommand(mimeType: mime);
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(c => c.MimeType);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("image/svg+xml")]
    [InlineData("")]
    public void Validate_InvalidMimeType_Fails(string mime)
    {
        var cmd = CreateCommand(mimeType: mime);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.MimeType);
    }

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var cmd = CreateCommand();
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    private static UploadCustomCoverCommand CreateCommand(
        Guid? userId = null,
        Guid? gameId = null,
        long fileSizeBytes = 150_000,
        string mimeType = "image/webp")
    {
        return new UploadCustomCoverCommand(
            UserId: userId ?? Guid.NewGuid(),
            GameId: gameId ?? Guid.NewGuid(),
            FileStream: new MemoryStream(new byte[100]),
            FileSizeBytes: fileSizeBytes,
            MimeType: mimeType
        );
    }
}

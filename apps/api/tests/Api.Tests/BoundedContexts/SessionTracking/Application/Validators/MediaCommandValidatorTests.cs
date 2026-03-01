using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Validators;

/// <summary>
/// Unit tests for SessionMedia command validators.
/// Issue #4760
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class UploadSessionMediaCommandValidatorTests
{
    private readonly UploadSessionMediaCommandValidator _validator = new();

    private static UploadSessionMediaCommand ValidCommand() => new(
        SessionId: Guid.NewGuid(),
        ParticipantId: Guid.NewGuid(),
        FileId: "file-123",
        FileName: "photo.jpg",
        ContentType: "image/jpeg",
        FileSizeBytes: 1024,
        MediaType: "Photo",
        Caption: "Board state",
        SnapshotId: null,
        TurnNumber: 1);

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_ShouldFail()
    {
        var cmd = ValidCommand() with { SessionId = Guid.Empty };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.SessionId));
    }

    [Fact]
    public void Validate_EmptyParticipantId_ShouldFail()
    {
        var cmd = ValidCommand() with { ParticipantId = Guid.Empty };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.ParticipantId));
    }

    [Fact]
    public void Validate_EmptyFileId_ShouldFail()
    {
        var cmd = ValidCommand() with { FileId = "" };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.FileId));
    }

    [Fact]
    public void Validate_EmptyFileName_ShouldFail()
    {
        var cmd = ValidCommand() with { FileName = "" };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_FileNameTooLong_ShouldFail()
    {
        var cmd = ValidCommand() with { FileName = new string('A', 256) };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyContentType_ShouldFail()
    {
        var cmd = ValidCommand() with { ContentType = "" };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ZeroFileSize_ShouldFail()
    {
        var cmd = ValidCommand() with { FileSizeBytes = 0 };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.FileSizeBytes));
    }

    [Fact]
    public void Validate_NegativeFileSize_ShouldFail()
    {
        var cmd = ValidCommand() with { FileSizeBytes = -100 };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Photo")]
    [InlineData("Note")]
    [InlineData("Screenshot")]
    [InlineData("Video")]
    [InlineData("Audio")]
    [InlineData("Document")]
    public void Validate_AllowedMediaTypes_ShouldPass(string mediaType)
    {
        var cmd = ValidCommand() with { MediaType = mediaType };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("photo")]
    [InlineData("PHOTO")]
    [InlineData("")]
    public void Validate_InvalidMediaType_ShouldFail(string mediaType)
    {
        var cmd = ValidCommand() with { MediaType = mediaType };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_CaptionTooLong_ShouldFail()
    {
        var cmd = ValidCommand() with { Caption = new string('A', 501) };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NullCaption_ShouldPass()
    {
        var cmd = ValidCommand() with { Caption = null };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class UpdateMediaCaptionCommandValidatorTests
{
    private readonly UpdateMediaCaptionCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var cmd = new UpdateMediaCaptionCommand(Guid.NewGuid(), Guid.NewGuid(), "New caption");
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyMediaId_ShouldFail()
    {
        var cmd = new UpdateMediaCaptionCommand(Guid.Empty, Guid.NewGuid(), "Caption");
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyParticipantId_ShouldFail()
    {
        var cmd = new UpdateMediaCaptionCommand(Guid.NewGuid(), Guid.Empty, "Caption");
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_CaptionTooLong_ShouldFail()
    {
        var cmd = new UpdateMediaCaptionCommand(Guid.NewGuid(), Guid.NewGuid(), new string('A', 501));
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class DeleteSessionMediaCommandValidatorTests
{
    private readonly DeleteSessionMediaCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var cmd = new DeleteSessionMediaCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyMediaId_ShouldFail()
    {
        var cmd = new DeleteSessionMediaCommand(Guid.Empty, Guid.NewGuid());
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyParticipantId_ShouldFail()
    {
        var cmd = new DeleteSessionMediaCommand(Guid.NewGuid(), Guid.Empty);
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }
}

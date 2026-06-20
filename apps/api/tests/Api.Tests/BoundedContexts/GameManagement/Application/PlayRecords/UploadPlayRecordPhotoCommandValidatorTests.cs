using System;
using System.IO;
using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2436")]
public class UploadPlayRecordPhotoCommandValidatorTests
{
    private readonly UploadPlayRecordPhotoCommandValidator _validator = new();

    private static UploadPlayRecordPhotoCommand Cmd(long size = 1000, string mime = "image/jpeg") =>
        new(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), size, mime, false, null);

    [Fact]
    public void Valid_Passes() => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void TooLarge_Fails() =>
        _validator.Validate(Cmd(size: 5 * 1024 * 1024 + 1)).IsValid.Should().BeFalse();

    [Fact]
    public void EmptyFile_Fails() => _validator.Validate(Cmd(size: 0)).IsValid.Should().BeFalse();

    [Fact]
    public void BadMime_Fails() =>
        _validator.Validate(Cmd(mime: "application/pdf")).IsValid.Should().BeFalse();

    [Fact]
    public void EmptyRecordId_Fails() =>
        _validator.Validate(new UploadPlayRecordPhotoCommand(Guid.Empty, Guid.NewGuid(),
            new MemoryStream(), 100, "image/png", false, null)).IsValid.Should().BeFalse();
}

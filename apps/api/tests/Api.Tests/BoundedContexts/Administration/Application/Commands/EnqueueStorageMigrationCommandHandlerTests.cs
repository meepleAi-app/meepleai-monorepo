using Api.BoundedContexts.Administration.Application.Commands;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Unit tests for <see cref="EnqueueStorageMigrationCommandHandler"/> internal
/// helpers + handler validator (issue #1333).
///
/// The handler itself depends on <see cref="Api.Services.Pdf.S3BlobStorageService"/>
/// + S3 round-trips, so end-to-end coverage lives in the integration suite
/// (Testcontainers MinIO). These tests cover the deterministic surface:
/// resourceKey extraction + input validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Issue", "1333")]
public sealed class EnqueueStorageMigrationCommandHandlerTests
{
    [Theory]
    [InlineData("pdf_uploads/abc-game/file123_rule.pdf", "pdf_uploads/", "abc-game")]
    [InlineData("pdf_uploads/wizard-temp/fileXyz_doc.pdf", "pdf_uploads/", "wizard-temp")]
    [InlineData("session-photos/sess-abc/img1_photo.jpg", "session-photos/", "sess-abc")]
    [InlineData("pdf_uploads/shared-game-42/file_name.pdf", "pdf_uploads/", "shared-game-42")]
    public void ExtractResourceKey_ValidKey_ReturnsResourceSegment(
        string legacyKey, string legacyPrefix, string expected)
    {
        var result = EnqueueStorageMigrationCommandHandler.ExtractResourceKey(legacyKey, legacyPrefix);
        result.Should().Be(expected);
    }

    [Theory]
    // Key does not start with the supplied prefix
    [InlineData("game-images/abc/file.jpg", "pdf_uploads/")]
    // No resourceKey segment (no '/' after prefix)
    [InlineData("pdf_uploads/just-a-file.pdf", "pdf_uploads/")]
    // Empty resourceKey (consecutive separators)
    [InlineData("pdf_uploads//file.pdf", "pdf_uploads/")]
    public void ExtractResourceKey_InvalidKey_ReturnsNull(string legacyKey, string legacyPrefix)
    {
        var result = EnqueueStorageMigrationCommandHandler.ExtractResourceKey(legacyKey, legacyPrefix);
        result.Should().BeNull();
    }

    [Fact]
    public void Validator_RejectsEmptyMigrationId()
    {
        var validator = new EnqueueStorageMigrationCommandValidator();
        var cmd = new EnqueueStorageMigrationCommand(
            MigrationId: Guid.Empty,
            LegacyPrefix: "pdf_uploads/",
            Category: Api.Services.Pdf.BlobCategory.Pdf);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MigrationId");
    }

    [Fact]
    public void Validator_RejectsPrefixWithoutTrailingSlash()
    {
        var validator = new EnqueueStorageMigrationCommandValidator();
        var cmd = new EnqueueStorageMigrationCommand(
            MigrationId: Guid.NewGuid(),
            LegacyPrefix: "pdf_uploads",
            Category: Api.Services.Pdf.BlobCategory.Pdf);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LegacyPrefix");
    }

    [Theory]
    [InlineData("../pdf_uploads/")]
    [InlineData("pdf_uploads/../etc/")]
    [InlineData("pdf_uploads/$bad/")]
    public void Validator_RejectsPrefixWithUnsafeChars(string prefix)
    {
        var validator = new EnqueueStorageMigrationCommandValidator();
        var cmd = new EnqueueStorageMigrationCommand(
            MigrationId: Guid.NewGuid(),
            LegacyPrefix: prefix,
            Category: Api.Services.Pdf.BlobCategory.Pdf);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LegacyPrefix");
    }

    [Fact]
    public void Validator_RejectsInvalidCategory()
    {
        var validator = new EnqueueStorageMigrationCommandValidator();
        var cmd = new EnqueueStorageMigrationCommand(
            MigrationId: Guid.NewGuid(),
            LegacyPrefix: "pdf_uploads/",
            Category: (Api.Services.Pdf.BlobCategory)999);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Category");
    }

    [Fact]
    public void Validator_AcceptsValidCommand()
    {
        var validator = new EnqueueStorageMigrationCommandValidator();
        var cmd = new EnqueueStorageMigrationCommand(
            MigrationId: Guid.NewGuid(),
            LegacyPrefix: "pdf_uploads/",
            Category: Api.Services.Pdf.BlobCategory.Pdf,
            DryRun: true);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }
}

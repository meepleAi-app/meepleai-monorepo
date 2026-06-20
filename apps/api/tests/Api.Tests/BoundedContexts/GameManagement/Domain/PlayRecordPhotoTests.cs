using System;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2436")]
public class PlayRecordPhotoTests
{
    private static readonly Guid RecordId = Guid.NewGuid();

    [Fact]
    public void Constructor_ValidArgs_SetsProperties()
    {
        var id = Guid.NewGuid();
        var uploader = Guid.NewGuid();
        var at = new DateTime(2026, 6, 20, 18, 0, 0, DateTimeKind.Utc);

        var photo = new PlayRecordPhoto(id, RecordId, "blob/url.jpg", "blob/thumb.jpg",
            12345, "abc123", "10 - 8", 0.91, "caption", uploader, at);

        photo.Id.Should().Be(id);
        photo.PlayRecordId.Should().Be(RecordId);
        photo.BlobUrl.Should().Be("blob/url.jpg");
        photo.ThumbnailUrl.Should().Be("blob/thumb.jpg");
        photo.FileSizeBytes.Should().Be(12345);
        photo.Sha256Hash.Should().Be("abc123");
        photo.OcrText.Should().Be("10 - 8");
        photo.OcrConfidence.Should().Be(0.91);
        photo.Caption.Should().Be("caption");
        photo.UploadedByUserId.Should().Be(uploader);
        photo.UploadedAt.Should().Be(at);
    }

    [Fact]
    public void Constructor_EmptyPlayRecordId_Throws()
    {
        var act = () => new PlayRecordPhoto(Guid.NewGuid(), Guid.Empty, "u", null, 1, "h", null, null, null, Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_BlankBlobUrl_Throws()
    {
        var act = () => new PlayRecordPhoto(Guid.NewGuid(), RecordId, "  ", null, 1, "h", null, null, null, Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_BlankSha_Throws()
    {
        var act = () => new PlayRecordPhoto(Guid.NewGuid(), RecordId, "u", null, 1, "  ", null, null, null, Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<ValidationException>();
    }
}

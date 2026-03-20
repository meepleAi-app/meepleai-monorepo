using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SessionAttachmentDomainTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();
    private const string ValidBlobUrl = "https://storage.example.com/photos/test.jpg";
    private const string ValidThumbnailUrl = "https://storage.example.com/thumbnails/test_thumb.jpg";
    private const string ValidContentTypeJpeg = "image/jpeg";
    private const string ValidContentTypePng = "image/png";
    private const long ValidFileSize = 2_097_152; // 2MB
    private const long MinFileSize = 1024; // 1KB
    private const long MaxFileSize = 10_485_760; // 10MB

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidArgs_CreatesAttachment()
    {
        var attachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.BoardState,
            ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize,
            ValidThumbnailUrl, "Board after round 3", 5);

        attachment.Id.Should().NotBe(Guid.Empty);
        attachment.SessionId.Should().Be(_sessionId);
        attachment.PlayerId.Should().Be(_playerId);
        attachment.AttachmentType.Should().Be(AttachmentType.BoardState);
        attachment.BlobUrl.Should().Be(ValidBlobUrl);
        attachment.ThumbnailUrl.Should().Be(ValidThumbnailUrl);
        attachment.Caption.Should().Be("Board after round 3");
        attachment.ContentType.Should().Be(ValidContentTypeJpeg);
        attachment.FileSizeBytes.Should().Be(ValidFileSize);
        attachment.SnapshotIndex.Should().Be(5);
        (attachment.IsDeleted).Should().BeFalse();
        attachment.DeletedAt.Should().BeNull();
        (attachment.CreatedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void Create_WithMinimalArgs_CreatesAttachment()
    {
        var attachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.PlayerArea,
            ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize);

        attachment.ThumbnailUrl.Should().BeNull();
        attachment.Caption.Should().BeNull();
        attachment.SnapshotIndex.Should().BeNull();
    }

    [Theory]
    [InlineData(ValidContentTypeJpeg)]
    [InlineData(ValidContentTypePng)]
    public void Create_WithValidContentTypes_Succeeds(string contentType)
    {
        var attachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.Custom,
            ValidBlobUrl, contentType, ValidFileSize);

        attachment.ContentType.Should().Be(contentType);
    }

    [Theory]
    [InlineData(AttachmentType.PlayerArea)]
    [InlineData(AttachmentType.BoardState)]
    [InlineData(AttachmentType.CharacterSheet)]
    [InlineData(AttachmentType.ResourceInventory)]
    [InlineData(AttachmentType.Custom)]
    public void Create_WithAllAttachmentTypes_Succeeds(AttachmentType type)
    {
        var attachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, type,
            ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize);

        attachment.AttachmentType.Should().Be(type);
    }

    [Fact]
    public void Create_WithBoundaryFileSizes_Succeeds()
    {
        var minAttachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.BoardState,
            ValidBlobUrl, ValidContentTypeJpeg, MinFileSize);
        minAttachment.FileSizeBytes.Should().Be(MinFileSize);

        var maxAttachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.BoardState,
            ValidBlobUrl, ValidContentTypeJpeg, MaxFileSize);
        maxAttachment.FileSizeBytes.Should().Be(MaxFileSize);
    }

    [Fact]
    public void Create_TrimsCaptionWhitespace()
    {
        var attachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.BoardState,
            ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize,
            caption: "  Board state  ");

        attachment.Caption.Should().Be("Board state");
    }

    #endregion

    #region Create - Validation Failures

    [Fact]
    public void Create_WithEmptySessionId_Throws()
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                Guid.Empty, _playerId, AttachmentType.BoardState,
                ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize);
        var ex = act.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("Session ID");
    }

    [Fact]
    public void Create_WithEmptyPlayerId_Throws()
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, Guid.Empty, AttachmentType.BoardState,
                ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize);
        var ex = act.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("Player ID");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidBlobUrl_Throws(string? blobUrl)
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                blobUrl!, ValidContentTypeJpeg, ValidFileSize);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithBlobUrlTooLong_Throws()
    {
        var longUrl = new string('x', 2049);
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                longUrl, ValidContentTypeJpeg, ValidFileSize);
        var ex = act.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("2048");
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public void Create_WithInvalidContentType_Throws(string contentType)
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                ValidBlobUrl, contentType, ValidFileSize);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1)]
    public void Create_WithFileSizeTooSmall_Throws(long fileSize)
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                ValidBlobUrl, ValidContentTypeJpeg, fileSize);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithFileSizeTooLarge_Throws()
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                ValidBlobUrl, ValidContentTypeJpeg, MaxFileSize + 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithCaptionTooLong_Throws()
    {
        var longCaption = new string('x', 201);
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize,
                caption: longCaption);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithInvalidAttachmentType_Throws()
    {
        var act = () =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, (AttachmentType)99,
                ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Behavior Methods

    [Fact]
    public void SetThumbnail_WithValidUrl_SetsThumbnail()
    {
        var attachment = CreateValidAttachment();
        attachment.SetThumbnail(ValidThumbnailUrl);

        attachment.ThumbnailUrl.Should().Be(ValidThumbnailUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SetThumbnail_WithInvalidUrl_Throws(string? url)
    {
        var attachment = CreateValidAttachment();
        ((Action)(() => attachment.SetThumbnail(url!))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetThumbnail_WithUrlTooLong_Throws()
    {
        var attachment = CreateValidAttachment();
        ((Action)(() => attachment.SetThumbnail(new string('x', 2049)))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateCaption_WithValidCaption_UpdatesCaption()
    {
        var attachment = CreateValidAttachment();
        attachment.UpdateCaption("Updated caption");

        attachment.Caption.Should().Be("Updated caption");
    }

    [Fact]
    public void UpdateCaption_WithNull_SetsNull()
    {
        var attachment = CreateValidAttachment();
        attachment.UpdateCaption(null);

        attachment.Caption.Should().BeNull();
    }

    [Fact]
    public void UpdateCaption_TrimsCaptionWhitespace()
    {
        var attachment = CreateValidAttachment();
        attachment.UpdateCaption("  Trimmed  ");

        attachment.Caption.Should().Be("Trimmed");
    }

    [Fact]
    public void UpdateCaption_WithCaptionTooLong_Throws()
    {
        var attachment = CreateValidAttachment();
        ((Action)(() => attachment.UpdateCaption(new string('x', 201)))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkToSnapshot_WithValidIndex_SetsSnapshotIndex()
    {
        var attachment = CreateValidAttachment();
        attachment.LinkToSnapshot(3);

        attachment.SnapshotIndex.Should().Be(3);
    }

    [Fact]
    public void LinkToSnapshot_WithNegativeIndex_Throws()
    {
        var attachment = CreateValidAttachment();
        ((Action)(() => attachment.LinkToSnapshot(-1))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsDeleted_SetsDeletedFlags()
    {
        var attachment = CreateValidAttachment();
        attachment.MarkAsDeleted();

        (attachment.IsDeleted).Should().BeTrue();
        attachment.DeletedAt.Should().NotBeNull();
        (attachment.DeletedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void MarkAsDeleted_WhenAlreadyDeleted_IsIdempotent()
    {
        var attachment = CreateValidAttachment();
        attachment.MarkAsDeleted();
        var firstDeletedAt = attachment.DeletedAt;

        attachment.MarkAsDeleted();

        attachment.DeletedAt.Should().Be(firstDeletedAt);
    }

    #endregion

    #region Helpers

    private Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment CreateValidAttachment()
    {
        return Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.BoardState,
            ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize);
    }

    #endregion
}

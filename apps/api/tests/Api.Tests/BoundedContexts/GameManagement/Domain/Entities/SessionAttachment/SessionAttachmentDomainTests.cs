using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.Tests.Constants;
using Xunit;

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

        Assert.NotEqual(Guid.Empty, attachment.Id);
        Assert.Equal(_sessionId, attachment.SessionId);
        Assert.Equal(_playerId, attachment.PlayerId);
        Assert.Equal(AttachmentType.BoardState, attachment.AttachmentType);
        Assert.Equal(ValidBlobUrl, attachment.BlobUrl);
        Assert.Equal(ValidThumbnailUrl, attachment.ThumbnailUrl);
        Assert.Equal("Board after round 3", attachment.Caption);
        Assert.Equal(ValidContentTypeJpeg, attachment.ContentType);
        Assert.Equal(ValidFileSize, attachment.FileSizeBytes);
        Assert.Equal(5, attachment.SnapshotIndex);
        Assert.False(attachment.IsDeleted);
        Assert.Null(attachment.DeletedAt);
        Assert.True(attachment.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithMinimalArgs_CreatesAttachment()
    {
        var attachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.PlayerArea,
            ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize);

        Assert.Null(attachment.ThumbnailUrl);
        Assert.Null(attachment.Caption);
        Assert.Null(attachment.SnapshotIndex);
    }

    [Theory]
    [InlineData(ValidContentTypeJpeg)]
    [InlineData(ValidContentTypePng)]
    public void Create_WithValidContentTypes_Succeeds(string contentType)
    {
        var attachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.Custom,
            ValidBlobUrl, contentType, ValidFileSize);

        Assert.Equal(contentType, attachment.ContentType);
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

        Assert.Equal(type, attachment.AttachmentType);
    }

    [Fact]
    public void Create_WithBoundaryFileSizes_Succeeds()
    {
        var minAttachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.BoardState,
            ValidBlobUrl, ValidContentTypeJpeg, MinFileSize);
        Assert.Equal(MinFileSize, minAttachment.FileSizeBytes);

        var maxAttachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.BoardState,
            ValidBlobUrl, ValidContentTypeJpeg, MaxFileSize);
        Assert.Equal(MaxFileSize, maxAttachment.FileSizeBytes);
    }

    [Fact]
    public void Create_TrimsCaptionWhitespace()
    {
        var attachment = Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
            _sessionId, _playerId, AttachmentType.BoardState,
            ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize,
            caption: "  Board state  ");

        Assert.Equal("Board state", attachment.Caption);
    }

    #endregion

    #region Create - Validation Failures

    [Fact]
    public void Create_WithEmptySessionId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                Guid.Empty, _playerId, AttachmentType.BoardState,
                ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize));

        Assert.Contains("Session ID", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyPlayerId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, Guid.Empty, AttachmentType.BoardState,
                ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize));

        Assert.Contains("Player ID", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidBlobUrl_Throws(string? blobUrl)
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                blobUrl!, ValidContentTypeJpeg, ValidFileSize));
    }

    [Fact]
    public void Create_WithBlobUrlTooLong_Throws()
    {
        var longUrl = new string('x', 2049);
        var ex = Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                longUrl, ValidContentTypeJpeg, ValidFileSize));

        Assert.Contains("2048", ex.Message);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public void Create_WithInvalidContentType_Throws(string contentType)
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                ValidBlobUrl, contentType, ValidFileSize));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1)]
    public void Create_WithFileSizeTooSmall_Throws(long fileSize)
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                ValidBlobUrl, ValidContentTypeJpeg, fileSize));
    }

    [Fact]
    public void Create_WithFileSizeTooLarge_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                ValidBlobUrl, ValidContentTypeJpeg, MaxFileSize + 1));
    }

    [Fact]
    public void Create_WithCaptionTooLong_Throws()
    {
        var longCaption = new string('x', 201);
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, AttachmentType.BoardState,
                ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize,
                caption: longCaption));
    }

    [Fact]
    public void Create_WithInvalidAttachmentType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment.SessionAttachment.Create(
                _sessionId, _playerId, (AttachmentType)99,
                ValidBlobUrl, ValidContentTypeJpeg, ValidFileSize));
    }

    #endregion

    #region Behavior Methods

    [Fact]
    public void SetThumbnail_WithValidUrl_SetsThumbnail()
    {
        var attachment = CreateValidAttachment();
        attachment.SetThumbnail(ValidThumbnailUrl);

        Assert.Equal(ValidThumbnailUrl, attachment.ThumbnailUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SetThumbnail_WithInvalidUrl_Throws(string? url)
    {
        var attachment = CreateValidAttachment();
        Assert.Throws<ArgumentException>(() => attachment.SetThumbnail(url!));
    }

    [Fact]
    public void SetThumbnail_WithUrlTooLong_Throws()
    {
        var attachment = CreateValidAttachment();
        Assert.Throws<ArgumentException>(() => attachment.SetThumbnail(new string('x', 2049)));
    }

    [Fact]
    public void UpdateCaption_WithValidCaption_UpdatesCaption()
    {
        var attachment = CreateValidAttachment();
        attachment.UpdateCaption("Updated caption");

        Assert.Equal("Updated caption", attachment.Caption);
    }

    [Fact]
    public void UpdateCaption_WithNull_SetsNull()
    {
        var attachment = CreateValidAttachment();
        attachment.UpdateCaption(null);

        Assert.Null(attachment.Caption);
    }

    [Fact]
    public void UpdateCaption_TrimsCaptionWhitespace()
    {
        var attachment = CreateValidAttachment();
        attachment.UpdateCaption("  Trimmed  ");

        Assert.Equal("Trimmed", attachment.Caption);
    }

    [Fact]
    public void UpdateCaption_WithCaptionTooLong_Throws()
    {
        var attachment = CreateValidAttachment();
        Assert.Throws<ArgumentException>(() => attachment.UpdateCaption(new string('x', 201)));
    }

    [Fact]
    public void LinkToSnapshot_WithValidIndex_SetsSnapshotIndex()
    {
        var attachment = CreateValidAttachment();
        attachment.LinkToSnapshot(3);

        Assert.Equal(3, attachment.SnapshotIndex);
    }

    [Fact]
    public void LinkToSnapshot_WithNegativeIndex_Throws()
    {
        var attachment = CreateValidAttachment();
        Assert.Throws<ArgumentException>(() => attachment.LinkToSnapshot(-1));
    }

    [Fact]
    public void MarkAsDeleted_SetsDeletedFlags()
    {
        var attachment = CreateValidAttachment();
        attachment.MarkAsDeleted();

        Assert.True(attachment.IsDeleted);
        Assert.NotNull(attachment.DeletedAt);
        Assert.True(attachment.DeletedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void MarkAsDeleted_WhenAlreadyDeleted_IsIdempotent()
    {
        var attachment = CreateValidAttachment();
        attachment.MarkAsDeleted();
        var firstDeletedAt = attachment.DeletedAt;

        attachment.MarkAsDeleted();

        Assert.Equal(firstDeletedAt, attachment.DeletedAt);
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

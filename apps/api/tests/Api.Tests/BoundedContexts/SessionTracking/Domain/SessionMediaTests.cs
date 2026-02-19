using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Unit tests for SessionMedia domain entity.
/// Issue #4760
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SessionMediaTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _participantId = Guid.NewGuid();

    private SessionMedia CreateValidMedia(
        string? caption = null,
        Guid? snapshotId = null,
        int? turnNumber = null)
    {
        return SessionMedia.Create(
            _sessionId,
            _participantId,
            fileId: "file-abc-123",
            fileName: "board-photo.jpg",
            contentType: "image/jpeg",
            fileSizeBytes: 1024 * 100,
            mediaType: SessionMediaType.Photo,
            caption: caption,
            snapshotId: snapshotId,
            turnNumber: turnNumber);
    }

    [Fact]
    public void Create_ValidInput_CreatesMedia()
    {
        // Act
        var media = CreateValidMedia(caption: "Turn 3 board state");

        // Assert
        Assert.NotEqual(Guid.Empty, media.Id);
        Assert.Equal(_sessionId, media.SessionId);
        Assert.Equal(_participantId, media.ParticipantId);
        Assert.Equal("file-abc-123", media.FileId);
        Assert.Equal("board-photo.jpg", media.FileName);
        Assert.Equal("image/jpeg", media.ContentType);
        Assert.Equal(1024 * 100, media.FileSizeBytes);
        Assert.Equal(SessionMediaType.Photo, media.MediaType);
        Assert.Equal("Turn 3 board state", media.Caption);
        Assert.True(media.IsSharedWithSession);
        Assert.False(media.IsDeleted);
        Assert.Null(media.DeletedAt);
        Assert.Null(media.ThumbnailFileId);
    }

    [Fact]
    public void Create_WithSnapshot_LinksSnapshotAndTurn()
    {
        // Arrange
        var snapshotId = Guid.NewGuid();

        // Act
        var media = CreateValidMedia(snapshotId: snapshotId, turnNumber: 5);

        // Assert
        Assert.Equal(snapshotId, media.SnapshotId);
        Assert.Equal(5, media.TurnNumber);
    }

    [Fact]
    public void Create_TrimsCaption()
    {
        // Act
        var media = CreateValidMedia(caption: "  trimmed  ");

        // Assert
        Assert.Equal("trimmed", media.Caption);
    }

    [Fact]
    public void Create_EmptySessionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionMedia.Create(Guid.Empty, _participantId, "f", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo));
    }

    [Fact]
    public void Create_EmptyParticipantId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionMedia.Create(_sessionId, Guid.Empty, "f", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo));
    }

    [Fact]
    public void Create_EmptyFileId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionMedia.Create(_sessionId, _participantId, "", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo));
        Assert.Throws<ArgumentException>(() =>
            SessionMedia.Create(_sessionId, _participantId, "   ", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo));
    }

    [Fact]
    public void Create_EmptyFileName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionMedia.Create(_sessionId, _participantId, "f", "", "image/jpeg", 100, SessionMediaType.Photo));
    }

    [Fact]
    public void Create_EmptyContentType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionMedia.Create(_sessionId, _participantId, "f", "f.jpg", "", 100, SessionMediaType.Photo));
    }

    [Fact]
    public void Create_ZeroFileSize_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionMedia.Create(_sessionId, _participantId, "f", "f.jpg", "image/jpeg", 0, SessionMediaType.Photo));
    }

    [Fact]
    public void Create_NegativeFileSize_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionMedia.Create(_sessionId, _participantId, "f", "f.jpg", "image/jpeg", -1, SessionMediaType.Photo));
    }

    [Fact]
    public void UpdateCaption_SetsNewCaption()
    {
        // Arrange
        var media = CreateValidMedia(caption: "old");
        var originalUpdatedAt = media.UpdatedAt;
        Thread.Sleep(10);

        // Act
        media.UpdateCaption("new caption");

        // Assert
        Assert.Equal("new caption", media.Caption);
        Assert.True(media.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void UpdateCaption_Null_ClearsCaption()
    {
        // Arrange
        var media = CreateValidMedia(caption: "old");

        // Act
        media.UpdateCaption(null);

        // Assert
        Assert.Null(media.Caption);
    }

    [Fact]
    public void UpdateCaption_TrimsWhitespace()
    {
        // Arrange
        var media = CreateValidMedia();

        // Act
        media.UpdateCaption("  spaced  ");

        // Assert
        Assert.Equal("spaced", media.Caption);
    }

    [Fact]
    public void SetThumbnail_ValidId_SetsThumbnailFileId()
    {
        // Arrange
        var media = CreateValidMedia();
        var originalUpdatedAt = media.UpdatedAt;
        Thread.Sleep(10);

        // Act
        media.SetThumbnail("thumb-xyz");

        // Assert
        Assert.Equal("thumb-xyz", media.ThumbnailFileId);
        Assert.True(media.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void SetThumbnail_EmptyId_ThrowsArgumentException()
    {
        var media = CreateValidMedia();
        Assert.Throws<ArgumentException>(() => media.SetThumbnail(""));
        Assert.Throws<ArgumentException>(() => media.SetThumbnail("   "));
    }

    [Fact]
    public void LinkToSnapshot_ValidId_SetsSnapshotAndTurn()
    {
        // Arrange
        var media = CreateValidMedia();
        var snapshotId = Guid.NewGuid();
        var originalUpdatedAt = media.UpdatedAt;
        Thread.Sleep(10);

        // Act
        media.LinkToSnapshot(snapshotId, turnNumber: 7);

        // Assert
        Assert.Equal(snapshotId, media.SnapshotId);
        Assert.Equal(7, media.TurnNumber);
        Assert.True(media.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void LinkToSnapshot_EmptySnapshotId_ThrowsArgumentException()
    {
        var media = CreateValidMedia();
        Assert.Throws<ArgumentException>(() => media.LinkToSnapshot(Guid.Empty));
    }

    [Fact]
    public void SetSharedWithSession_ToggleSharing()
    {
        // Arrange
        var media = CreateValidMedia();
        Assert.True(media.IsSharedWithSession); // default true

        // Act
        media.SetSharedWithSession(false);

        // Assert
        Assert.False(media.IsSharedWithSession);

        // Toggle back
        media.SetSharedWithSession(true);
        Assert.True(media.IsSharedWithSession);
    }

    [Fact]
    public void SoftDelete_SetsDeletedFlags()
    {
        // Arrange
        var media = CreateValidMedia();

        // Act
        media.SoftDelete();

        // Assert
        Assert.True(media.IsDeleted);
        Assert.NotNull(media.DeletedAt);
    }

    [Theory]
    [InlineData(SessionMediaType.Photo)]
    [InlineData(SessionMediaType.Note)]
    [InlineData(SessionMediaType.Screenshot)]
    [InlineData(SessionMediaType.Video)]
    [InlineData(SessionMediaType.Audio)]
    [InlineData(SessionMediaType.Document)]
    public void Create_AllMediaTypes_Succeeds(SessionMediaType mediaType)
    {
        // Act
        var media = SessionMedia.Create(
            _sessionId, _participantId, "f", "f.bin", "application/octet-stream", 100, mediaType);

        // Assert
        Assert.Equal(mediaType, media.MediaType);
    }
}

using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
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
        media.Id.Should().NotBe(Guid.Empty);
        media.SessionId.Should().Be(_sessionId);
        media.ParticipantId.Should().Be(_participantId);
        media.FileId.Should().Be("file-abc-123");
        media.FileName.Should().Be("board-photo.jpg");
        media.ContentType.Should().Be("image/jpeg");
        media.FileSizeBytes.Should().Be(1024 * 100);
        media.MediaType.Should().Be(SessionMediaType.Photo);
        media.Caption.Should().Be("Turn 3 board state");
        media.IsSharedWithSession.Should().BeTrue();
        media.IsDeleted.Should().BeFalse();
        media.DeletedAt.Should().BeNull();
        media.ThumbnailFileId.Should().BeNull();
    }

    [Fact]
    public void Create_WithSnapshot_LinksSnapshotAndTurn()
    {
        // Arrange
        var snapshotId = Guid.NewGuid();

        // Act
        var media = CreateValidMedia(snapshotId: snapshotId, turnNumber: 5);

        // Assert
        media.SnapshotId.Should().Be(snapshotId);
        media.TurnNumber.Should().Be(5);
    }

    [Fact]
    public void Create_TrimsCaption()
    {
        // Act
        var media = CreateValidMedia(caption: "  trimmed  ");

        // Assert
        media.Caption.Should().Be("trimmed");
    }

    [Fact]
    public void Create_EmptySessionId_ThrowsArgumentException()
    {
        var act = () =>
            SessionMedia.Create(Guid.Empty, _participantId, "f", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyParticipantId_ThrowsArgumentException()
    {
        var act2 = () =>
            SessionMedia.Create(_sessionId, Guid.Empty, "f", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo);
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyFileId_ThrowsArgumentException()
    {
        var act3 = () =>
            SessionMedia.Create(_sessionId, _participantId, "", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo);
        act3.Should().Throw<ArgumentException>();
        var act4 = () =>
            SessionMedia.Create(_sessionId, _participantId, "   ", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo);
        act4.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyFileName_ThrowsArgumentException()
    {
        var act5 = () =>
            SessionMedia.Create(_sessionId, _participantId, "f", "", "image/jpeg", 100, SessionMediaType.Photo);
        act5.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyContentType_ThrowsArgumentException()
    {
        var act6 = () =>
            SessionMedia.Create(_sessionId, _participantId, "f", "f.jpg", "", 100, SessionMediaType.Photo);
        act6.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ZeroFileSize_ThrowsArgumentException()
    {
        var act7 = () =>
            SessionMedia.Create(_sessionId, _participantId, "f", "f.jpg", "image/jpeg", 0, SessionMediaType.Photo);
        act7.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NegativeFileSize_ThrowsArgumentException()
    {
        var act8 = () =>
            SessionMedia.Create(_sessionId, _participantId, "f", "f.jpg", "image/jpeg", -1, SessionMediaType.Photo);
        act8.Should().Throw<ArgumentException>();
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
        media.Caption.Should().Be("new caption");
        (media.UpdatedAt > originalUpdatedAt).Should().BeTrue();
    }

    [Fact]
    public void UpdateCaption_Null_ClearsCaption()
    {
        // Arrange
        var media = CreateValidMedia(caption: "old");

        // Act
        media.UpdateCaption(null);

        // Assert
        media.Caption.Should().BeNull();
    }

    [Fact]
    public void UpdateCaption_TrimsWhitespace()
    {
        // Arrange
        var media = CreateValidMedia();

        // Act
        media.UpdateCaption("  spaced  ");

        // Assert
        media.Caption.Should().Be("spaced");
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
        media.ThumbnailFileId.Should().Be("thumb-xyz");
        (media.UpdatedAt > originalUpdatedAt).Should().BeTrue();
    }

    [Fact]
    public void SetThumbnail_EmptyId_ThrowsArgumentException()
    {
        var media = CreateValidMedia();
        var act9 = () => media.SetThumbnail("");
        act9.Should().Throw<ArgumentException>();
        var act10 = () => media.SetThumbnail("   ");
        act10.Should().Throw<ArgumentException>();
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
        media.SnapshotId.Should().Be(snapshotId);
        media.TurnNumber.Should().Be(7);
        (media.UpdatedAt > originalUpdatedAt).Should().BeTrue();
    }

    [Fact]
    public void LinkToSnapshot_EmptySnapshotId_ThrowsArgumentException()
    {
        var media = CreateValidMedia();
        var act11 = () => media.LinkToSnapshot(Guid.Empty);
        act11.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetSharedWithSession_ToggleSharing()
    {
        // Arrange
        var media = CreateValidMedia();
        media.IsSharedWithSession.Should().BeTrue();

        // Act
        media.SetSharedWithSession(false);

        // Assert
        media.IsSharedWithSession.Should().BeFalse();

        // Toggle back
        media.SetSharedWithSession(true);
        media.IsSharedWithSession.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_SetsDeletedFlags()
    {
        // Arrange
        var media = CreateValidMedia();

        // Act
        media.SoftDelete();

        // Assert
        media.IsDeleted.Should().BeTrue();
        media.DeletedAt.Should().NotBeNull();
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
        media.MediaType.Should().Be(mediaType);
    }
}

using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Unit tests for SessionNote domain entity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SessionNoteTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _participantId = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_CreatesEncryptedNote()
    {
        // Arrange
        var content = "My secret strategy for winning!";

        // Act
        var note = SessionNote.Create(_sessionId, _participantId, content);

        // Assert
        Assert.NotEqual(Guid.Empty, note.Id);
        Assert.Equal(_sessionId, note.SessionId);
        Assert.Equal(_participantId, note.ParticipantId);
        Assert.False(note.IsRevealed);
        Assert.False(note.IsDeleted);
        Assert.NotEmpty(note.EncryptedContent);
        Assert.NotEqual(content, note.EncryptedContent); // Should be encrypted
    }

    [Fact]
    public void Create_WithObscuredText_SetsObscuredText()
    {
        // Arrange
        var content = "My secret note";
        var obscuredText = "Hint: chess move";

        // Act
        var note = SessionNote.Create(_sessionId, _participantId, content, obscuredText);

        // Assert
        Assert.Equal(obscuredText, note.ObscuredText);
    }

    [Fact]
    public void Create_EmptySessionId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SessionNote.Create(Guid.Empty, _participantId, "content"));
    }

    [Fact]
    public void Create_EmptyParticipantId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SessionNote.Create(_sessionId, Guid.Empty, "content"));
    }

    [Fact]
    public void Create_EmptyContent_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SessionNote.Create(_sessionId, _participantId, ""));
        Assert.Throws<ArgumentException>(() =>
            SessionNote.Create(_sessionId, _participantId, "   "));
    }

    [Fact]
    public void GetDecryptedContent_ReturnsOriginalContent()
    {
        // Arrange
        var content = "My secret strategy for winning!";
        var note = SessionNote.Create(_sessionId, _participantId, content);

        // Act
        var decrypted = note.GetDecryptedContent();

        // Assert
        Assert.Equal(content, decrypted);
    }

    [Fact]
    public void GetContent_Owner_ReturnsDecryptedContent()
    {
        // Arrange
        var content = "My secret note";
        var note = SessionNote.Create(_sessionId, _participantId, content);

        // Act
        var result = note.GetContent(_participantId); // Owner requesting

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void GetContent_NonOwnerNotRevealed_ReturnsObscuredText()
    {
        // Arrange
        var content = "My secret note";
        var obscuredText = "Hint: chess move";
        var note = SessionNote.Create(_sessionId, _participantId, content, obscuredText);
        var otherParticipant = Guid.NewGuid();

        // Act
        var result = note.GetContent(otherParticipant);

        // Assert
        Assert.Equal(obscuredText, result);
    }

    [Fact]
    public void GetContent_NonOwnerNotRevealed_NoObscuredText_ReturnsPlaceholder()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "secret");
        var otherParticipant = Guid.NewGuid();

        // Act
        var result = note.GetContent(otherParticipant);

        // Assert
        Assert.Equal("***", result);
    }

    [Fact]
    public void GetContent_NonOwnerRevealed_ReturnsDecryptedContent()
    {
        // Arrange
        var content = "My secret note";
        var note = SessionNote.Create(_sessionId, _participantId, content);
        note.Reveal();
        var otherParticipant = Guid.NewGuid();

        // Act
        var result = note.GetContent(otherParticipant);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void Reveal_SetsIsRevealedToTrue()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");
        var originalUpdatedAt = note.UpdatedAt;

        // Act
        Thread.Sleep(10); // Ensure different timestamp
        note.Reveal();

        // Assert
        Assert.True(note.IsRevealed);
        Assert.True(note.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void Hide_SetsIsRevealedToFalse()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");
        note.Reveal();

        // Act
        note.Hide();

        // Assert
        Assert.False(note.IsRevealed);
    }

    [Fact]
    public void UpdateContent_ChangesEncryptedContent()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "original");
        var originalEncrypted = note.EncryptedContent;

        // Act
        note.UpdateContent("updated content");

        // Assert
        Assert.NotEqual(originalEncrypted, note.EncryptedContent);
        Assert.Equal("updated content", note.GetDecryptedContent());
    }

    [Fact]
    public void UpdateContent_EmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => note.UpdateContent(""));
    }

    [Fact]
    public void UpdateObscuredText_ChangesObscuredText()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content", "original hint");

        // Act
        note.UpdateObscuredText("new hint");

        // Assert
        Assert.Equal("new hint", note.ObscuredText);
    }

    [Fact]
    public void UpdateObscuredText_Null_ClearsObscuredText()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content", "original hint");

        // Act
        note.UpdateObscuredText(null);

        // Assert
        Assert.Null(note.ObscuredText);
    }

    [Fact]
    public void CanView_Owner_ReturnsTrue()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");

        // Act & Assert
        Assert.True(note.CanView(_participantId));
    }

    [Fact]
    public void CanView_NonOwnerNotRevealed_ReturnsFalse()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");
        var otherParticipant = Guid.NewGuid();

        // Act & Assert
        Assert.False(note.CanView(otherParticipant));
    }

    [Fact]
    public void CanView_NonOwnerRevealed_ReturnsTrue()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");
        note.Reveal();
        var otherParticipant = Guid.NewGuid();

        // Act & Assert
        Assert.True(note.CanView(otherParticipant));
    }

    [Fact]
    public void SoftDelete_SetsDeletedFlags()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");

        // Act
        note.SoftDelete();

        // Assert
        Assert.True(note.IsDeleted);
        Assert.NotNull(note.DeletedAt);
    }

    [Fact]
    public void Reconstitute_RecreatesNoteFromPersistence()
    {
        // Arrange
        var id = Guid.NewGuid();
        var encryptedContent = "encrypted";
        var createdAt = DateTime.UtcNow.AddHours(-1);
        var updatedAt = DateTime.UtcNow;

        // Act
        var note = SessionNote.Reconstitute(
            id,
            _sessionId,
            _participantId,
            encryptedContent,
            isRevealed: true,
            obscuredText: "hint",
            createdAt,
            updatedAt,
            isDeleted: false,
            deletedAt: null);

        // Assert
        Assert.Equal(id, note.Id);
        Assert.Equal(_sessionId, note.SessionId);
        Assert.Equal(_participantId, note.ParticipantId);
        Assert.Equal(encryptedContent, note.EncryptedContent);
        Assert.True(note.IsRevealed);
        Assert.Equal("hint", note.ObscuredText);
        Assert.Equal(createdAt, note.CreatedAt);
        Assert.Equal(updatedAt, note.UpdatedAt);
        Assert.False(note.IsDeleted);
    }

    [Fact]
    public void EncryptionRoundTrip_MultipleNotes_ContentRemainsSeparate()
    {
        // Arrange
        var content1 = "First secret note";
        var content2 = "Second secret note";

        // Act
        var note1 = SessionNote.Create(_sessionId, _participantId, content1);
        var note2 = SessionNote.Create(_sessionId, _participantId, content2);

        // Assert
        Assert.Equal(content1, note1.GetDecryptedContent());
        Assert.Equal(content2, note2.GetDecryptedContent());
        Assert.NotEqual(note1.EncryptedContent, note2.EncryptedContent);
    }

    [Fact]
    public void EncryptionRoundTrip_SpecialCharacters_HandledCorrectly()
    {
        // Arrange
        var content = "Special chars: 'quotes' \"double\" <brackets> & ampersand \n newline \t tab";

        // Act
        var note = SessionNote.Create(_sessionId, _participantId, content);
        var decrypted = note.GetDecryptedContent();

        // Assert
        Assert.Equal(content, decrypted);
    }

    [Fact]
    public void EncryptionRoundTrip_UnicodeContent_HandledCorrectly()
    {
        // Arrange
        var content = "Unicode test: Emoji chess pieces: \u265A \u265B \u265C \u265D \u265E \u265F";

        // Act
        var note = SessionNote.Create(_sessionId, _participantId, content);
        var decrypted = note.GetDecryptedContent();

        // Assert
        Assert.Equal(content, decrypted);
    }
}

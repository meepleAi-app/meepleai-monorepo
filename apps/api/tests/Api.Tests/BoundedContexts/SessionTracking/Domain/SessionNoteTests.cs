using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
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
        note.Id.Should().NotBe(Guid.Empty);
        note.SessionId.Should().Be(_sessionId);
        note.ParticipantId.Should().Be(_participantId);
        note.IsRevealed.Should().BeFalse();
        note.IsDeleted.Should().BeFalse();
        note.EncryptedContent.Should().NotBeEmpty();
        note.EncryptedContent.Should().NotBeEquivalentTo(content);
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
        note.ObscuredText.Should().Be(obscuredText);
    }

    [Fact]
    public void Create_EmptySessionId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () =>
            SessionNote.Create(Guid.Empty, _participantId, "content");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyParticipantId_ThrowsArgumentException()
    {
        // Act & Assert
        var act2 = () =>
            SessionNote.Create(_sessionId, Guid.Empty, "content");
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyContent_ThrowsArgumentException()
    {
        // Act & Assert
        var act3 = () =>
            SessionNote.Create(_sessionId, _participantId, "");
        act3.Should().Throw<ArgumentException>();
        var act4 = () =>
            SessionNote.Create(_sessionId, _participantId, "   ");
        act4.Should().Throw<ArgumentException>();
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
        decrypted.Should().BeEquivalentTo(content);
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
        result.Should().BeEquivalentTo(content);
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
        result.Should().Be(obscuredText);
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
        result.Should().Be("***");
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
        result.Should().BeEquivalentTo(content);
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
        note.IsRevealed.Should().BeTrue();
        (note.UpdatedAt > originalUpdatedAt).Should().BeTrue();
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
        note.IsRevealed.Should().BeFalse();
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
        note.EncryptedContent.Should().NotBe(originalEncrypted);
        note.GetDecryptedContent().Should().Be("updated content");
    }

    [Fact]
    public void UpdateContent_EmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");

        // Act & Assert
        var act5 = () => note.UpdateContent("");
        act5.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateObscuredText_ChangesObscuredText()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content", "original hint");

        // Act
        note.UpdateObscuredText("new hint");

        // Assert
        note.ObscuredText.Should().Be("new hint");
    }

    [Fact]
    public void UpdateObscuredText_Null_ClearsObscuredText()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content", "original hint");

        // Act
        note.UpdateObscuredText(null);

        // Assert
        note.ObscuredText.Should().BeNull();
    }

    [Fact]
    public void CanView_Owner_ReturnsTrue()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");

        // Act & Assert
        note.CanView(_participantId).Should().BeTrue();
    }

    [Fact]
    public void CanView_NonOwnerNotRevealed_ReturnsFalse()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");
        var otherParticipant = Guid.NewGuid();

        // Act & Assert
        note.CanView(otherParticipant).Should().BeFalse();
    }

    [Fact]
    public void CanView_NonOwnerRevealed_ReturnsTrue()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");
        note.Reveal();
        var otherParticipant = Guid.NewGuid();

        // Act & Assert
        note.CanView(otherParticipant).Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_SetsDeletedFlags()
    {
        // Arrange
        var note = SessionNote.Create(_sessionId, _participantId, "content");

        // Act
        note.SoftDelete();

        // Assert
        note.IsDeleted.Should().BeTrue();
        note.DeletedAt.Should().NotBeNull();
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
        note.Id.Should().Be(id);
        note.SessionId.Should().Be(_sessionId);
        note.ParticipantId.Should().Be(_participantId);
        note.EncryptedContent.Should().Be(encryptedContent);
        note.IsRevealed.Should().BeTrue();
        note.ObscuredText.Should().Be("hint");
        note.CreatedAt.Should().Be(createdAt);
        note.UpdatedAt.Should().Be(updatedAt);
        note.IsDeleted.Should().BeFalse();
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
        note1.GetDecryptedContent().Should().Be(content1);
        note2.GetDecryptedContent().Should().Be(content2);
        note2.EncryptedContent.Should().NotBe(note1.EncryptedContent);
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
        decrypted.Should().BeEquivalentTo(content);
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
        decrypted.Should().BeEquivalentTo(content);
    }
}

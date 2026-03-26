using Api.BoundedContexts.SessionTracking.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class PlayerNoteTests
{
    private static readonly Guid ValidSessionId = Guid.NewGuid();
    private static readonly Guid ValidParticipantId = Guid.NewGuid();

    // --- Create: valid inputs ---

    [Fact]
    public void Create_WithValidParameters_ShouldReturnNoteWithCorrectProperties()
    {
        // Act
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "My private note");

        // Assert
        note.Should().NotBeNull();
        note.Id.Should().NotBeEmpty();
        note.SessionId.Should().Be(ValidSessionId);
        note.ParticipantId.Should().Be(ValidParticipantId);
        note.NoteType.Should().Be(NoteType.Private);
        note.Content.Should().Be("My private note");
        note.IsHidden.Should().BeFalse();
        note.TemplateKey.Should().BeNull();
        note.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithSharedNoteType_ShouldSetNoteType()
    {
        // Act
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Shared, "Shared content");

        // Assert
        note.NoteType.Should().Be(NoteType.Shared);
    }

    [Fact]
    public void Create_WithTemplateNoteAndKey_ShouldSetTemplateKey()
    {
        // Act
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Template, "Template content", templateKey: "objective_card");

        // Assert
        note.NoteType.Should().Be(NoteType.Template);
        note.TemplateKey.Should().Be("objective_card");
    }

    [Fact]
    public void Create_WithIsHiddenTrue_ShouldSetIsHidden()
    {
        // Act
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Secret", isHidden: true);

        // Assert
        note.IsHidden.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldTrimContent()
    {
        // Act
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "  trimmed  ");

        // Assert
        note.Content.Should().Be("trimmed");
    }

    [Fact]
    public void Create_ShouldTrimTemplateKey()
    {
        // Act
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Template, "Content", templateKey: "  key  ");

        // Assert
        note.TemplateKey.Should().Be("key");
    }

    [Fact]
    public void Create_ShouldSetCreatedAt()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Content");

        // Assert
        note.CreatedAt.Should().BeAfter(before);
    }

    // --- Create: invalid inputs ---

    [Fact]
    public void Create_WithEmptySessionId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => PlayerNote.Create(Guid.Empty, ValidParticipantId, NoteType.Private, "Content");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Session ID cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyParticipantId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => PlayerNote.Create(ValidSessionId, Guid.Empty, NoteType.Private, "Content");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Participant ID cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceContent_ShouldThrowArgumentException(string? content)
    {
        // Act
        var act = () => PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, content!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Content cannot be empty*");
    }

    [Fact]
    public void Create_WithContentExceeding10000Chars_ShouldThrowArgumentException()
    {
        // Arrange
        var tooLong = new string('x', 10001);

        // Act
        var act = () => PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, tooLong);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Content cannot exceed 10000 characters*");
    }

    [Fact]
    public void Create_TemplateNoteWithoutTemplateKey_ShouldThrowArgumentException()
    {
        // Act
        var act = () => PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Template, "Content");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Template key required for template notes*");
    }

    [Fact]
    public void Create_WithTemplateKeyExceeding50Chars_ShouldThrowArgumentException()
    {
        // Arrange
        var longKey = new string('k', 51);

        // Act
        var act = () => PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Template, "Content", templateKey: longKey);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Template key cannot exceed 50 characters*");
    }

    // --- UpdateContent ---

    [Fact]
    public void UpdateContent_WithValidContent_ShouldChangeContent()
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Original");

        // Act
        note.UpdateContent("Updated content");

        // Assert
        note.Content.Should().Be("Updated content");
    }

    [Fact]
    public void UpdateContent_ShouldSetUpdatedAt()
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Original");
        note.UpdatedAt.Should().BeNull();

        // Act
        note.UpdateContent("Updated");

        // Assert
        note.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateContent_ShouldTrimContent()
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Original");

        // Act
        note.UpdateContent("  trimmed update  ");

        // Assert
        note.Content.Should().Be("trimmed update");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateContent_WithNullOrWhitespace_ShouldThrowArgumentException(string? newContent)
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Original");

        // Act
        var act = () => note.UpdateContent(newContent!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Content cannot be empty*");
    }

    [Fact]
    public void UpdateContent_WithContentExceeding10000Chars_ShouldThrowArgumentException()
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Original");
        var tooLong = new string('x', 10001);

        // Act
        var act = () => note.UpdateContent(tooLong);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Content cannot exceed 10000 characters*");
    }

    // --- ToggleHidden ---

    [Fact]
    public void ToggleHidden_WhenVisible_ShouldSetIsHiddenTrue()
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Content", isHidden: false);

        // Act
        note.ToggleHidden();

        // Assert
        note.IsHidden.Should().BeTrue();
    }

    [Fact]
    public void ToggleHidden_WhenHidden_ShouldSetIsHiddenFalse()
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Content", isHidden: true);

        // Act
        note.ToggleHidden();

        // Assert
        note.IsHidden.Should().BeFalse();
    }

    [Fact]
    public void ToggleHidden_ShouldSetUpdatedAt()
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Content");

        // Act
        note.ToggleHidden();

        // Assert
        note.UpdatedAt.Should().NotBeNull();
    }

    // --- Show / Hide ---

    [Fact]
    public void Hide_ShouldSetIsHiddenTrue()
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Content", isHidden: false);

        // Act
        note.Hide();

        // Assert
        note.IsHidden.Should().BeTrue();
        note.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Show_ShouldSetIsHiddenFalse()
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Content", isHidden: true);

        // Act
        note.Show();

        // Assert
        note.IsHidden.Should().BeFalse();
        note.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Hide_ThenShow_ShouldBeVisible()
    {
        // Arrange
        var note = PlayerNote.Create(ValidSessionId, ValidParticipantId, NoteType.Private, "Content");

        // Act
        note.Hide();
        note.Show();

        // Assert
        note.IsHidden.Should().BeFalse();
    }
}

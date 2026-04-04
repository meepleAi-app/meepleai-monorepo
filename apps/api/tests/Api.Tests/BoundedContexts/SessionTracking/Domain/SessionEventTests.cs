using Api.BoundedContexts.SessionTracking.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionEventTests
{
    private static readonly Guid ValidSessionId = Guid.NewGuid();

    // --- Create: valid inputs ---

    [Fact]
    public void Create_WithMinimalValidParameters_ShouldReturnEventWithCorrectProperties()
    {
        // Act
        var evt = SessionEvent.Create(ValidSessionId, "dice_roll");

        // Assert
        evt.Should().NotBeNull();
        evt.Id.Should().NotBeEmpty();
        evt.SessionId.Should().Be(ValidSessionId);
        evt.EventType.Should().Be("dice_roll");
        evt.Payload.Should().Be("{}");
        evt.CreatedBy.Should().BeNull();
        evt.Source.Should().BeNull();
        evt.IsDeleted.Should().BeFalse();
        evt.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllParameters_ShouldSetAllProperties()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var payload = """{"score": 42}""";

        // Act
        var evt = SessionEvent.Create(ValidSessionId, "score_update", payload, createdBy, "user");

        // Assert
        evt.Payload.Should().Be(payload);
        evt.CreatedBy.Should().Be(createdBy);
        evt.Source.Should().Be("user");
    }

    [Fact]
    public void Create_WithNullPayload_ShouldDefaultToEmptyJsonObject()
    {
        // Act
        var evt = SessionEvent.Create(ValidSessionId, "player_joined", payload: null);

        // Assert
        evt.Payload.Should().Be("{}");
    }

    [Fact]
    public void Create_ShouldSetTimestampApproximatelyToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var evt = SessionEvent.Create(ValidSessionId, "note_added");

        // Assert
        var after = DateTime.UtcNow.AddSeconds(1);
        evt.Timestamp.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Act
        var evt1 = SessionEvent.Create(ValidSessionId, "event_a");
        var evt2 = SessionEvent.Create(ValidSessionId, "event_b");

        // Assert
        evt1.Id.Should().NotBe(evt2.Id);
    }

    [Fact]
    public void Create_WithSystemEvent_ShouldAllowNullCreatedBy()
    {
        // Act
        var evt = SessionEvent.Create(ValidSessionId, "system_tick", createdBy: null, source: "system");

        // Assert
        evt.CreatedBy.Should().BeNull();
        evt.Source.Should().Be("system");
    }

    // --- Create: invalid inputs ---

    [Fact]
    public void Create_WithEmptySessionId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => SessionEvent.Create(Guid.Empty, "dice_roll");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Session ID cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceEventType_ShouldThrowArgumentException(string? eventType)
    {
        // Act
        var act = () => SessionEvent.Create(ValidSessionId, eventType!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Event type cannot be empty*");
    }

    [Fact]
    public void Create_WithEventTypeLongerThan50Chars_ShouldThrowArgumentException()
    {
        // Arrange
        var tooLong = new string('e', 51);

        // Act
        var act = () => SessionEvent.Create(ValidSessionId, tooLong);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Event type must be 50 characters or fewer*");
    }

    [Fact]
    public void Create_WithEventTypeExactly50Chars_ShouldSucceed()
    {
        // Arrange
        var exactly50 = new string('e', 50);

        // Act
        var evt = SessionEvent.Create(ValidSessionId, exactly50);

        // Assert
        evt.EventType.Should().HaveLength(50);
    }

    [Fact]
    public void Create_WithSourceLongerThan50Chars_ShouldThrowArgumentException()
    {
        // Arrange
        var tooLong = new string('s', 51);

        // Act
        var act = () => SessionEvent.Create(ValidSessionId, "dice_roll", source: tooLong);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Source must be 50 characters or fewer*");
    }

    [Fact]
    public void Create_WithSourceExactly50Chars_ShouldSucceed()
    {
        // Arrange
        var exactly50 = new string('s', 50);

        // Act
        var evt = SessionEvent.Create(ValidSessionId, "dice_roll", source: exactly50);

        // Assert
        evt.Source.Should().HaveLength(50);
    }

    // --- SoftDelete ---

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedTrue()
    {
        // Arrange
        var evt = SessionEvent.Create(ValidSessionId, "dice_roll");
        evt.IsDeleted.Should().BeFalse();

        // Act
        evt.SoftDelete();

        // Assert
        evt.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_ShouldSetDeletedAt()
    {
        // Arrange
        var evt = SessionEvent.Create(ValidSessionId, "dice_roll");
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        evt.SoftDelete();

        // Assert
        evt.DeletedAt.Should().NotBeNull();
        evt.DeletedAt.Should().BeAfter(before);
    }

    [Fact]
    public void SoftDelete_CalledTwice_ShouldRemainDeleted()
    {
        // Arrange
        var evt = SessionEvent.Create(ValidSessionId, "dice_roll");

        // Act
        evt.SoftDelete();
        evt.SoftDelete();

        // Assert
        evt.IsDeleted.Should().BeTrue();
        evt.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void NewEvent_ShouldNotBeDeleted()
    {
        // Act
        var evt = SessionEvent.Create(ValidSessionId, "note_added");

        // Assert
        evt.IsDeleted.Should().BeFalse();
        evt.DeletedAt.Should().BeNull();
    }
}

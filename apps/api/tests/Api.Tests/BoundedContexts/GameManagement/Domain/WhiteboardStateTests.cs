using Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

/// <summary>
/// Unit tests for the WhiteboardState domain aggregate.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class WhiteboardStateTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    // ========================================================================
    // Constructor
    // ========================================================================

    [Fact]
    public void Constructor_WithValidArgs_CreatesEmptyWhiteboard()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        whiteboard.SessionId.Should().Be(SessionId);
        whiteboard.LastModifiedBy.Should().Be(UserId);
        whiteboard.Strokes.Should().BeEmpty();
        whiteboard.StructuredJson.Should().Be("{}");
    }

    [Fact]
    public void Constructor_WithEmptySessionId_ThrowsArgumentException()
    {
        var act = () =>
            new WhiteboardState(Guid.NewGuid(), Guid.Empty, UserId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyUserId_ThrowsArgumentException()
    {
        var act = () =>
            new WhiteboardState(Guid.NewGuid(), SessionId, Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    // ========================================================================
    // AddStroke
    // ========================================================================

    [Fact]
    public void AddStroke_WithValidArgs_AddsStroke()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        var stroke = whiteboard.AddStroke("stroke-1", "{\"path\":[]}", UserId);

        whiteboard.Strokes.Should().ContainSingle();
        stroke.Id.Should().Be("stroke-1");
        stroke.DataJson.Should().Be("{\"path\":[]}");
    }

    [Fact]
    public void AddStroke_WithNullDataJson_DefaultsToEmptyObject()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        var stroke = whiteboard.AddStroke("stroke-1", null!, UserId);

        stroke.DataJson.Should().Be("{}");
    }

    [Fact]
    public void AddStroke_WithDuplicateId_ThrowsInvalidOperationException()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        whiteboard.AddStroke("stroke-1", "{}", UserId);

        var act = () =>
            whiteboard.AddStroke("stroke-1", "{\"new\":true}", UserId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddStroke_WithEmptyStrokeId_ThrowsArgumentException()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        var act = () =>
            whiteboard.AddStroke("", "{}", UserId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddStroke_UpdatesLastModifiedBy()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        var anotherUser = Guid.NewGuid();

        whiteboard.AddStroke("stroke-1", "{}", anotherUser);

        whiteboard.LastModifiedBy.Should().Be(anotherUser);
    }

    [Fact]
    public void AddStroke_MultipleTimes_AccumulatesStrokes()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        whiteboard.AddStroke("stroke-1", "{}", UserId);
        whiteboard.AddStroke("stroke-2", "{}", UserId);
        whiteboard.AddStroke("stroke-3", "{}", UserId);

        whiteboard.Strokes.Count.Should().Be(3);
    }

    // ========================================================================
    // RemoveStroke
    // ========================================================================

    [Fact]
    public void RemoveStroke_WithExistingId_RemovesStroke()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        whiteboard.AddStroke("stroke-1", "{}", UserId);

        var removedId = whiteboard.RemoveStroke("stroke-1", UserId);

        removedId.Should().Be("stroke-1");
        whiteboard.Strokes.Should().BeEmpty();
    }

    [Fact]
    public void RemoveStroke_WithNonExistingId_ThrowsInvalidOperationException()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        var act = () =>
            whiteboard.RemoveStroke("non-existing", UserId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveStroke_WithEmptyId_ThrowsArgumentException()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        var act = () =>
            whiteboard.RemoveStroke("", UserId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RemoveStroke_PreservesOtherStrokes()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        whiteboard.AddStroke("stroke-1", "{}", UserId);
        whiteboard.AddStroke("stroke-2", "{}", UserId);
        whiteboard.AddStroke("stroke-3", "{}", UserId);

        whiteboard.RemoveStroke("stroke-2", UserId);

        whiteboard.Strokes.Count.Should().Be(2);
        whiteboard.Strokes.Should().NotContain(s => s.Id == "stroke-2");
    }

    // ========================================================================
    // UpdateStructured
    // ========================================================================

    [Fact]
    public void UpdateStructured_WithValidJson_ReplacesStructuredLayer()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        var newJson = "{\"tokens\":[{\"id\":\"t1\"}]}";

        whiteboard.UpdateStructured(newJson, UserId);

        whiteboard.StructuredJson.Should().Be(newJson);
    }

    [Fact]
    public void UpdateStructured_WithNull_DefaultsToEmptyObject()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        whiteboard.UpdateStructured(null!, UserId);

        whiteboard.StructuredJson.Should().Be("{}");
    }

    [Fact]
    public void UpdateStructured_ExceedingMaxSize_ThrowsArgumentException()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        var oversizedJson = new string('x', 100 * 1024 + 1); // 100 KB + 1 byte

        var act = () =>
            whiteboard.UpdateStructured(oversizedJson, UserId);
        act.Should().Throw<ArgumentException>();
    }

    // ========================================================================
    // Clear
    // ========================================================================

    [Fact]
    public void Clear_RemovesAllStrokesAndResetsStructured()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        whiteboard.AddStroke("stroke-1", "{}", UserId);
        whiteboard.AddStroke("stroke-2", "{}", UserId);
        whiteboard.UpdateStructured("{\"tokens\":[]}", UserId);

        whiteboard.Clear(UserId);

        whiteboard.Strokes.Should().BeEmpty();
        whiteboard.StructuredJson.Should().Be("{}");
    }

    [Fact]
    public void Clear_UpdatesLastModifiedBy()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        var clearingUser = Guid.NewGuid();

        whiteboard.Clear(clearingUser);

        whiteboard.LastModifiedBy.Should().Be(clearingUser);
    }

    // ========================================================================
    // Restore
    // ========================================================================

    [Fact]
    public void Restore_WithStrokes_ReconstitutesAggregate()
    {
        var id = Guid.NewGuid();
        var strokes = new[] { new WhiteboardStroke("s1", "{}"), new WhiteboardStroke("s2", "{}") };
        var now = DateTime.UtcNow;

        var whiteboard = WhiteboardState.Restore(
            id, SessionId, strokes, "{\"key\":\"val\"}", UserId, now, now.AddHours(-1));

        whiteboard.Id.Should().Be(id);
        whiteboard.SessionId.Should().Be(SessionId);
        whiteboard.Strokes.Count.Should().Be(2);
        whiteboard.StructuredJson.Should().Be("{\"key\":\"val\"}");
        whiteboard.LastModifiedBy.Should().Be(UserId);
    }
}

using Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;
using Api.Tests.Constants;
using Xunit;

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

        Assert.Equal(SessionId, whiteboard.SessionId);
        Assert.Equal(UserId, whiteboard.LastModifiedBy);
        Assert.Empty(whiteboard.Strokes);
        Assert.Equal("{}", whiteboard.StructuredJson);
    }

    [Fact]
    public void Constructor_WithEmptySessionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new WhiteboardState(Guid.NewGuid(), Guid.Empty, UserId));
    }

    [Fact]
    public void Constructor_WithEmptyUserId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new WhiteboardState(Guid.NewGuid(), SessionId, Guid.Empty));
    }

    // ========================================================================
    // AddStroke
    // ========================================================================

    [Fact]
    public void AddStroke_WithValidArgs_AddsStroke()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        var stroke = whiteboard.AddStroke("stroke-1", "{\"path\":[]}", UserId);

        Assert.Single(whiteboard.Strokes);
        Assert.Equal("stroke-1", stroke.Id);
        Assert.Equal("{\"path\":[]}", stroke.DataJson);
    }

    [Fact]
    public void AddStroke_WithNullDataJson_DefaultsToEmptyObject()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        var stroke = whiteboard.AddStroke("stroke-1", null!, UserId);

        Assert.Equal("{}", stroke.DataJson);
    }

    [Fact]
    public void AddStroke_WithDuplicateId_ThrowsInvalidOperationException()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        whiteboard.AddStroke("stroke-1", "{}", UserId);

        Assert.Throws<InvalidOperationException>(() =>
            whiteboard.AddStroke("stroke-1", "{\"new\":true}", UserId));
    }

    [Fact]
    public void AddStroke_WithEmptyStrokeId_ThrowsArgumentException()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        Assert.Throws<ArgumentException>(() =>
            whiteboard.AddStroke("", "{}", UserId));
    }

    [Fact]
    public void AddStroke_UpdatesLastModifiedBy()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        var anotherUser = Guid.NewGuid();

        whiteboard.AddStroke("stroke-1", "{}", anotherUser);

        Assert.Equal(anotherUser, whiteboard.LastModifiedBy);
    }

    [Fact]
    public void AddStroke_MultipleTimes_AccumulatesStrokes()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        whiteboard.AddStroke("stroke-1", "{}", UserId);
        whiteboard.AddStroke("stroke-2", "{}", UserId);
        whiteboard.AddStroke("stroke-3", "{}", UserId);

        Assert.Equal(3, whiteboard.Strokes.Count);
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

        Assert.Equal("stroke-1", removedId);
        Assert.Empty(whiteboard.Strokes);
    }

    [Fact]
    public void RemoveStroke_WithNonExistingId_ThrowsInvalidOperationException()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        Assert.Throws<InvalidOperationException>(() =>
            whiteboard.RemoveStroke("non-existing", UserId));
    }

    [Fact]
    public void RemoveStroke_WithEmptyId_ThrowsArgumentException()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        Assert.Throws<ArgumentException>(() =>
            whiteboard.RemoveStroke("", UserId));
    }

    [Fact]
    public void RemoveStroke_PreservesOtherStrokes()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        whiteboard.AddStroke("stroke-1", "{}", UserId);
        whiteboard.AddStroke("stroke-2", "{}", UserId);
        whiteboard.AddStroke("stroke-3", "{}", UserId);

        whiteboard.RemoveStroke("stroke-2", UserId);

        Assert.Equal(2, whiteboard.Strokes.Count);
        Assert.DoesNotContain(whiteboard.Strokes, s => s.Id == "stroke-2");
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

        Assert.Equal(newJson, whiteboard.StructuredJson);
    }

    [Fact]
    public void UpdateStructured_WithNull_DefaultsToEmptyObject()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);

        whiteboard.UpdateStructured(null!, UserId);

        Assert.Equal("{}", whiteboard.StructuredJson);
    }

    [Fact]
    public void UpdateStructured_ExceedingMaxSize_ThrowsArgumentException()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        var oversizedJson = new string('x', 100 * 1024 + 1); // 100 KB + 1 byte

        Assert.Throws<ArgumentException>(() =>
            whiteboard.UpdateStructured(oversizedJson, UserId));
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

        Assert.Empty(whiteboard.Strokes);
        Assert.Equal("{}", whiteboard.StructuredJson);
    }

    [Fact]
    public void Clear_UpdatesLastModifiedBy()
    {
        var whiteboard = new WhiteboardState(Guid.NewGuid(), SessionId, UserId);
        var clearingUser = Guid.NewGuid();

        whiteboard.Clear(clearingUser);

        Assert.Equal(clearingUser, whiteboard.LastModifiedBy);
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

        Assert.Equal(id, whiteboard.Id);
        Assert.Equal(SessionId, whiteboard.SessionId);
        Assert.Equal(2, whiteboard.Strokes.Count);
        Assert.Equal("{\"key\":\"val\"}", whiteboard.StructuredJson);
        Assert.Equal(UserId, whiteboard.LastModifiedBy);
    }
}

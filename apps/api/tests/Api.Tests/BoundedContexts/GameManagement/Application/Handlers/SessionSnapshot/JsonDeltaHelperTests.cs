using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class JsonDeltaHelperTests
{
    // ========================================================================
    // ComputeDelta
    // ========================================================================

    [Fact]
    public void ComputeDelta_IdenticalObjects_ReturnsEmptyArray()
    {
        var json = "{\"a\":1,\"b\":2}";
        var delta = JsonDeltaHelper.ComputeDelta(json, json);
        Assert.Equal("[]", delta);
    }

    [Fact]
    public void ComputeDelta_AddedProperty_ReturnsAddOperation()
    {
        var previous = "{\"a\":1}";
        var current = "{\"a\":1,\"b\":2}";
        var delta = JsonDeltaHelper.ComputeDelta(previous, current);

        Assert.Contains("\"op\":\"add\"", delta);
        Assert.Contains("\"path\":\"/b\"", delta);
        Assert.Contains("\"value\":2", delta);
    }

    [Fact]
    public void ComputeDelta_RemovedProperty_ReturnsRemoveOperation()
    {
        var previous = "{\"a\":1,\"b\":2}";
        var current = "{\"a\":1}";
        var delta = JsonDeltaHelper.ComputeDelta(previous, current);

        Assert.Contains("\"op\":\"remove\"", delta);
        Assert.Contains("\"path\":\"/b\"", delta);
    }

    [Fact]
    public void ComputeDelta_ChangedProperty_ReturnsReplaceOperation()
    {
        var previous = "{\"a\":1}";
        var current = "{\"a\":42}";
        var delta = JsonDeltaHelper.ComputeDelta(previous, current);

        Assert.Contains("\"op\":\"replace\"", delta);
        Assert.Contains("\"path\":\"/a\"", delta);
        Assert.Contains("\"value\":42", delta);
    }

    [Fact]
    public void ComputeDelta_NestedChange_ReturnsNestedPath()
    {
        var previous = "{\"obj\":{\"x\":1}}";
        var current = "{\"obj\":{\"x\":99}}";
        var delta = JsonDeltaHelper.ComputeDelta(previous, current);

        Assert.Contains("\"op\":\"replace\"", delta);
        Assert.Contains("\"path\":\"/obj/x\"", delta);
        Assert.Contains("\"value\":99", delta);
    }

    [Fact]
    public void ComputeDelta_ArrayChange_ReplacesEntireArray()
    {
        var previous = "{\"arr\":[1,2,3]}";
        var current = "{\"arr\":[1,2,3,4]}";
        var delta = JsonDeltaHelper.ComputeDelta(previous, current);

        Assert.Contains("\"op\":\"replace\"", delta);
        Assert.Contains("\"path\":\"/arr\"", delta);
    }

    [Fact]
    public void ComputeDelta_EmptyToNonEmpty_ReturnsAddOperations()
    {
        var previous = "{}";
        var current = "{\"x\":1,\"y\":2}";
        var delta = JsonDeltaHelper.ComputeDelta(previous, current);

        Assert.Contains("\"op\":\"add\"", delta);
    }

    [Fact]
    public void ComputeDelta_NonEmptyToEmpty_ReturnsRemoveOperations()
    {
        var previous = "{\"x\":1,\"y\":2}";
        var current = "{}";
        var delta = JsonDeltaHelper.ComputeDelta(previous, current);

        Assert.Contains("\"op\":\"remove\"", delta);
    }

    [Fact]
    public void ComputeDelta_TypeChange_ReturnsReplaceOperation()
    {
        var previous = "{\"val\":\"text\"}";
        var current = "{\"val\":42}";
        var delta = JsonDeltaHelper.ComputeDelta(previous, current);

        Assert.Contains("\"op\":\"replace\"", delta);
        Assert.Contains("\"path\":\"/val\"", delta);
    }

    // ========================================================================
    // ApplyDelta
    // ========================================================================

    [Fact]
    public void ApplyDelta_EmptyDelta_ReturnsOriginal()
    {
        var baseState = "{\"a\":1}";
        var result = JsonDeltaHelper.ApplyDelta(baseState, "[]");
        Assert.Equal("{\"a\":1}", result);
    }

    [Fact]
    public void ApplyDelta_AddOperation_AddsProperty()
    {
        var baseState = "{\"a\":1}";
        var delta = "[{\"op\":\"add\",\"path\":\"/b\",\"value\":2}]";
        var result = JsonDeltaHelper.ApplyDelta(baseState, delta);

        Assert.Contains("\"a\":1", result);
        Assert.Contains("\"b\":2", result);
    }

    [Fact]
    public void ApplyDelta_ReplaceOperation_ReplacesValue()
    {
        var baseState = "{\"a\":1}";
        var delta = "[{\"op\":\"replace\",\"path\":\"/a\",\"value\":99}]";
        var result = JsonDeltaHelper.ApplyDelta(baseState, delta);

        Assert.Contains("\"a\":99", result);
    }

    [Fact]
    public void ApplyDelta_RemoveOperation_RemovesProperty()
    {
        var baseState = "{\"a\":1,\"b\":2}";
        var delta = "[{\"op\":\"remove\",\"path\":\"/b\"}]";
        var result = JsonDeltaHelper.ApplyDelta(baseState, delta);

        Assert.Contains("\"a\":1", result);
        Assert.DoesNotContain("\"b\"", result);
    }

    [Fact]
    public void ApplyDelta_NestedAdd_CreatesNestedPath()
    {
        var baseState = "{\"obj\":{}}";
        var delta = "[{\"op\":\"add\",\"path\":\"/obj/x\",\"value\":42}]";
        var result = JsonDeltaHelper.ApplyDelta(baseState, delta);

        Assert.Contains("\"x\":42", result);
    }

    [Fact]
    public void ApplyDelta_MultipleOperations_AppliesAll()
    {
        var baseState = "{\"a\":1,\"b\":2}";
        var delta = "[{\"op\":\"replace\",\"path\":\"/a\",\"value\":10},{\"op\":\"remove\",\"path\":\"/b\"},{\"op\":\"add\",\"path\":\"/c\",\"value\":3}]";
        var result = JsonDeltaHelper.ApplyDelta(baseState, delta);

        Assert.Contains("\"a\":10", result);
        Assert.DoesNotContain("\"b\"", result);
        Assert.Contains("\"c\":3", result);
    }

    // ========================================================================
    // ReconstructState
    // ========================================================================

    [Fact]
    public void ReconstructState_NoDeltas_ReturnsCheckpoint()
    {
        var checkpoint = "{\"turn\":0,\"score\":0}";
        var result = JsonDeltaHelper.ReconstructState(checkpoint, Array.Empty<string>());
        Assert.Equal(checkpoint, result);
    }

    [Fact]
    public void ReconstructState_SingleDelta_AppliesCorrectly()
    {
        var checkpoint = "{\"turn\":0,\"score\":0}";
        var delta = "[{\"op\":\"replace\",\"path\":\"/turn\",\"value\":1},{\"op\":\"replace\",\"path\":\"/score\",\"value\":10}]";

        var result = JsonDeltaHelper.ReconstructState(checkpoint, new[] { delta });

        Assert.Contains("\"turn\":1", result);
        Assert.Contains("\"score\":10", result);
    }

    [Fact]
    public void ReconstructState_MultipleDeltas_AppliesSequentially()
    {
        var checkpoint = "{\"turn\":0,\"score\":0}";
        var delta1 = "[{\"op\":\"replace\",\"path\":\"/turn\",\"value\":1}]";
        var delta2 = "[{\"op\":\"replace\",\"path\":\"/turn\",\"value\":2},{\"op\":\"replace\",\"path\":\"/score\",\"value\":20}]";
        var delta3 = "[{\"op\":\"replace\",\"path\":\"/turn\",\"value\":3}]";

        var result = JsonDeltaHelper.ReconstructState(checkpoint, new[] { delta1, delta2, delta3 });

        Assert.Contains("\"turn\":3", result);
        Assert.Contains("\"score\":20", result);
    }

    // ========================================================================
    // Roundtrip: ComputeDelta → ApplyDelta
    // ========================================================================

    [Fact]
    public void Roundtrip_SimpleChange_ReconstructsOriginal()
    {
        var previous = "{\"a\":1,\"b\":\"hello\"}";
        var current = "{\"a\":42,\"b\":\"world\",\"c\":true}";

        var delta = JsonDeltaHelper.ComputeDelta(previous, current);
        var reconstructed = JsonDeltaHelper.ApplyDelta(previous, delta);

        // Parse both to compare structurally
        var expected = System.Text.Json.JsonDocument.Parse(current);
        var actual = System.Text.Json.JsonDocument.Parse(reconstructed);

        Assert.Equal(
            expected.RootElement.GetProperty("a").GetInt32(),
            actual.RootElement.GetProperty("a").GetInt32());
        Assert.Equal(
            expected.RootElement.GetProperty("b").GetString(),
            actual.RootElement.GetProperty("b").GetString());
        Assert.Equal(
            expected.RootElement.GetProperty("c").GetBoolean(),
            actual.RootElement.GetProperty("c").GetBoolean());
    }

    [Fact]
    public void Roundtrip_NestedObject_ReconstructsCorrectly()
    {
        var previous = "{\"player\":{\"name\":\"Alice\",\"score\":10},\"turn\":1}";
        var current = "{\"player\":{\"name\":\"Alice\",\"score\":25},\"turn\":2}";

        var delta = JsonDeltaHelper.ComputeDelta(previous, current);
        var reconstructed = JsonDeltaHelper.ApplyDelta(previous, delta);

        var actual = System.Text.Json.JsonDocument.Parse(reconstructed);
        Assert.Equal(25, actual.RootElement.GetProperty("player").GetProperty("score").GetInt32());
        Assert.Equal(2, actual.RootElement.GetProperty("turn").GetInt32());
    }

    [Fact]
    public void Roundtrip_MultiStep_ReconstructsFinalState()
    {
        var state0 = "{\"turn\":0,\"players\":{\"alice\":0,\"bob\":0}}";
        var state1 = "{\"turn\":1,\"players\":{\"alice\":10,\"bob\":0}}";
        var state2 = "{\"turn\":2,\"players\":{\"alice\":10,\"bob\":15}}";

        var delta01 = JsonDeltaHelper.ComputeDelta(state0, state1);
        var delta12 = JsonDeltaHelper.ComputeDelta(state1, state2);

        var reconstructed = JsonDeltaHelper.ReconstructState(state0, new[] { delta01, delta12 });

        var actual = System.Text.Json.JsonDocument.Parse(reconstructed);
        Assert.Equal(2, actual.RootElement.GetProperty("turn").GetInt32());
        Assert.Equal(10, actual.RootElement.GetProperty("players").GetProperty("alice").GetInt32());
        Assert.Equal(15, actual.RootElement.GetProperty("players").GetProperty("bob").GetInt32());
    }
}

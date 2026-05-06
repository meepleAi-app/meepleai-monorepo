using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.2) — opaque cursor encoding
/// round-trip and malformed-input fail-fast tests for <see cref="KbChunksCursor"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KbChunksCursorTests
{
    [Fact]
    public void Encode_Decode_RoundTripsPositionAndId()
    {
        var payload = new KbChunksCursor.CursorPayload(42, Guid.NewGuid());
        var encoded = KbChunksCursor.Encode(payload);
        var decoded = KbChunksCursor.Decode(encoded);

        decoded.Should().NotBeNull();
        decoded!.Position.Should().Be(payload.Position);
        decoded.Id.Should().Be(payload.Id);
    }

    [Fact]
    public void Encode_ProducesBase64String()
    {
        var encoded = KbChunksCursor.Encode(new KbChunksCursor.CursorPayload(1, Guid.NewGuid()));

        // Should be valid base64 (no whitespace, only base64-alphabet + padding).
        var act = () => Convert.FromBase64String(encoded);
        act.Should().NotThrow();
    }

    [Fact]
    public void Decode_NullOrEmpty_ReturnsNull()
    {
        KbChunksCursor.Decode(null).Should().BeNull();
        KbChunksCursor.Decode(string.Empty).Should().BeNull();
    }

    [Fact]
    public void Decode_InvalidBase64_ThrowsFormatException()
    {
        var act = () => KbChunksCursor.Decode("not!valid!base64!");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Decode_MissingColonSeparator_ThrowsFormatException()
    {
        // base64 of "no-colon-here"
        var bad = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("no-colon-here"));
        var act = () => KbChunksCursor.Decode(bad);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Decode_NonNumericPosition_ThrowsFormatException()
    {
        var bad = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"abc:{Guid.NewGuid():N}"));
        var act = () => KbChunksCursor.Decode(bad);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Decode_InvalidGuid_ThrowsFormatException()
    {
        var bad = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("42:not-a-guid"));
        var act = () => KbChunksCursor.Decode(bad);
        act.Should().Throw<FormatException>();
    }
}

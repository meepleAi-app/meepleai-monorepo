using Api.Constants;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

public sealed class SmolDoclingChunkOverflowTests
{
    [Theory]
    [InlineData(1800, false)] // exactly at limit — no split needed
    [InlineData(1801, true)]  // just over limit — must split
    [InlineData(3500, true)]  // very large page — must split
    public void ChunkNeedsSplit_BasedOnCharCount(int charCount, bool expectedNeedsSplit)
    {
        var needsSplit = charCount > ChunkingConstants.MaxEmbeddingChars;
        Assert.Equal(expectedNeedsSplit, needsSplit);
    }
}

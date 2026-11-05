using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Chunking;

/// <summary>
/// #4782 — a chunk whose page nobody measured is stored with NO page, not with a page that looks measured. The
/// contract is the one of #4827: an unknown page is <c>null</c>, never <c>0</c>, in the table the readers read.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4782")]
public sealed class ChunkPageTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(int.MaxValue)]
    public void ForStorage_ARealPage_IsStoredAsItIs(int page)
    {
        ChunkPage.ForStorage(page).Should().Be(page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ForStorage_NoPage_IsStoredAsNull_NeverAsZero(int page)
    {
        ChunkPage.ForStorage(page).Should().BeNull();
    }

    [Fact]
    public void TheUnknownPage_IsNotAPage()
    {
        ChunkPage.ForStorage(ChunkPage.Unknown).Should().BeNull();
    }
}

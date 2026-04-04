using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Enums;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class CopyrightTierTests
{
    [Fact]
    public void CopyrightTier_Has_Two_Values()
    {
        var values = Enum.GetValues<CopyrightTier>();
        Assert.Equal(2, values.Length);
    }

    [Fact]
    public void CopyrightTier_Protected_Is_Default_Safe()
    {
        Assert.Equal(CopyrightTier.Protected, default(CopyrightTier));
    }
}

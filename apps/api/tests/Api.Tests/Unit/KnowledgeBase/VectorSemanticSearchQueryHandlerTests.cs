using Xunit;
using System.Reflection;

namespace Api.Tests.Unit.KnowledgeBase;

public sealed class VectorSemanticSearchQueryHandlerTests
{
    [Fact]
    public void DefaultMinScore_ShouldBeAtLeast_0_35()
    {
        var type = typeof(Api.BoundedContexts.KnowledgeBase.Application.Queries
            .VectorSemanticSearchQueryHandler);
        var field = type.GetField("DefaultMinScore",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);

        var value = (double)field.GetValue(null)!;

        Assert.True(value >= 0.35,
            $"DefaultMinScore was {value}, expected >= 0.35 to filter irrelevant chunks");
    }
}

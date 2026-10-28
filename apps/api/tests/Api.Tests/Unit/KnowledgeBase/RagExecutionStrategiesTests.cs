using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

/// <summary>
/// #4677: the strategies the playground saves its runs with and the ids the dashboard routes accept were two lists that
/// nobody tied together, and they had drifted apart. <see cref="RagExecutionStrategies.All"/> is the single definition,
/// and the writer (<c>ResolveStrategy</c>) is derived from it: it keeps what the list holds and nothing else, so a
/// strategy cannot be added to the writer without being in the list. These tests pin that, and pin the three values
/// themselves, which are already saved in the database.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4677")]
public sealed class RagExecutionStrategiesTests
{
    [Theory]
    [InlineData(RagExecutionStrategies.SingleModel)]
    [InlineData(RagExecutionStrategies.RetrievalOnly)]
    [InlineData(RagExecutionStrategies.MultiModelConsensus)]
    public void EveryStrategyOfTheList_IsKeptAsItIs_ByTheWriter(string strategy)
    {
        PlaygroundChatCommandHandler.ResolveStrategy(strategy).Should().Be(strategy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Hybrid")]
    [InlineData("singlemodel")]
    [InlineData("SomethingElse")]
    public void WhateverElseTheCallerSends_BecomesAStrategyOfTheList(string? requested)
    {
        RagExecutionStrategies.All.Should().Contain(PlaygroundChatCommandHandler.ResolveStrategy(requested));
    }

    [Fact]
    public void TheList_HasNoDuplicates_AndKeepsTheThreeKnownStrategies()
    {
        RagExecutionStrategies.All.Should().OnlyHaveUniqueItems();
        RagExecutionStrategies.All.Should().BeEquivalentTo(["SingleModel", "RetrievalOnly", "MultiModelConsensus"],
            "these are the values already saved in knowledge_base.rag_executions: renaming one orphans the old rows");
    }
}

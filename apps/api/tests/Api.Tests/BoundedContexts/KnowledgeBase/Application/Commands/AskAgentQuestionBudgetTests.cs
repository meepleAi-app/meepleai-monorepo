using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class AskAgentQuestionBudgetTests
{
    [Fact]
    public void Handler_Should_Accept_IUserBudgetService_Dependency()
    {
        var handlerType = typeof(AskAgentQuestionCommandHandler);
        var ctors = handlerType.GetConstructors(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ctors.Should().NotBeEmpty();
        var paramTypes = ctors[0].GetParameters().Select(p => p.ParameterType.Name).ToList();
        paramTypes.Should().Contain("IUserBudgetService",
            "Handler must inject IUserBudgetService for budget enforcement");
    }

    [Fact]
    public void ChatWithSessionHandler_Should_Accept_IUserBudgetService_Dependency()
    {
        var handlerType = typeof(ChatWithSessionAgentCommandHandler);
        var ctors = handlerType.GetConstructors(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ctors.Should().NotBeEmpty();
        var paramTypes = ctors[0].GetParameters().Select(p => p.ParameterType.Name).ToList();
        paramTypes.Should().Contain("IUserBudgetService",
            "Handler must inject IUserBudgetService for budget enforcement");
    }
}

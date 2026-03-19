using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetPendingTypologiesQueryHandlerTests
{
    private readonly Mock<IAgentTypologyRepository> _mockRepository;
    private readonly GetPendingTypologiesQueryHandler _handler;

    public GetPendingTypologiesQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentTypologyRepository>();
        _handler = new GetPendingTypologiesQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPendingTypologiesOnly()
    {
        // Arrange
        var pending1 = CreateTypology(Guid.NewGuid(), "Pending1", TypologyStatus.Pending);
        var pending2 = CreateTypology(Guid.NewGuid(), "Pending2", TypologyStatus.Pending);
        var approved = CreateTypology(Guid.NewGuid(), "Approved", TypologyStatus.Approved);
        var draft = CreateTypology(Guid.NewGuid(), "Draft", TypologyStatus.Draft);

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentTypology> { pending1, approved, pending2, draft });

        var query = new GetPendingTypologiesQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t => t.Status.Should().Be("Pending"));
    }

    [Fact]
    public async Task Handle_OrdersByCreatedAtAscending()
    {
        // Arrange
        var older = CreateTypology(Guid.NewGuid(), "Older", TypologyStatus.Pending);
        var newer = CreateTypology(Guid.NewGuid(), "Newer", TypologyStatus.Pending);

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentTypology> { newer, older });

        var query = new GetPendingTypologiesQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result[0].CreatedAt.Should().BeBefore(result[1].CreatedAt);
    }

    private static AgentTypology CreateTypology(Guid id, string name, TypologyStatus status)
    {
        var strategy = AgentStrategy.Custom("Test", new Dictionary<string, object> { { "TopK", 10 } });
        return new AgentTypology(id, name, "Desc", "Prompt", strategy, Guid.NewGuid(), status);
    }
}

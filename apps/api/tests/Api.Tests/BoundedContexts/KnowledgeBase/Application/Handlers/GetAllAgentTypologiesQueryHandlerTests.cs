using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
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
public sealed class GetAllAgentTypologiesQueryHandlerTests
{
    private readonly Mock<IAgentTypologyRepository> _mockRepository;
    private readonly GetAllAgentTypologiesQueryHandler _handler;

    public GetAllAgentTypologiesQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentTypologyRepository>();
        _handler = new GetAllAgentTypologiesQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_AsAdmin_ReturnsAllTypologies()
    {
        // Arrange
        var approved = CreateTypology(Guid.NewGuid(), "Approved", TypologyStatus.Approved, Guid.NewGuid());
        var draft = CreateTypology(Guid.NewGuid(), "Draft", TypologyStatus.Draft, Guid.NewGuid());
        var pending = CreateTypology(Guid.NewGuid(), "Pending", TypologyStatus.Pending, Guid.NewGuid());

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentTypology> { approved, draft, pending });

        var query = new GetAllAgentTypologiesQuery(UserRole: "Admin", UserId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_AsUser_ReturnsApprovedOnly()
    {
        // Arrange
        var approved = CreateTypology(Guid.NewGuid(), "Approved", TypologyStatus.Approved, Guid.NewGuid());
        var draft = CreateTypology(Guid.NewGuid(), "Draft", TypologyStatus.Draft, Guid.NewGuid());

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentTypology> { approved, draft });

        var query = new GetAllAgentTypologiesQuery(UserRole: "User", UserId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("Approved");
    }

    [Fact]
    public async Task Handle_AsEditor_ReturnsApprovedPlusOwn()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var approved = CreateTypology(Guid.NewGuid(), "Approved", TypologyStatus.Approved, Guid.NewGuid());
        var ownDraft = CreateTypology(Guid.NewGuid(), "Own Draft", TypologyStatus.Draft, editorId);
        var otherDraft = CreateTypology(Guid.NewGuid(), "Other Draft", TypologyStatus.Draft, Guid.NewGuid());

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentTypology> { approved, ownDraft, otherDraft });

        var query = new GetAllAgentTypologiesQuery(UserRole: "Editor", UserId: editorId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Id == approved.Id);
        result.Should().Contain(t => t.Id == ownDraft.Id);
        result.Should().NotContain(t => t.Id == otherDraft.Id);
    }

    private static AgentTypology CreateTypology(Guid id, string name, TypologyStatus status, Guid createdBy)
    {
        var strategy = AgentStrategy.Custom("Test", new Dictionary<string, object> { { "TopK", 10 } });
        return new AgentTypology(id, name, "Desc", "Prompt", strategy, createdBy, status);
    }
}

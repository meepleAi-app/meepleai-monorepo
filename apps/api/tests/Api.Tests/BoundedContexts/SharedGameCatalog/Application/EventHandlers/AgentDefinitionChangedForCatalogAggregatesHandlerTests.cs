using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="AgentDefinitionChangedForCatalogAggregatesHandler"/>.
/// Issue #593 (Wave A.3a) — spec §6.5.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AgentDefinitionChangedForCatalogAggregatesHandlerTests
{
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<AgentDefinitionChangedForCatalogAggregatesHandler>> _loggerMock = new();

    private AgentDefinitionChangedForCatalogAggregatesHandler CreateHandler() =>
        new(_cacheMock.Object, _loggerMock.Object);

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AgentDefinitionChangedForCatalogAggregatesHandler(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AgentDefinitionChangedForCatalogAggregatesHandler(_cacheMock.Object, null!));
    }

    [Fact]
    public async Task Handle_AgentDefinitionCreatedEvent_InvalidatesSearchGamesTag()
    {
        var notification = new AgentDefinitionCreatedEvent(
            agentDefinitionId: Guid.NewGuid(),
            name: "Rules Agent");

        await CreateHandler().Handle(notification, CancellationToken.None);

        _cacheMock.Verify(
            c => c.RemoveByTagAsync("search-games", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AgentDefinitionUpdatedEvent_InvalidatesSearchGamesTag()
    {
        var notification = new AgentDefinitionUpdatedEvent(
            agentDefinitionId: Guid.NewGuid(),
            changeDescription: "Prompt updated");

        await CreateHandler().Handle(notification, CancellationToken.None);

        _cacheMock.Verify(
            c => c.RemoveByTagAsync("search-games", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AgentDefinitionActivatedEvent_InvalidatesSearchGamesTag()
    {
        var notification = new AgentDefinitionActivatedEvent(agentDefinitionId: Guid.NewGuid());

        await CreateHandler().Handle(notification, CancellationToken.None);

        _cacheMock.Verify(
            c => c.RemoveByTagAsync("search-games", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AgentDefinitionDeactivatedEvent_InvalidatesSearchGamesTag()
    {
        var notification = new AgentDefinitionDeactivatedEvent(agentDefinitionId: Guid.NewGuid());

        await CreateHandler().Handle(notification, CancellationToken.None);

        _cacheMock.Verify(
            c => c.RemoveByTagAsync("search-games", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NullAgentDefinitionCreatedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler().Handle((AgentDefinitionCreatedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullAgentDefinitionUpdatedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler().Handle((AgentDefinitionUpdatedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullAgentDefinitionActivatedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler().Handle((AgentDefinitionActivatedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullAgentDefinitionDeactivatedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler().Handle((AgentDefinitionDeactivatedEvent)null!, CancellationToken.None));
    }
}

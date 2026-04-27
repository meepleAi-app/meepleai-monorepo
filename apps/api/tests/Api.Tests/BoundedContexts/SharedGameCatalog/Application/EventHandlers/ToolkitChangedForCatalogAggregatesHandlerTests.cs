using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="ToolkitChangedForCatalogAggregatesHandler"/>.
/// Issue #593 (Wave A.3a) — spec §6.5.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ToolkitChangedForCatalogAggregatesHandlerTests
{
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<ToolkitChangedForCatalogAggregatesHandler>> _loggerMock = new();

    private ToolkitChangedForCatalogAggregatesHandler CreateHandler() =>
        new(_cacheMock.Object, _loggerMock.Object);

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ToolkitChangedForCatalogAggregatesHandler(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ToolkitChangedForCatalogAggregatesHandler(_cacheMock.Object, null!));
    }

    [Fact]
    public async Task Handle_ToolkitCreatedEvent_InvalidatesSearchGamesTag()
    {
        var notification = new ToolkitCreatedEvent(
            toolkitId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            privateGameId: null,
            name: "Test Toolkit");

        await CreateHandler().Handle(notification, CancellationToken.None);

        _cacheMock.Verify(
            c => c.RemoveByTagAsync("search-games", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ToolkitPublishedEvent_InvalidatesSearchGamesTag()
    {
        var notification = new ToolkitPublishedEvent(toolkitId: Guid.NewGuid(), version: 1);

        await CreateHandler().Handle(notification, CancellationToken.None);

        _cacheMock.Verify(
            c => c.RemoveByTagAsync("search-games", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NullToolkitCreatedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler().Handle((ToolkitCreatedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullToolkitPublishedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler().Handle((ToolkitPublishedEvent)null!, CancellationToken.None));
    }
}

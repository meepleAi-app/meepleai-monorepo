using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetModelChangeHistoryQueryHandler.
/// Issue #5503: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GetModelChangeHistoryQueryHandlerTests
{
    private readonly Mock<IModelCompatibilityRepository> _repoMock;
    private readonly GetModelChangeHistoryQueryHandler _handler;

    public GetModelChangeHistoryQueryHandlerTests()
    {
        _repoMock = new Mock<IModelCompatibilityRepository>();
        _handler = new GetModelChangeHistoryQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsChangeHistory()
    {
        // Arrange
        var entries = new List<ModelChangeLogEntry>
        {
            new(Guid.NewGuid(), "model-1", "deprecated", null, null,
                "BALANCED", "Model deprecated by OpenRouter", true, null, DateTime.UtcNow),
            new(Guid.NewGuid(), "model-1", "fallback_activated", "model-1", "model-2",
                "BALANCED", "Auto-fallback activated", true, null, DateTime.UtcNow),
        };

        _repoMock.Setup(r => r.GetChangeHistoryAsync(null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(
            new GetModelChangeHistoryQuery(null, 50), CancellationToken.None);

        // Assert
        result.Changes.Should().HaveCount(2);
        result.Changes[0].ChangeType.Should().Be("deprecated");
        result.Changes[0].IsAutomatic.Should().BeTrue();
        result.Changes[1].ChangeType.Should().Be("fallback_activated");
        result.Changes[1].NewModelId.Should().Be("model-2");
    }

    [Fact]
    public async Task Handle_WithModelIdFilter_PassesFilterToRepository()
    {
        // Arrange
        _repoMock.Setup(r => r.GetChangeHistoryAsync("specific-model", 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModelChangeLogEntry>());

        // Act
        var result = await _handler.Handle(
            new GetModelChangeHistoryQuery("specific-model", 25), CancellationToken.None);

        // Assert
        result.Changes.Should().BeEmpty();
        _repoMock.Verify(r => r.GetChangeHistoryAsync("specific-model", 25, It.IsAny<CancellationToken>()), Times.Once);
    }
}

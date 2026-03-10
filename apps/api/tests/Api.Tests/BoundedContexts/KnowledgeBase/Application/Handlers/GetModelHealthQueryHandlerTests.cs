using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetModelHealthQueryHandler.
/// Issue #5503: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GetModelHealthQueryHandlerTests
{
    private readonly Mock<IModelCompatibilityRepository> _repoMock;
    private readonly GetModelHealthQueryHandler _handler;

    public GetModelHealthQueryHandlerTests()
    {
        _repoMock = new Mock<IModelCompatibilityRepository>();
        _handler = new GetModelHealthQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsAllModels()
    {
        // Arrange
        var entries = new List<ModelCompatibilityEntry>
        {
            new(Guid.NewGuid(), "model-1", "Model One", "openrouter",
                new[] { "alt-1" }, 128000, new[] { "reasoning" },
                true, false, DateTime.UtcNow),
            new(Guid.NewGuid(), "model-2", "Model Two", "openrouter",
                Array.Empty<string>(), 32000, new[] { "speed" },
                false, true, DateTime.UtcNow.AddHours(-2)),
        };

        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(new GetModelHealthQuery(), CancellationToken.None);

        // Assert
        result.Models.Should().HaveCount(2);
        result.Models[0].ModelId.Should().Be("model-1");
        result.Models[0].IsCurrentlyAvailable.Should().BeTrue();
        result.Models[0].IsDeprecated.Should().BeFalse();
        result.Models[1].ModelId.Should().Be("model-2");
        result.Models[1].IsDeprecated.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyList()
    {
        // Arrange
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModelCompatibilityEntry>());

        // Act
        var result = await _handler.Handle(new GetModelHealthQuery(), CancellationToken.None);

        // Assert
        result.Models.Should().BeEmpty();
    }
}

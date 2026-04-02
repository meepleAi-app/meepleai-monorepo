using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for GetUserGameKbStatusQueryHandler.
/// KB-03: User-facing per-game KB status query.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetUserGameKbStatusQueryHandlerTests
{
    private readonly Mock<IVectorDocumentRepository> _vectorRepoMock;
    private readonly Mock<IConfigurationRepository> _configRepoMock;
    private readonly GetUserGameKbStatusQueryHandler _sut;

    public GetUserGameKbStatusQueryHandlerTests()
    {
        _vectorRepoMock = new Mock<IVectorDocumentRepository>();
        _configRepoMock = new Mock<IConfigurationRepository>();
        _sut = new GetUserGameKbStatusQueryHandler(
            _vectorRepoMock.Object,
            _configRepoMock.Object);
    }

    [Fact]
    public async Task Handle_NoDocuments_ReturnsNotIndexed()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _vectorRepoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument>());

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GameId.Should().Be(gameId);
        result.IsIndexed.Should().BeFalse();
        result.DocumentCount.Should().Be(0);
        result.CoverageScore.Should().Be(0);
        result.CoverageLevel.Should().Be("None");
        result.SuggestedQuestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_HasDocuments_NoCoverageConfig_ReturnsIndexedWithDefaults()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 10);

        _vectorRepoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument> { vectorDoc });

        _configRepoMock
            .Setup(r => r.GetByKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.IsIndexed.Should().BeTrue();
        result.DocumentCount.Should().Be(1);
        result.CoverageScore.Should().Be(0);
        result.CoverageLevel.Should().Be("None");
        result.SuggestedQuestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_HasDocuments_WithCoverageConfig_ReturnsCoverageData()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "it", 25);

        _vectorRepoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument> { vectorDoc });

        var coverageKey = $"KB:Coverage:{gameId}";
        var coverageConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey(coverageKey),
            "{\"score\":75,\"level\":\"Complete\"}",
            "json",
            Guid.NewGuid(),
            category: "KnowledgeBase");

        _configRepoMock
            .Setup(r => r.GetByKeyAsync(
                coverageKey,
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(coverageConfig);

        _configRepoMock
            .Setup(r => r.GetByKeyAsync(
                $"KB:SuggestedQuestions:{gameId}",
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.IsIndexed.Should().BeTrue();
        result.DocumentCount.Should().Be(1);
        result.CoverageScore.Should().Be(75);
        result.CoverageLevel.Should().Be("Complete");
        result.SuggestedQuestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_HasDocuments_WithSuggestedQuestions_ReturnsQuestions()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 15);

        _vectorRepoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument> { vectorDoc });

        var coverageKey = $"KB:Coverage:{gameId}";
        _configRepoMock
            .Setup(r => r.GetByKeyAsync(
                coverageKey,
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        var questionsKey = $"KB:SuggestedQuestions:{gameId}";
        var questionsConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey(questionsKey),
            "[\"How do you win?\",\"What happens on your turn?\"]",
            "json",
            Guid.NewGuid(),
            category: "KnowledgeBase");

        _configRepoMock
            .Setup(r => r.GetByKeyAsync(
                questionsKey,
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(questionsConfig);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.IsIndexed.Should().BeTrue();
        result.SuggestedQuestions.Should().HaveCount(2);
        result.SuggestedQuestions.Should().Contain("How do you win?");
        result.SuggestedQuestions.Should().Contain("What happens on your turn?");
    }

    [Fact]
    public async Task Handle_MalformedCoverageJson_UsesDefaults()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 5);

        _vectorRepoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument> { vectorDoc });

        var coverageKey = $"KB:Coverage:{gameId}";
        var badConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey(coverageKey),
            "not-valid-json",
            "json",
            Guid.NewGuid(),
            category: "KnowledgeBase");

        _configRepoMock
            .Setup(r => r.GetByKeyAsync(
                coverageKey,
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(badConfig);

        _configRepoMock
            .Setup(r => r.GetByKeyAsync(
                $"KB:SuggestedQuestions:{gameId}",
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert — malformed JSON should not throw; defaults used
        result.IsIndexed.Should().BeTrue();
        result.CoverageScore.Should().Be(0);
        result.CoverageLevel.Should().Be("None");
    }
}

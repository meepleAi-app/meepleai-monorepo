using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using NSubstitute;
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
    private readonly IVectorDocumentRepository _vectorRepoMock;
    private readonly IConfigurationRepository _configRepoMock;
    private readonly GetUserGameKbStatusQueryHandler _sut;

    public GetUserGameKbStatusQueryHandlerTests()
    {
        _vectorRepoMock = Substitute.For<IVectorDocumentRepository>();
        _configRepoMock = Substitute.For<IConfigurationRepository>();
        _sut = new GetUserGameKbStatusQueryHandler(
            _vectorRepoMock,
            _configRepoMock);
    }

    [Fact]
    public async Task Handle_NoDocuments_ReturnsNotIndexed()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument>());

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
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { vectorDoc });

        _configRepoMock
            .GetByKeyAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

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
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { vectorDoc });

        var coverageKey = $"KB:Coverage:{gameId}";
        var coverageConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey(coverageKey),
            "{\"score\":75,\"level\":\"Complete\"}",
            "json",
            Guid.NewGuid(),
            category: "KnowledgeBase");

        _configRepoMock
            .GetByKeyAsync(
                coverageKey,
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(coverageConfig);

        _configRepoMock
            .GetByKeyAsync(
                $"KB:SuggestedQuestions:{gameId}",
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

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
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { vectorDoc });

        var coverageKey = $"KB:Coverage:{gameId}";
        _configRepoMock
            .GetByKeyAsync(
                coverageKey,
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        var questionsKey = $"KB:SuggestedQuestions:{gameId}";
        var questionsConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey(questionsKey),
            "[\"How do you win?\",\"What happens on your turn?\"]",
            "json",
            Guid.NewGuid(),
            category: "KnowledgeBase");

        _configRepoMock
            .GetByKeyAsync(
                questionsKey,
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(questionsConfig);

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
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { vectorDoc });

        var coverageKey = $"KB:Coverage:{gameId}";
        var badConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey(coverageKey),
            "not-valid-json",
            "json",
            Guid.NewGuid(),
            category: "KnowledgeBase");

        _configRepoMock
            .GetByKeyAsync(
                coverageKey,
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(badConfig);

        _configRepoMock
            .GetByKeyAsync(
                $"KB:SuggestedQuestions:{gameId}",
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert — malformed JSON should not throw; defaults used
        result.IsIndexed.Should().BeTrue();
        result.CoverageScore.Should().Be(0);
        result.CoverageLevel.Should().Be("None");
    }
}

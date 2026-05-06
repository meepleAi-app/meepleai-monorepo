using Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;
using Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Discover.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Discover")]
public sealed class GetDiscoverDataHandlerTests
{
    private readonly Mock<IMediator> _mediator;
    private readonly Mock<ILogger<GetDiscoverDataHandler>> _logger;
    private readonly GetDiscoverDataHandler _handler;

    public GetDiscoverDataHandlerTests()
    {
        _mediator = new Mock<IMediator>();
        _logger = new Mock<ILogger<GetDiscoverDataHandler>>();
        _handler = new GetDiscoverDataHandler(_mediator.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_AllSubQueriesSucceed_ReturnsFullPayload()
    {
        var newGames = new[] { new NewGameDto(Guid.NewGuid(), "G1", null, DateTime.UtcNow, null) };
        var topAgents = new[] { new TopAgentDto(Guid.NewGuid(), "", "", "", 5, DateTime.UtcNow) };
        var toolkits = new[] { new RecommendedToolkitDto(Guid.NewGuid(), "TK", "Game", 1, 0, DateTime.UtcNow) };
        var recentKb = new[] { new RecentKbDocDto(Guid.NewGuid(), "Doc", "Game", "Rulebook", DateTime.UtcNow, "en") };
        var contributors = new[] { new TopContributorDto(Guid.NewGuid(), "Marco", null, 13, 10, 1) };

        _mediator.Setup(m => m.Send(It.IsAny<GetNewGamesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<NewGameDto>)newGames);
        _mediator.Setup(m => m.Send(It.IsAny<GetTopAgentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TopAgentDto>)topAgents);
        _mediator.Setup(m => m.Send(It.IsAny<GetRecommendedToolkitsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RecommendedToolkitDto>)toolkits);
        _mediator.Setup(m => m.Send(It.IsAny<GetRecentKbDocumentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RecentKbDocDto>)recentKb);
        _mediator.Setup(m => m.Send(It.IsAny<GetTopContributorsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TopContributorDto>)contributors);

        var result = await _handler.Handle(new GetDiscoverDataQuery(10), CancellationToken.None);

        result.NewGames.Should().HaveCount(1);
        result.TopAgents.Should().HaveCount(1);
        result.RecommendedToolkits.Should().HaveCount(1);
        result.RecentKb.Should().HaveCount(1);
        result.TopContributors.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_OneSubQueryFails_PartialSuccessReturnsEmptyForFailedRow()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetNewGamesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<NewGameDto>)Array.Empty<NewGameDto>());
        _mediator.Setup(m => m.Send(It.IsAny<GetTopAgentsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated DB error"));
        _mediator.Setup(m => m.Send(It.IsAny<GetRecommendedToolkitsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RecommendedToolkitDto>)Array.Empty<RecommendedToolkitDto>());
        _mediator.Setup(m => m.Send(It.IsAny<GetRecentKbDocumentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RecentKbDocDto>)Array.Empty<RecentKbDocDto>());
        _mediator.Setup(m => m.Send(It.IsAny<GetTopContributorsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TopContributorDto>)Array.Empty<TopContributorDto>());

        var result = await _handler.Handle(new GetDiscoverDataQuery(10), CancellationToken.None);

        result.TopAgents.Should().BeEmpty();
        result.NewGames.Should().NotBeNull();
        result.RecommendedToolkits.Should().NotBeNull();
        result.RecentKb.Should().NotBeNull();
        result.TopContributors.Should().NotBeNull();

        // Verify error log
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_AllSubQueriesFail_ReturnsAllEmptyArrays_NoException()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetNewGamesQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("fail"));
        _mediator.Setup(m => m.Send(It.IsAny<GetTopAgentsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("fail"));
        _mediator.Setup(m => m.Send(It.IsAny<GetRecommendedToolkitsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("fail"));
        _mediator.Setup(m => m.Send(It.IsAny<GetRecentKbDocumentsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("fail"));
        _mediator.Setup(m => m.Send(It.IsAny<GetTopContributorsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("fail"));

        var result = await _handler.Handle(new GetDiscoverDataQuery(10), CancellationToken.None);

        result.NewGames.Should().BeEmpty();
        result.TopAgents.Should().BeEmpty();
        result.RecommendedToolkits.Should().BeEmpty();
        result.RecentKb.Should().BeEmpty();
        result.TopContributors.Should().BeEmpty();
    }
}

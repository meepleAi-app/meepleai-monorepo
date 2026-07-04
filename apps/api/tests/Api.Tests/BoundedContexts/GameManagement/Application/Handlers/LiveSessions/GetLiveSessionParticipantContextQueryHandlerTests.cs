using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Unit tests for <see cref="GetLiveSessionParticipantContextQueryHandler"/> — the non-throwing
/// authz-resolution query backing the RequireLiveSessionParticipant endpoint filter (#2573).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetLiveSessionParticipantContextQueryHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repositoryMock = new();

    private GetLiveSessionParticipantContextQueryHandler Sut() => new(_repositoryMock.Object);

    private static LiveGameSession CreateSession(Guid sessionId, Guid creatorId) =>
        LiveGameSession.Create(sessionId, creatorId, "Test Game", TimeProvider.System);

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsNotFoundAndUnauthorized()
    {
        var id = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var result = await Sut().Handle(
            new GetLiveSessionParticipantContextQuery(id, Guid.NewGuid()), CancellationToken.None);

        result.Found.Should().BeFalse();
        result.Authorized.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Creator_ReturnsFoundAndAuthorized()
    {
        var id = Guid.NewGuid();
        var creator = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSession(id, creator));

        var result = await Sut().Handle(
            new GetLiveSessionParticipantContextQuery(id, creator), CancellationToken.None);

        result.Found.Should().BeTrue();
        result.Authorized.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonParticipant_ReturnsFoundButUnauthorized()
    {
        var id = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSession(id, Guid.NewGuid()));

        var result = await Sut().Handle(
            new GetLiveSessionParticipantContextQuery(id, Guid.NewGuid()), CancellationToken.None);

        result.Found.Should().BeTrue();
        result.Authorized.Should().BeFalse();
    }
}

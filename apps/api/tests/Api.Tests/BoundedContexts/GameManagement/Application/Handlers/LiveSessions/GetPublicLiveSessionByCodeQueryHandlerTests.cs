using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Unit tests for <see cref="GetPublicLiveSessionByCodeQueryHandler"/> — the narrow, code-as-capability
/// public lobby projection (#2590). Asserts unknown code → null and that the projected DTO type carries
/// no sensitive members (UserId / CreatedByUserId / Notes).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetPublicLiveSessionByCodeQueryHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repo = new();

    private GetPublicLiveSessionByCodeQueryHandler Sut() => new(_repo.Object);

    [Fact]
    public async Task Handle_UnknownCode_ReturnsNull()
    {
        _repo.Setup(r => r.GetByCodeAsync("NOPE12", It.IsAny<CancellationToken>()))
             .ReturnsAsync((LiveGameSession?)null);

        var result = await Sut().Handle(new GetPublicLiveSessionByCodeQuery("NOPE12"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_KnownCode_ProjectsNarrowDto()
    {
        var session = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", TimeProvider.System, gameId: Guid.NewGuid());
        session.AddPlayer(Guid.NewGuid(), "Alice", PlayerColor.Red); // linked user
        _repo.Setup(r => r.GetByCodeAsync(session.SessionCode, It.IsAny<CancellationToken>()))
             .ReturnsAsync(session);

        var result = await Sut().Handle(
            new GetPublicLiveSessionByCodeQuery(session.SessionCode), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(session.Id);
        result.SessionCode.Should().Be(session.SessionCode);
        result.GameName.Should().Be("Catan");
        result.GameSlug.Should().Be("catan");
        result.Players.Should().ContainSingle();
        result.Players[0].DisplayName.Should().Be("Alice");
        result.Players[0].Color.Should().Be(PlayerColor.Red);
    }

    [Fact]
    public void PublicDto_HasNoSensitiveMembers()
    {
        // Compile-time + reflection guard: the public projection must never expose identity/private fields.
        typeof(PublicLiveSessionPlayerDto).GetProperty("UserId").Should().BeNull();
        typeof(PublicLiveSessionDto).GetProperty("CreatedByUserId").Should().BeNull();
        typeof(PublicLiveSessionDto).GetProperty("Notes").Should().BeNull();
        typeof(PublicLiveSessionDto).GetProperty("RoundScores").Should().BeNull();
        typeof(PublicLiveSessionDto).GetProperty("Teams").Should().BeNull();
        typeof(PublicLiveSessionDto).GetProperty("Visibility").Should().BeNull();
    }
}

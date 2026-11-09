using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// #4939: <c>GET /game-sessions/code/{code}</c> used to answer the full <c>SessionDto</c> (owner, participants'
/// user ids, scores) to anyone who knew the code. The code is a credential (#4883), but knowing it is not being
/// at the table: the read is for the owner or a participant, and a stranger gets exactly what an unknown code
/// gets, so the route does not even confirm that the code is live.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "4939")]
public sealed class GetSessionByCodeQueryHandlerTests : IDisposable
{
    private const string Code = "K7X2M9";

    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _participant = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();

    public GetSessionByCodeQueryHandlerTests()
    {
        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = _sessionId,
            UserId = _owner,
            GameId = Guid.NewGuid(),
            SessionCode = Code,
            SessionType = "GameSpecific",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _owner,
            Participants =
            [
                new ParticipantEntity { Id = Guid.NewGuid(), SessionId = _sessionId, UserId = _owner, DisplayName = "Owner", IsOwner = true, JoinOrder = 0 },
                new ParticipantEntity { Id = Guid.NewGuid(), SessionId = _sessionId, UserId = _participant, DisplayName = "Giulia", JoinOrder = 1 },
                new ParticipantEntity { Id = Guid.NewGuid(), SessionId = _sessionId, UserId = null, DisplayName = "Ospite", JoinOrder = 2 },
            ]
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private Task<Api.BoundedContexts.SessionTracking.Application.DTOs.SessionDto?> Ask(string code, Guid requestedBy) =>
        new GetSessionByCodeQueryHandler(_db).Handle(new GetSessionByCodeQuery(code, requestedBy), TestContext.Current.CancellationToken);

    [Fact]
    public async Task TheOwner_ReadsTheFullSession()
    {
        var dto = await Ask(Code, _owner);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(_sessionId);
        dto.Participants.Should().HaveCount(3);
    }

    [Fact]
    public async Task AParticipant_ReadsTheFullSession()
    {
        var dto = await Ask(Code, _participant);

        dto.Should().NotBeNull();
        dto!.UserId.Should().Be(_owner, "a participant sees the session as it is, owner included");
    }

    [Fact]
    public async Task AStrangerHoldingTheCode_GetsWhatAnUnknownCodeGets()
    {
        var stranger = await Ask(Code, Guid.NewGuid());
        var unknown = await Ask("ZZZZZ9", _owner);

        stranger.Should().BeNull("knowing the code is not being at the table");
        unknown.Should().BeNull();
    }

    [Fact]
    public async Task AnEmptyCaller_IsNobody()
    {
        // An endpoint that forgot to derive the caller from the principal would pass Guid.Empty: fail-closed.
        (await Ask(Code, Guid.Empty)).Should().BeNull();
    }
}

using Api.BoundedContexts.GameManagement.Application.Queries.Session;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries.Session;

/// <summary>
/// #4780 — every participant of a session reads the others' names, and a user without a usable one appeared under
/// their address (<c>User.DisplayName ?? User.Email</c>). A guest keeps the name they typed; a user is shown by name,
/// never by an address.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4780")]
public sealed class GetSessionParticipantsQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly GetSessionParticipantsQueryHandler _handler;

    public GetSessionParticipantsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new GetSessionParticipantsQueryHandler(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(null, "Giocatore")]
    [InlineData("marta.rossi@example.com", "Giocatore")]
    [InlineData("Marta (marta.rossi@example.com)", "Giocatore")]
    [InlineData("marta@vecchio.it", "Giocatore")]
    [InlineData("Marta Rossi", "Marta Rossi")]
    public async Task Handle_AUserIsNeverShownByAnAddress(string? profileName, string shown)
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "marta.rossi@example.com",
            DisplayName = profileName,
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        });
        _db.SessionParticipants.Add(Participant(sessionId, userId, guestName: null));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(new GetSessionParticipantsQuery(sessionId), TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.DisplayName.Should().Be(shown);
    }

    [Fact]
    public async Task Handle_AGuestKeepsTheNameTheyTyped_InTheOrderTheyJoined()
    {
        var sessionId = Guid.NewGuid();
        var first = Participant(sessionId, userId: null, guestName: "Luca");
        var second = Participant(sessionId, userId: null, guestName: "Anna");
        second.JoinedAt = first.JoinedAt.AddMinutes(1);
        _db.SessionParticipants.AddRange(second, first);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(new GetSessionParticipantsQuery(sessionId), TestContext.Current.CancellationToken);

        result.Select(p => p.DisplayName).Should().Equal("Luca", "Anna");
    }

    private static SessionParticipantEntity Participant(Guid sessionId, Guid? userId, string? guestName) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        UserId = userId,
        GuestName = guestName,
        Role = "Player",
        ConnectionToken = "123456",
        JoinedAt = DateTime.UtcNow,
    };
}

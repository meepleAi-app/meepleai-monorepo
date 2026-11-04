using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

/// <summary>
/// #4846. The 24 hour reminder to a guest is written once per address: the job emits its event once per night, but
/// the event reaches the handler through the outbox, which in <c>Hybrid</c> mode delivers it twice. What stops the
/// second delivery from writing again is <see cref="GameNightInvitation.Reminder24hSentAt"/>, and that only holds if
/// the mark makes the round trip: the invitation is rewritten WHOLE from the aggregate on update
/// (<c>DbContext.Update(entity)</c>), so a mapper that leaves the column out would not fail anywhere — it would
/// store it as empty, and the guest would be written to again at every delivery. Same hazard, same test as the
/// cancellation mark (#4853). Runs on the EF in-memory provider: it covers the mapping, not the SQL.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4846")]
public sealed class GameNightInvitationReminderMarkMappingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly MeepleAiDbContext _dbContext;
    private readonly GameNightInvitationRepository _repository;

    public GameNightInvitationReminderMarkMappingTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"InvitationReminderMarkTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
        _repository = new GameNightInvitationRepository(
            _dbContext,
            new Mock<IDomainEventCollector>().Object,
            NullLogger<GameNightInvitationRepository>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    private static GameNightInvitation Invitation(
        DateTimeOffset? reminder24hSentAt = null,
        DateTimeOffset? cancellationNoticeSentAt = null,
        GameNightInvitationStatus status = GameNightInvitationStatus.Accepted) =>
        GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: Guid.NewGuid().ToString("N")[..22],
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            status: status,
            expiresAt: Now.AddDays(3),
            respondedAt: Now,
            respondedByUserId: null,
            createdAt: Now.AddDays(-2),
            createdBy: Guid.NewGuid(),
            cancellationNoticeSentAt: cancellationNoticeSentAt,
            reminder24hSentAt: reminder24hSentAt);

    /// <summary>Written by one request, read by another: nothing carries over but the stored row.</summary>
    private async Task<GameNightInvitation> StoreThenReadAsync(GameNightInvitation invitation)
    {
        await _repository.AddAsync(invitation, CancellationToken.None);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
        return (await _repository.GetByIdAsync(invitation.Id, CancellationToken.None))!;
    }

    [Fact]
    public void AnInvitationNeverRemindedTo_HasNoMark()
    {
        Invitation().Reminder24hSentAt.Should().BeNull();
    }

    [Fact]
    public void MarkingIt_SetsTheTimeItWasWrittenAt()
    {
        var invitation = Invitation();

        invitation.MarkReminder24hSent(Now);

        invitation.Reminder24hSentAt.Should().Be(Now);
    }

    [Fact]
    public async Task AnInvitationStoredWithoutTheMark_ComesBackWithoutIt()
    {
        var read = await StoreThenReadAsync(Invitation());

        read.Reminder24hSentAt.Should().BeNull();
    }

    [Fact]
    public async Task TheMarkOfAnInvitationAddedWithIt_ComesBack()
    {
        var read = await StoreThenReadAsync(Invitation(reminder24hSentAt: Now));

        read.Reminder24hSentAt.Should().Be(Now);
    }

    [Fact]
    public async Task TheMarkSetOnAnExistingInvitation_IsStoredByTheUpdate_AndComesBack()
    {
        // The path of the handler: read the invitation, mark it, write it back whole, read it again.
        var stored = await StoreThenReadAsync(Invitation());
        stored.MarkReminder24hSent(Now);

        await _repository.UpdateAsync(stored, CancellationToken.None);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
        var read = await _repository.GetByIdAsync(stored.Id, CancellationToken.None);

        read!.Reminder24hSentAt.Should().Be(Now);
    }

    [Fact]
    public async Task AnUpdateOfSomethingElse_DoesNotEraseTheMark()
    {
        // The other direction: the invitation is rewritten whole by the RSVP handlers too, from what they read.
        var stored = await StoreThenReadAsync(
            Invitation(reminder24hSentAt: Now, status: GameNightInvitationStatus.Pending));

        stored.Accept(userId: null, utcNow: Now.AddMinutes(1)).Should().BeTrue();
        await _repository.UpdateAsync(stored, CancellationToken.None);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
        var read = await _repository.GetByIdAsync(stored.Id, CancellationToken.None);

        read!.Reminder24hSentAt.Should().Be(Now);
    }

    [Fact]
    public async Task TheTwoMarks_AreIndependent()
    {
        // A cancellation notice and a reminder are different emails: one must not stand in for the other.
        var read = await StoreThenReadAsync(Invitation(cancellationNoticeSentAt: Now));

        read.CancellationNoticeSentAt.Should().Be(Now);
        read.Reminder24hSentAt.Should().BeNull();
    }
}

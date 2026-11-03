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
/// #4853. The handler of the cancellation email lets a second delivery of the event write nothing because of
/// <see cref="GameNightInvitation.CancellationNoticeSentAt"/>. That only holds if the mark makes the round trip:
/// the invitation is rewritten WHOLE from the aggregate on update (<c>DbContext.Update(entity)</c>), so a mapper
/// that leaves the column out would not fail anywhere — it would store it as empty, and the guest would be
/// written to again at every delivery. Runs on the EF in-memory provider: it covers the mapping, not the SQL.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightInvitationCancellationMarkMappingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly MeepleAiDbContext _dbContext;
    private readonly GameNightInvitationRepository _repository;

    public GameNightInvitationCancellationMarkMappingTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"InvitationMarkTests_{Guid.NewGuid()}")
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
            cancellationNoticeSentAt: cancellationNoticeSentAt);

    /// <summary>Written by one request, read by another: nothing carries over but the stored row.</summary>
    private async Task<GameNightInvitation> StoreThenReadAsync(GameNightInvitation invitation)
    {
        await _repository.AddAsync(invitation, CancellationToken.None);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
        return (await _repository.GetByIdAsync(invitation.Id, CancellationToken.None))!;
    }

    [Fact]
    public async Task AnInvitationNeverWrittenTo_ComesBackWithoutTheMark()
    {
        var read = await StoreThenReadAsync(Invitation());

        read.CancellationNoticeSentAt.Should().BeNull();
    }

    [Fact]
    public async Task TheMarkOfAnInvitationAddedWithIt_ComesBack()
    {
        var read = await StoreThenReadAsync(Invitation(cancellationNoticeSentAt: Now));

        read.CancellationNoticeSentAt.Should().Be(Now);
    }

    [Fact]
    public async Task TheMarkSetOnAnExistingInvitation_IsStoredByTheUpdate_AndComesBack()
    {
        // The path of the handler: read the invitation, mark it, write it back whole, read it again.
        var stored = await StoreThenReadAsync(Invitation());
        stored.MarkCancellationNoticeSent(Now);

        await _repository.UpdateAsync(stored, CancellationToken.None);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
        var read = await _repository.GetByIdAsync(stored.Id, CancellationToken.None);

        read!.CancellationNoticeSentAt.Should().Be(Now);
    }

    [Fact]
    public async Task AnUpdateOfSomethingElse_DoesNotEraseTheMark()
    {
        // The other direction: the invitation is rewritten whole by the RSVP handlers too, from what they read.
        var stored = await StoreThenReadAsync(
            Invitation(cancellationNoticeSentAt: Now, status: GameNightInvitationStatus.Pending));

        stored.Accept(userId: null, utcNow: Now.AddMinutes(1)).Should().BeTrue();
        await _repository.UpdateAsync(stored, CancellationToken.None);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
        var read = await _repository.GetByIdAsync(stored.Id, CancellationToken.None);

        read!.CancellationNoticeSentAt.Should().Be(Now);
    }
}

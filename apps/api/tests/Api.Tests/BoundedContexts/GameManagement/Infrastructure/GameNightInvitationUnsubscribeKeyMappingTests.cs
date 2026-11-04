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
/// #4846. The key that stops a guest's emails is not the RSVP token: the organizer holds that one (it comes back in
/// the 201 of the creation), and the opt-out is global, so it needs a credential only the address has seen. The key
/// is stored on the invitation and looked up by it. The invitation is rewritten WHOLE from the aggregate on update
/// (<c>DbContext.Update(entity)</c>), so a mapper that leaves the column out would not fail anywhere: it would
/// store it as empty, and the link already in the guest's inbox would stop working. Runs on the EF in-memory
/// provider: it covers the mapping and the lookup, not the SQL (the unique index is the migration's).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4846")]
public sealed class GameNightInvitationUnsubscribeKeyMappingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly MeepleAiDbContext _dbContext;
    private readonly GameNightInvitationRepository _repository;

    public GameNightInvitationUnsubscribeKeyMappingTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"InvitationKeyTests_{Guid.NewGuid()}")
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

    private static GameNightInvitation Invitation(string key, GameNightInvitationStatus status = GameNightInvitationStatus.Pending) =>
        GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: Guid.NewGuid().ToString("N")[..22],
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            status: status,
            expiresAt: Now.AddDays(3),
            respondedAt: null,
            respondedByUserId: null,
            createdAt: Now.AddDays(-2),
            createdBy: Guid.NewGuid(),
            unsubscribeKey: key);

    private async Task<GameNightInvitation> StoreThenReadAsync(GameNightInvitation invitation)
    {
        await _repository.AddAsync(invitation, CancellationToken.None);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
        return (await _repository.GetByIdAsync(invitation.Id, CancellationToken.None))!;
    }

    [Fact]
    public async Task TheKeyOfAStoredInvitation_ComesBack()
    {
        var read = await StoreThenReadAsync(Invitation("KeyOneKeyOneKeyOneKeyOn"));

        read.UnsubscribeKey.Should().Be("KeyOneKeyOneKeyOneKeyOn");
    }

    [Fact]
    public async Task AnUpdateOfSomethingElse_DoesNotEraseTheKey()
    {
        var stored = await StoreThenReadAsync(Invitation("KeyOneKeyOneKeyOneKeyOn"));

        stored.Accept(userId: null, utcNow: Now).Should().BeTrue();
        await _repository.UpdateAsync(stored, CancellationToken.None);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
        var read = await _repository.GetByIdAsync(stored.Id, CancellationToken.None);

        read!.UnsubscribeKey.Should().Be("KeyOneKeyOneKeyOneKeyOn");
    }

    [Fact]
    public async Task TheInvitationIsFoundByItsKey_WhateverItsState()
    {
        foreach (var status in new[] { GameNightInvitationStatus.Pending, GameNightInvitationStatus.Accepted, GameNightInvitationStatus.Cancelled, GameNightInvitationStatus.Expired })
        {
            var key = $"Key{status}".PadRight(22, 'x');
            var stored = await StoreThenReadAsync(Invitation(key, status));

            var found = await _repository.GetByUnsubscribeKeyAsync(key, CancellationToken.None);

            found.Should().NotBeNull($"a {status} invitation still holds a link in someone's inbox");
            found!.Id.Should().Be(stored.Id);
        }
    }

    [Fact]
    public async Task AKeyNobodyHolds_FindsNothing()
    {
        await StoreThenReadAsync(Invitation("KeyOneKeyOneKeyOneKeyOn"));

        (await _repository.GetByUnsubscribeKeyAsync("SomethingElseEntirelyXX", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task TheRsvpToken_FindsNothingAsAKey()
    {
        var stored = await StoreThenReadAsync(Invitation("KeyOneKeyOneKeyOneKeyOn"));

        (await _repository.GetByUnsubscribeKeyAsync(stored.Token, CancellationToken.None))
            .Should().BeNull("the organizer holds the token and must not be able to use it as the key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AskingWithNoKey_IsAnError_NotAnAnswer(string? key)
    {
        var act = () => _repository.GetByUnsubscribeKeyAsync(key!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}

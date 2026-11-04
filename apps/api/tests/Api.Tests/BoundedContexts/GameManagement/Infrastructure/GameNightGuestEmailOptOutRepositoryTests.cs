using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

/// <summary>
/// #4846. The list is asked «is this address on it?» before every email to a guest, so the question has to give
/// the same answer however the address is spelled, and an entry has to survive the round trip through the
/// stored row. Runs on the EF in-memory provider: it covers the mapping and the lookup, not the unique index
/// (that is the migration's, and the handler's «lost the race» branch is covered with a faked unit of work).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4846")]
public sealed class GameNightGuestEmailOptOutRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2031, 5, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly MeepleAiDbContext _dbContext;
    private readonly GameNightGuestEmailOptOutRepository _repository;

    public GameNightGuestEmailOptOutRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"GuestOptOutTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
        _repository = new GameNightGuestEmailOptOutRepository(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private async Task StoreAsync(string email, Guid? invitationId = null)
    {
        await _repository.AddAsync(
            GameNightGuestEmailOptOut.Create(email, invitationId ?? Guid.NewGuid(), Now), CancellationToken.None);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AnAddressNeverAdded_IsNotOnTheList()
    {
        (await _repository.ExistsAsync("nobody@example.com", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task AnAddressAdded_IsOnTheList_AfterTheRoundTripThroughTheStoredRow()
    {
        await StoreAsync("guest@example.com");

        (await _repository.ExistsAsync("guest@example.com", CancellationToken.None)).Should().BeTrue();
    }

    [Theory]
    [InlineData("GUEST@example.com")]
    [InlineData("  guest@Example.COM ")]
    [InlineData("Guest Person <GUEST@example.com>")]
    public async Task TheLookup_GivesTheSameAnswerHoweverTheAddressIsSpelled(string spelled)
    {
        await StoreAsync("guest@example.com");

        (await _repository.ExistsAsync(spelled, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task AnEntryAddedUnderOneSpelling_IsStoredNormalized_SoAnotherSpellingFindsIt()
    {
        await StoreAsync("  Guest@Example.COM ");

        var stored = await _dbContext.GameNightGuestEmailOptOuts.AsNoTracking().SingleAsync(CancellationToken.None);
        stored.Email.Should().Be("guest@example.com");
    }

    [Fact]
    public async Task TheTraceOfWhereTheRequestCameFrom_IsStoredWithTheEntry()
    {
        var invitationId = Guid.NewGuid();
        await StoreAsync("guest@example.com", invitationId);

        var stored = await _dbContext.GameNightGuestEmailOptOuts.AsNoTracking().SingleAsync(CancellationToken.None);
        stored.SourceInvitationId.Should().Be(invitationId);
        stored.OptedOutAt.Should().Be(Now);
    }

    [Fact]
    public async Task AnotherAddress_IsNotOnTheListBecauseOneIs()
    {
        await StoreAsync("guest@example.com");

        (await _repository.ExistsAsync("someone-else@example.com", CancellationToken.None)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AskingAboutNoAddress_IsAnError_NotAnAnswer(string? email)
    {
        var act = () => _repository.ExistsAsync(email!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}

using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Infrastructure.Persistence;

/// <summary>
/// Tests for the membership read-port <see cref="GroupMemoryRepository.GetGroupIdsForUserAsync"/>
/// added for the game leaderboard (#1467). Membership is the inverse of <c>GetByCreatorIdAsync</c>:
/// "which groups is this user a member of". Members are persisted as JSONB, so the lookup
/// deserializes in-memory.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
[Trait("Issue", "1467")]
public sealed class GroupMemoryRepositoryMembershipTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly GroupMemoryRepository _repo;

    public GroupMemoryRepositoryMembershipTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"GroupMembership_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
        _repo = new GroupMemoryRepository(_db, new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetGroupIdsForUserAsync_ReturnsOnlyGroupsWhereUserIsMember()
    {
        var targetUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();

        var mine = GroupMemory.Create(Guid.NewGuid(), "My Group");
        mine.AddMember(targetUser);
        var theirs = GroupMemory.Create(Guid.NewGuid(), "Other Group");
        theirs.AddMember(otherUser);

        await _repo.AddAsync(mine, TestContext.Current.CancellationToken);
        await _repo.AddAsync(theirs, TestContext.Current.CancellationToken);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _repo.GetGroupIdsForUserAsync(targetUser, TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Should().Be(mine.Id);
    }

    [Fact]
    public async Task GetGroupIdsForUserAsync_UserInMultipleGroups_ReturnsAll()
    {
        var targetUser = Guid.NewGuid();

        var g1 = GroupMemory.Create(Guid.NewGuid(), "Group 1");
        g1.AddMember(targetUser);
        var g2 = GroupMemory.Create(Guid.NewGuid(), "Group 2");
        g2.AddMember(targetUser);

        await _repo.AddAsync(g1, TestContext.Current.CancellationToken);
        await _repo.AddAsync(g2, TestContext.Current.CancellationToken);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _repo.GetGroupIdsForUserAsync(targetUser, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new[] { g1.Id, g2.Id });
    }

    [Fact]
    public async Task GetGroupIdsForUserAsync_NoMembership_ReturnsEmpty()
    {
        var g = GroupMemory.Create(Guid.NewGuid(), "Group");
        g.AddMember(Guid.NewGuid());
        await _repo.AddAsync(g, TestContext.Current.CancellationToken);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _repo.GetGroupIdsForUserAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGroupIdsForUserAsync_GuestMembersAreIgnored()
    {
        var g = GroupMemory.Create(Guid.NewGuid(), "Group");
        g.AddGuestMember("Marco");
        await _repo.AddAsync(g, TestContext.Current.CancellationToken);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _repo.GetGroupIdsForUserAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGroupIdsForUserAsync_EmptyUserId_ReturnsEmpty()
    {
        var result = await _repo.GetGroupIdsForUserAsync(Guid.Empty, TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }
}

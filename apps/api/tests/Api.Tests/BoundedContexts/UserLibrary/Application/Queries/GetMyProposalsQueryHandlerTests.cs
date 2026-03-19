using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;

/// <summary>
/// Unit tests for GetMyProposalsQueryHandler.
/// Issue #3665: Phase 4 - Proposal System.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GetMyProposalsQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly Mock<ILogger<GetMyProposalsQueryHandler>> _loggerMock;
    private readonly GetMyProposalsQueryHandler _handler;

    public GetMyProposalsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"GetMyProposalsTests_{Guid.NewGuid()}")
            .Options;

        var mediatorMock = new Mock<IMediator>();
        var domainEventCollectorMock = new Mock<IDomainEventCollector>();

        _context = new MeepleAiDbContext(options, mediatorMock.Object, domainEventCollectorMock.Object);
        _loggerMock = new Mock<ILogger<GetMyProposalsQueryHandler>>();
        _handler = new GetMyProposalsQueryHandler(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyUserProposals()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        // User's proposals (PrivateGame entities are included in ShareRequest)
        var proposal1 = CreateProposalEntity(userId, Guid.NewGuid(), "Game 1");
        var proposal2 = CreateProposalEntity(userId, Guid.NewGuid(), "Game 2");

        // Other user's proposal
        var otherProposal = CreateProposalEntity(otherUserId, Guid.NewGuid(), "Other Game");

        // Non-proposal ShareRequest
        var nonProposal = CreateShareRequestEntity(userId, Guid.NewGuid(), ContributionType.NewGame);

        await _context.Set<ShareRequestEntity>().AddRangeAsync(
            proposal1, proposal2, otherProposal, nonProposal);
        await _context.SaveChangesAsync();

        var query = new GetMyProposalsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(p =>
        {
            p.ContributionType.Should().Be(ContributionType.NewGameProposal);
        });
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsFilteredProposals()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var pendingProposal = CreateProposalEntity(
            userId, Guid.NewGuid(), "Game 1", ShareRequestStatus.Pending);
        var approvedProposal = CreateProposalEntity(
            userId, Guid.NewGuid(), "Game 2", ShareRequestStatus.Approved);

        await _context.Set<ShareRequestEntity>().AddRangeAsync(pendingProposal, approvedProposal);
        await _context.SaveChangesAsync();

        var query = new GetMyProposalsQuery(userId, ShareRequestStatus.Pending);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items.First().Status.Should().Be(ShareRequestStatus.Pending);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();

        for (int i = 1; i <= 5; i++)
        {
            var proposal = CreateProposalEntity(userId, Guid.NewGuid(), $"Game {i}");
            await _context.Set<ShareRequestEntity>().AddAsync(proposal);
        }

        await _context.SaveChangesAsync();

        var query = new GetMyProposalsQuery(userId, null, 2, 2); // Page 2, size 2

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Total.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NoProposals_ReturnsEmptyResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetMyProposalsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    private static ShareRequestEntity CreateProposalEntity(
        Guid userId,
        Guid privateGameId,
        string gameTitle,
        ShareRequestStatus status = ShareRequestStatus.Pending)
    {
        return new ShareRequestEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SourceGameId = Guid.Empty,
            SourcePrivateGameId = privateGameId,
            TargetSharedGameId = null,
            Status = (int)status,
            ContributionType = (int)ContributionType.NewGameProposal,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            PrivateGame = new PrivateGameEntity
            {
                Id = privateGameId,
                OwnerId = userId,
                Title = gameTitle,
                MinPlayers = 2,
                MaxPlayers = 4,
#pragma warning disable MA0099 // PrivateGameSource.Manual is the explicit enum value (value happens to be 0)
                Source = (int)PrivateGameSource.Manual,
#pragma warning restore MA0099
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            }
        };
    }

    private static ShareRequestEntity CreateShareRequestEntity(
        Guid userId,
        Guid sourceGameId,
        ContributionType contributionType)
    {
        return new ShareRequestEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SourceGameId = sourceGameId,
            Status = (int)ShareRequestStatus.Pending,
            ContributionType = (int)contributionType,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
